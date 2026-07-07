using RustIQ.Domain.Servers;

namespace RustIQ.Application.ServerQuery;

public sealed class ConfiguredRustServer
{
    public string? Name { get; init; }

    public long? BattleMetricsServerId { get; init; }

    public string? Address { get; init; }

    public string Host { get; init; } = string.Empty;

    public int? GamePort { get; init; }

    public int QueryPort { get; init; }

    public string? Country { get; init; }

    public ServerDiscoverySource DiscoverySource { get; init; } = ServerDiscoverySource.Manual;

    public ServerSummarySource SummarySource { get; init; } = ServerSummarySource.A2S;

    public ServerPlayerCountSource PlayerCountSource { get; init; } = ServerPlayerCountSource.BattleMetrics;

    public PlayerListSource PlayerListSource { get; init; } = PlayerListSource.A2S;
}
