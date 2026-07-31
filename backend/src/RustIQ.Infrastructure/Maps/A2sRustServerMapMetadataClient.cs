using System.Buffers.Binary;
using System.Globalization;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using RustIQ.Application.Maps;
using RustIQ.Application.ServerQuery;
using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Infrastructure.Maps;

public sealed class A2sRustServerMapMetadataClient(
    IOptions<RustServerQueryOptions> options)
    : IRustServerMapMetadataClient
{
    private const string SourceName = "A2S";
    private const byte ChallengeResponseType = 0x41;
    private const byte RulesRequestType = 0x56;
    private const int SimplePacketHeader = -1;
    private const int SplitPacketHeader = -2;

    private static readonly byte[] RulesChallengeRequest =
    [
        0xff, 0xff, 0xff, 0xff,
        RulesRequestType,
        0xff, 0xff, 0xff, 0xff
    ];

    private readonly RustServerQueryOptions _options = options.Value;

    public async Task<RustServerMapMetadata> GetMapMetadataAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        var fetchedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            var rules = await QueryRulesAsync(configuredServer, cancellationToken);

            var mapFileUrl = GetRule(rules, "level_url");
            var thumbnailUrl = GetRule(rules, "map_image_url");
            var seed = GetIntRule(rules, "world.seed");
            var size = GetIntRule(rules, "world.size");
            var rustMapsUrl = RustServerRuleParser.BuildRustMapsUrl(mapFileUrl);
            var mapName = NullIfWhiteSpace(configuredServer.Map);
            var lastWipeAtUtc = configuredServer.LastWipeAtUtc;
            var advertisedNextWipeAtUtc = RustWipeScheduleParser.GetNextWipeAtUtc(
                rules,
                lastWipeAtUtc,
                fetchedAtUtc);
            var nextWipeAtUtc = RustWipeScheduleParser.GetEarliestUpcomingWipeAtUtc(
                fetchedAtUtc,
                advertisedNextWipeAtUtc,
                configuredServer.NextWipeAtUtc);

            if (seed is null
                && size is null
                && mapFileUrl is null
                && thumbnailUrl is null)
            {
                return Unavailable(
                    configuredServer,
                    fetchedAtUtc,
                    "The game server did not expose map metadata through A2S rules.");
            }

            return new RustServerMapMetadata(
                configuredServer.Host,
                configuredServer.QueryPort,
                mapName,
                seed,
                size,
                rustMapsUrl,
                thumbnailUrl,
                mapFileUrl,
                SourceName,
                SourceUpdatedAtUtc: null,
                fetchedAtUtc,
                Error: null,
                nextWipeAtUtc,
                lastWipeAtUtc);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Unavailable(
                configuredServer,
                DateTimeOffset.UtcNow,
                exception.Message);
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> QueryRulesAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMilliseconds));
        var timeoutToken = timeoutCancellation.Token;

        using var client = new UdpClient(AddressFamily.InterNetwork);
        client.Connect(configuredServer.Host, configuredServer.QueryPort);

        await client.SendAsync(RulesChallengeRequest, timeoutToken);
        var challengeResponse = (await client.ReceiveAsync(timeoutToken)).Buffer;
        if (!IsChallengeResponse(challengeResponse))
        {
            throw new InvalidDataException("The server returned an invalid A2S challenge.");
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var rulesRequest = new byte[9];
            BinaryPrimitives.WriteInt32LittleEndian(rulesRequest, SimplePacketHeader);
            rulesRequest[4] = RulesRequestType;
            challengeResponse.AsSpan(5, 4).CopyTo(rulesRequest.AsSpan(5));

            await client.SendAsync(rulesRequest, timeoutToken);
            var rulesPayload = await ReceiveResponseAsync(client, timeoutToken);
            if (!IsChallengeResponse(rulesPayload))
            {
                return RustServerRuleParser.Parse(rulesPayload);
            }

            challengeResponse = rulesPayload;
        }

        throw new InvalidDataException("The server repeatedly replaced its A2S rules challenge.");
    }

    private static bool IsChallengeResponse(byte[] payload)
    {
        return payload.Length >= 9
               && BinaryPrimitives.ReadInt32LittleEndian(payload) == SimplePacketHeader
               && payload[4] == ChallengeResponseType;
    }

    private static async Task<byte[]> ReceiveResponseAsync(
        UdpClient client,
        CancellationToken cancellationToken)
    {
        var firstPacket = (await client.ReceiveAsync(cancellationToken)).Buffer;
        if (firstPacket.Length < 4)
        {
            throw new InvalidDataException("The server returned an incomplete A2S response.");
        }

        var header = BinaryPrimitives.ReadInt32LittleEndian(firstPacket);
        if (header == SimplePacketHeader)
        {
            return firstPacket;
        }

        if (header != SplitPacketHeader || firstPacket.Length < 12)
        {
            throw new InvalidDataException("The server returned an invalid A2S packet header.");
        }

        var responseId = BinaryPrimitives.ReadInt32LittleEndian(firstPacket.AsSpan(4));
        if ((responseId & int.MinValue) != 0)
        {
            throw new InvalidDataException("Compressed split A2S responses are not supported.");
        }

        var packetCount = firstPacket[8];
        if (packetCount is 0 or > 32)
        {
            throw new InvalidDataException("The server returned an invalid A2S split-packet count.");
        }

        var fragments = new byte[packetCount][];
        AddSplitPacket(firstPacket, responseId, fragments);

        while (fragments.Any(fragment => fragment is null))
        {
            var packet = (await client.ReceiveAsync(cancellationToken)).Buffer;
            AddSplitPacket(packet, responseId, fragments);
        }

        var length = fragments.Sum(fragment => fragment.Length);
        var payload = new byte[length];
        var offset = 0;
        foreach (var fragment in fragments)
        {
            fragment.CopyTo(payload, offset);
            offset += fragment.Length;
        }

        return payload;
    }

    private static void AddSplitPacket(
        byte[] packet,
        int expectedResponseId,
        byte[][] fragments)
    {
        if (packet.Length < 12
            || BinaryPrimitives.ReadInt32LittleEndian(packet) != SplitPacketHeader
            || BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(4)) != expectedResponseId
            || packet[8] != fragments.Length
            || packet[9] >= fragments.Length)
        {
            throw new InvalidDataException("The server returned an invalid A2S split packet.");
        }

        fragments[packet[9]] = packet[12..];
    }

    private static int? GetIntRule(
        IReadOnlyDictionary<string, string> rules,
        string name)
    {
        var value = GetRule(rules, name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? GetRule(
        IReadOnlyDictionary<string, string> rules,
        string name)
    {
        return rules.TryGetValue(name, out var value)
            ? NullIfWhiteSpace(value)
            : null;
    }

    private static RustServerMapMetadata Unavailable(
        ConfiguredRustServer configuredServer,
        DateTimeOffset fetchedAtUtc,
        string error)
    {
        return new RustServerMapMetadata(
            configuredServer.Host,
            configuredServer.QueryPort,
            MapName: null,
            Seed: null,
            Size: null,
            RustMapsUrl: null,
            ThumbnailUrl: null,
            MapFileUrl: null,
            SourceName,
            SourceUpdatedAtUtc: null,
            fetchedAtUtc,
            error,
            RustWipeScheduleParser.GetEarliestUpcomingWipeAtUtc(
                fetchedAtUtc,
                configuredServer.NextWipeAtUtc),
            configuredServer.LastWipeAtUtc);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
