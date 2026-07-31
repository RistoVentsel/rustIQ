using System.Diagnostics;
using A2S;
using A2S.Structs;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;
using A2SServer = A2S.Server;

namespace RustIQ.Infrastructure.ServerQuery;

public sealed class SteamRustServerQueryClient(
    IOptions<RustServerQueryOptions> options)
    : IRustServerQueryClient
{
    private readonly RustServerQueryOptions _options = options.Value;

    public async Task<RustServerQueryResult> QueryServerAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        return await QueryServerViaA2SAsync(configuredServer, cancellationToken);
    }

    public async Task<IReadOnlyList<RustServerPlayerResult>> QueryPlayersAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMilliseconds(_options.TimeoutMilliseconds);

        try
        {
            var result = await RunWithTimeoutAsync(
                () => Players.Query(configuredServer.Host, configuredServer.QueryPort, _options.TimeoutMilliseconds),
                timeout,
                cancellationToken);

            if (result is not IEnumerable<Player> players)
            {
                return [];
            }

            return players
                .Select(player => new RustServerPlayerResult(
                    NullIfWhiteSpace(player.Name),
                    player.Duration))
                .ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }

    private async Task<RustServerQueryResult> QueryServerViaA2SAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMilliseconds(_options.TimeoutMilliseconds);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await RunWithTimeoutAsync(
                () => A2SServer.Query(configuredServer.Host, configuredServer.QueryPort, _options.TimeoutMilliseconds),
                timeout,
                cancellationToken);

            stopwatch.Stop();
            var checkedAtUtc = DateTimeOffset.UtcNow;

            if (result is not ServerInfo serverInfo)
            {
                return OfflineResult(
                    configuredServer,
                    checkedAtUtc,
                    stopwatch.ElapsedMilliseconds,
                    "Server query returned no server information.");
            }

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
                stopwatch.ElapsedMilliseconds,
                checkedAtUtc,
                ServerPlayerCountSource.A2S,
                ServerSummarySource.A2S,
                Error: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            return OfflineResult(
                configuredServer,
                DateTimeOffset.UtcNow,
                stopwatch.ElapsedMilliseconds,
                exception.Message);
        }
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
            error);
    }

    private static async Task<object?> RunWithTimeoutAsync(
        Func<object?> query,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var queryTask = Task.Run(query, CancellationToken.None);
        var timeoutTask = Task.Delay(timeout, cancellationToken);

        var completedTask = await Task.WhenAny(queryTask, timeoutTask);
        if (completedTask == timeoutTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Rust server query timed out.");
        }

        return await queryTask;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
