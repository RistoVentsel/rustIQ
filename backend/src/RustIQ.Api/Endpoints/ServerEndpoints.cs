using RustIQ.Api.Workers;
using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;
using Microsoft.Extensions.Caching.Memory;

namespace RustIQ.Api.Endpoints;

public static class ServerEndpoints
{
    private static readonly TimeSpan ServerListCacheDuration = TimeSpan.FromSeconds(5);

    public static IEndpointRouteBuilder MapServerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var servers = endpoints.MapGroup("/servers")
            .WithTags("Servers");

        servers.MapGet("", async (
            string? search,
            ServerStatus? status,
            int? page,
            int? pageSize,
            string? sortBy,
            string? sortDirection,
            string? serverTypes,
            string? wipeFilter,
            string? refresh,
            IMemoryCache serverListCache,
            IServerRefreshJobQueue refreshJobQueue,
            ServerReadService serverReadService,
            CancellationToken cancellationToken) =>
        {
            var canUseCache = string.IsNullOrWhiteSpace(refresh)
                || string.Equals(refresh, ServerReadService.RefreshNoneMode, StringComparison.OrdinalIgnoreCase);
            var cacheKey = BuildServerListCacheKey(
                search,
                status,
                page,
                pageSize,
                sortBy,
                sortDirection,
                serverTypes,
                wipeFilter);

            if (canUseCache
                && serverListCache.TryGetValue(cacheKey, out PagedResult<ServerListItemDto>? cachedResult)
                && cachedResult is not null)
            {
                return Results.Ok(cachedResult);
            }

            if (string.Equals(refresh, ServerReadService.RefreshAllMode, StringComparison.OrdinalIgnoreCase))
            {
                refreshJobQueue.TryQueueRefreshAll();
                refresh = ServerReadService.RefreshNoneMode;
            }

            var result = await serverReadService.GetServersAsync(
                search,
                status,
                page,
                pageSize,
                sortBy,
                sortDirection,
                serverTypes,
                refresh,
                cancellationToken,
                wipeFilter);

            if (canUseCache)
            {
                serverListCache.Set(cacheKey, result, ServerListCacheDuration);
            }

            return Results.Ok(result);
        })
        .WithName("GetServers");

        servers.MapPost("/refresh", (
            IServerRefreshJobQueue refreshJobQueue) =>
        {
            refreshJobQueue.TryQueueRefreshAll();

            return Results.Accepted("/servers/refresh", refreshJobQueue.GetStatus());
        })
        .WithName("QueueServerRefresh");

        servers.MapGet("/refresh", (
            IServerRefreshJobQueue refreshJobQueue) =>
        {
            return Results.Ok(refreshJobQueue.GetStatus());
        })
        .WithName("GetServerRefreshStatus");

        servers.MapGet("/{id:guid}", async (
            Guid id,
            string? refresh,
            ServerReadService serverReadService,
            CancellationToken cancellationToken) =>
        {
            var server = await serverReadService.GetServerAsync(id, refresh, cancellationToken);

            return server is null ? Results.NotFound() : Results.Ok(server);
        })
        .WithName("GetServer");

        servers.MapGet("/{id:guid}/snapshots", async (
            Guid id,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? limit,
            ServerReadService serverReadService,
            CancellationToken cancellationToken) =>
        {
            var snapshots = await serverReadService.GetSnapshotsAsync(
                id,
                from,
                to,
                limit,
                cancellationToken);

            return snapshots is null ? Results.NotFound() : Results.Ok(snapshots);
        })
        .WithName("GetServerSnapshots");

        servers.MapGet("/{id:guid}/players", async (
            Guid id,
            ServerReadService serverReadService,
            CancellationToken cancellationToken) =>
        {
            var players = await serverReadService.GetCurrentPlayersAsync(id, cancellationToken);

            return players is null ? Results.NotFound() : Results.Ok(players);
        })
        .WithName("GetServerPlayers");

        servers.MapGet("/{id:guid}/map", async (
            Guid id,
            ServerReadService serverReadService,
            CancellationToken cancellationToken) =>
        {
            var metadata = await serverReadService.GetMapMetadataAsync(id, cancellationToken);

            return metadata is null ? Results.NotFound() : Results.Ok(metadata);
        })
        .WithName("GetServerMap");

        return endpoints;
    }

    private static string BuildServerListCacheKey(
        string? search,
        ServerStatus? status,
        int? page,
        int? pageSize,
        string? sortBy,
        string? sortDirection,
        string? serverTypes,
        string? wipeFilter)
    {
        return string.Join('|',
            "servers",
            NormalizeCacheValue(search),
            status?.ToString() ?? string.Empty,
            page?.ToString() ?? string.Empty,
            pageSize?.ToString() ?? string.Empty,
            NormalizeCacheValue(sortBy),
            NormalizeCacheValue(sortDirection),
            NormalizeCacheValue(serverTypes),
            NormalizeCacheValue(wipeFilter));
    }

    private static string NormalizeCacheValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
