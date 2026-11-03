using System.Globalization;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using RustIQ.Application.Maps;
using RustIQ.Application.ServerQuery;
using RustIQ.Infrastructure.ServerQuery;
using RustIQ.Application.Study;

namespace RustIQ.Infrastructure.Maps;

public sealed class A2sRustServerMapMetadataClient(
    IOptions<RustServerQueryOptions> options)
    : IRustServerMapMetadataClient, IRustServerDescriptionClient
{
    private const string SourceName = "A2S";
    private const byte RulesRequestType = 0x56;

    private static readonly byte[] RulesChallengeRequest = A2sPacket.BuildChallengeRequest(RulesRequestType);

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

    public async Task<RustServerDescriptionResult> GetDescriptionAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        try
        {
            var rules = await QueryRulesAsync(configuredServer, cancellationToken);
            return new RustServerDescriptionResult(true, RustWipeScheduleParser.GetDescription(rules));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Timeouts and refused rules queries are both just "no answer" here; the study counts them.
            return new RustServerDescriptionResult(false, null);
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

        TrafficCounters.CountA2sPacketSent();
        await client.SendAsync(RulesChallengeRequest, timeoutToken);
        var challengeResponse = (await client.ReceiveAsync(timeoutToken)).Buffer;
        var challenge = A2sPacket.CopyChallenge(challengeResponse);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var rulesRequest = A2sPacket.BuildRequest(RulesRequestType, [], challenge);

            TrafficCounters.CountA2sPacketSent();
            await client.SendAsync(rulesRequest, timeoutToken);
            var rulesPayload = await A2sPacket.ReceiveResponseAsync(client, timeoutToken);
            if (!A2sPacket.IsChallengeResponse(rulesPayload))
            {
                return RustServerRuleParser.Parse(rulesPayload);
            }

            challenge = A2sPacket.CopyChallenge(rulesPayload);
        }

        throw new InvalidDataException("The server repeatedly replaced its A2S rules challenge.");
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
