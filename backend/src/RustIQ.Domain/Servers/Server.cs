namespace RustIQ.Domain.Servers;

public sealed class Server
{
    public Guid Id { get; set; }

    public long? BattleMetricsServerId { get; set; }

    public string? Address { get; set; }

    public string Host { get; set; } = string.Empty;

    public int? GamePort { get; set; }

    public int QueryPort { get; set; }

    public string? Country { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Name { get; set; }

    public string? Map { get; set; }

    public int Players { get; set; }

    public int MaxPlayers { get; set; }

    public int? QueuePlayers { get; set; }

    public ServerStatus Status { get; set; } = ServerStatus.Unknown;

    public ServerDiscoverySource DiscoverySource { get; set; } = ServerDiscoverySource.Unknown;

    public ServerSummarySource SummarySource { get; set; } = ServerSummarySource.Unknown;

    public ServerPlayerCountSource PlayerCountSource { get; set; } = ServerPlayerCountSource.Unknown;

    public PlayerListSource PlayerListSource { get; set; } = PlayerListSource.Unknown;

    public ServerDataQuality DataQuality { get; set; } = ServerDataQuality.Unknown;

    public string? LastValidationReason { get; set; }

    public DateTimeOffset? LastSeenAtUtc { get; set; }

    public DateTimeOffset? LastCheckedAtUtc { get; set; }

    public DateTimeOffset? LastDiscoveredAtUtc { get; set; }

    public DateTimeOffset? LastPlayerCountUpdatedAtUtc { get; set; }

    public DateTimeOffset? LastWipeAtUtc { get; set; }

    public DateTimeOffset? NextWipeAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public List<ServerSnapshot> Snapshots { get; set; } = [];

    public List<ServerPlayerCurrent> CurrentPlayers { get; set; } = [];

    public ServerMapMetadata? MapMetadata { get; set; }
}
