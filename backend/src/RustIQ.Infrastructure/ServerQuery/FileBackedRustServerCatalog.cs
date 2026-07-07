using RustIQ.Domain.Servers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;

namespace RustIQ.Infrastructure.ServerQuery;

public sealed class FileBackedRustServerCatalog(
    IOptions<RustServerQueryOptions> queryOptions,
    IOptions<RustServerCatalogImportOptions> importOptions)
    : IRustServerCatalog
{
    public async Task<IReadOnlyList<ConfiguredRustServer>> GetConfiguredServersAsync(CancellationToken cancellationToken)
    {
        var mergedServers = new List<ConfiguredRustServer>();
        mergedServers.AddRange(queryOptions.Value.Servers);

        if (importOptions.Value.Enabled)
        {
            var importedServers = await LoadImportedServersAsync(importOptions.Value.JsonFilePath, cancellationToken);
            mergedServers.AddRange(importedServers);
        }

        return mergedServers
            .GroupBy(server => $"{server.Host.Trim().ToLowerInvariant()}:{server.QueryPort}")
            .Select(group => group.First())
            .ToArray();
    }

    private static async Task<IReadOnlyList<ConfiguredRustServer>> LoadImportedServersAsync(
        string? jsonFilePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jsonFilePath))
        {
            throw new InvalidOperationException("Rust server catalog import is enabled, but JsonFilePath is missing.");
        }

        var absolutePath = Path.GetFullPath(jsonFilePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException(
                $"Rust server catalog import file was not found: {absolutePath}",
                absolutePath);
        }

        await using var stream = File.OpenRead(absolutePath);
        var payload = await JsonSerializer.DeserializeAsync<ConfiguredRustServerCatalogFile>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken: cancellationToken);

        if (payload?.Servers is null)
        {
            return [];
        }

        return payload.Servers
            .Where(server => !string.IsNullOrWhiteSpace(server.Host) && server.QueryPort is > 0 and <= 65535)
            .Select(server => new ConfiguredRustServer
            {
                BattleMetricsServerId = server.BattleMetricsServerId,
                Name = server.Name,
                Address = server.Address,
                Host = server.Host.Trim(),
                GamePort = server.GamePort,
                QueryPort = server.QueryPort,
                Country = server.Country,
                DiscoverySource = server.DiscoverySource ?? ServerDiscoverySource.Manual,
                SummarySource = server.SummarySource ?? ServerSummarySource.A2S,
                PlayerCountSource = server.PlayerCountSource ?? ServerPlayerCountSource.BattleMetrics,
                PlayerListSource = server.PlayerListSource ?? PlayerListSource.A2S
            })
            .ToArray();
    }

    private sealed class ConfiguredRustServerCatalogFile
    {
        public List<ConfiguredRustServerFileEntry> Servers { get; init; } = [];
    }

    private sealed class ConfiguredRustServerFileEntry
    {
        public string? Name { get; init; }

        public long? BattleMetricsServerId { get; init; }

        public string? Address { get; init; }

        public string Host { get; init; } = string.Empty;

        public int? GamePort { get; init; }

        public int QueryPort { get; init; }

        public string? Country { get; init; }

        public ServerDiscoverySource? DiscoverySource { get; init; }

        public ServerSummarySource? SummarySource { get; init; }

        public ServerPlayerCountSource? PlayerCountSource { get; init; }

        public PlayerListSource? PlayerListSource { get; init; }
    }
}
