using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;

namespace RustIQ.Infrastructure.ServerQuery;

public sealed class SteamRustServerQueryClient(
    IOptions<RustServerQueryOptions> options)
    : IRustServerQueryClient
{
    private const byte InfoRequestType = 0x54;
    private const byte PlayerRequestType = 0x55;

    // A server that keeps handing back a fresh challenge instead of an answer is not going to settle,
    // so the exchange is capped rather than looped until the timeout.
    private const int MaximumChallengeExchanges = 3;

    private static readonly byte[] InfoRequestBody = Encoding.ASCII.GetBytes("Source Engine Query\0");

    private readonly RustServerQueryOptions _options = options.Value;

    public async Task<RustServerQueryResult> QueryServerAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var attempts = Math.Max(1, _options.MaxQueryAttempts);
        Exception? lastFailure = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var serverInfo = await QueryServerInfoAsync(configuredServer, cancellationToken);
                stopwatch.Stop();
                return OnlineResult(
                    configuredServer,
                    serverInfo,
                    DateTimeOffset.UtcNow,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // UDP has no delivery guarantee and Rust servers rate limit queries, so one silent
                // attempt is not evidence that a server is down. Retrying here keeps a dropped packet
                // from spending the caller's whole offline grace period.
                lastFailure = exception;
            }
        }

        stopwatch.Stop();
        return OfflineResult(
            configuredServer,
            DateTimeOffset.UtcNow,
            stopwatch.ElapsedMilliseconds,
            DescribeFailure(lastFailure));
    }

    public async Task<IReadOnlyList<RustServerPlayerResult>> QueryPlayersAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, _options.MaxQueryAttempts);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var players = await QueryPlayerListAsync(configuredServer, cancellationToken);
                return players
                    .Select(player => new RustServerPlayerResult(
                        NullIfWhiteSpace(player.Name),
                        player.DurationSeconds))
                    .ToArray();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // A missing player list is normal - plenty of servers refuse or rate limit A2S_PLAYER -
                // so this stays best effort and the caller shows "player list unavailable".
            }
        }

        return [];
    }

    private async Task<A2sServerInfo> QueryServerInfoAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMilliseconds));
        var timeoutToken = timeoutCancellation.Token;

        using var client = new UdpClient(AddressFamily.InterNetwork);
        client.Connect(configuredServer.Host, configuredServer.QueryPort);

        var request = A2sPacket.BuildRequest(InfoRequestType, InfoRequestBody, []);

        for (var exchange = 0; exchange < MaximumChallengeExchanges; exchange++)
        {
            await client.SendAsync(request, timeoutToken);
            var response = await A2sPacket.ReceiveResponseAsync(client, timeoutToken);
            if (!A2sPacket.IsChallengeResponse(response))
            {
                return A2sResponseParser.ParseServerInfo(response);
            }

            // Servers may answer A2S_INFO with a challenge that has to be echoed back before they will
            // send the real payload. The A2S package this replaced had no idea what to do with that and
            // returned null, which RustIQ then recorded as the server being offline.
            request = A2sPacket.BuildRequest(
                InfoRequestType,
                InfoRequestBody,
                A2sPacket.CopyChallenge(response));
        }

        throw new InvalidDataException("The server repeatedly replaced its A2S_INFO challenge.");
    }

    private async Task<IReadOnlyList<A2sPlayer>> QueryPlayerListAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMilliseconds));
        var timeoutToken = timeoutCancellation.Token;

        using var client = new UdpClient(AddressFamily.InterNetwork);
        client.Connect(configuredServer.Host, configuredServer.QueryPort);

        var request = A2sPacket.BuildChallengeRequest(PlayerRequestType);

        for (var exchange = 0; exchange < MaximumChallengeExchanges; exchange++)
        {
            await client.SendAsync(request, timeoutToken);
            var response = await A2sPacket.ReceiveResponseAsync(client, timeoutToken);
            if (!A2sPacket.IsChallengeResponse(response))
            {
                return A2sResponseParser.ParsePlayers(response);
            }

            request = A2sPacket.BuildRequest(
                PlayerRequestType,
                [],
                A2sPacket.CopyChallenge(response));
        }

        throw new InvalidDataException("The server repeatedly replaced its A2S_PLAYER challenge.");
    }

    private static RustServerQueryResult OnlineResult(
        ConfiguredRustServer configuredServer,
        A2sServerInfo serverInfo,
        DateTimeOffset checkedAtUtc,
        long pingMilliseconds)
    {
        var lastWipeAtUtc = RustServerKeywordParser.GetLastWipeAtUtc(
            serverInfo.Keywords,
            checkedAtUtc);
        var advertisedNextWipeAtUtc = RustServerKeywordParser.GetNextWipeAtUtc(
            serverInfo.Keywords,
            checkedAtUtc);
        var nextWipeAtUtc = RustWipeScheduleParser.GetEarliestUpcomingWipeAtUtc(
            checkedAtUtc,
            advertisedNextWipeAtUtc,
            configuredServer.NextWipeAtUtc);

        // Rust carries the real counts in its keywords because the A2S player fields are single bytes.
        var players = RustServerKeywordParser.GetCurrentPlayers(serverInfo.Keywords)
            ?? serverInfo.Players;
        var maxPlayers = RustServerKeywordParser.GetMaxPlayers(serverInfo.Keywords)
            ?? serverInfo.MaxPlayers;
        var queuePlayers = RustServerKeywordParser.GetQueuePlayers(serverInfo.Keywords)
            ?? configuredServer.QueuePlayers;

        return new RustServerQueryResult(
            configuredServer.BattleMetricsServerId,
            configuredServer.Address,
            configuredServer.Host,
            configuredServer.GamePort,
            configuredServer.QueryPort,
            configuredServer.Country,
            configuredServer.Latitude,
            configuredServer.Longitude,
            lastWipeAtUtc ?? configuredServer.LastWipeAtUtc,
            nextWipeAtUtc,
            NullIfWhiteSpace(serverInfo.Name),
            NullIfWhiteSpace(serverInfo.Map),
            players,
            maxPlayers,
            queuePlayers,
            IsOnline: true,
            pingMilliseconds,
            checkedAtUtc,
            ServerPlayerCountSource.A2S,
            ServerSummarySource.A2S,
            // A poll is the freshest view of what a server calls itself, so it re-reads the type
            // every time: a community server that installs mods changes category mid-wipe.
            RustServerTypeParser.GetServerType(serverInfo.Name, serverInfo.Keywords),
            Error: null);
    }

    private static RustServerQueryResult OfflineResult(
        ConfiguredRustServer configuredServer,
        DateTimeOffset checkedAtUtc,
        long pingMilliseconds,
        string error)
    {
        return new RustServerQueryResult(
            configuredServer.BattleMetricsServerId,
            configuredServer.Address,
            configuredServer.Host,
            configuredServer.GamePort,
            configuredServer.QueryPort,
            configuredServer.Country,
            configuredServer.Latitude,
            configuredServer.Longitude,
            configuredServer.LastWipeAtUtc,
            RustWipeScheduleParser.GetEarliestUpcomingWipeAtUtc(
                checkedAtUtc,
                configuredServer.NextWipeAtUtc),
            Name: null,
            Map: null,
            Players: 0,
            MaxPlayers: 0,
            QueuePlayers: configuredServer.QueuePlayers,
            IsOnline: false,
            pingMilliseconds,
            checkedAtUtc,
            configuredServer.PlayerCountSource,
            configuredServer.SummarySource,
            // A server that did not answer tells us nothing new about its category, so the one we
            // already had stands.
            configuredServer.ServerType,
            error);
    }

    private static string DescribeFailure(Exception? failure)
    {
        return failure switch
        {
            null => "Server query returned no server information.",
            OperationCanceledException => "Rust server query timed out.",
            _ => failure.Message
        };
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
