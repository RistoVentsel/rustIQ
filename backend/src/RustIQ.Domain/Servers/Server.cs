namespace RustIQ.Domain.Servers;

public sealed class Server
{
    public Guid Id { get; set; }

    public string Host { get; set; } = string.Empty;

    public int QueryPort { get; set; }

    public string? Name { get; set; }

    public string? Map { get; set; }

    public int Players { get; set; }

    public int MaxPlayers { get; set; }

    public ServerStatus Status { get; set; } = ServerStatus.Unknown;

    public DateTimeOffset? LastSeenAtUtc { get; set; }

    public DateTimeOffset? LastCheckedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public List<ServerSnapshot> Snapshots { get; set; } = [];

    public List<ServerPlayerCurrent> CurrentPlayers { get; set; } = [];

    public ServerMapMetadata? MapMetadata { get; set; }
}
