namespace RustIQ.Domain.Servers;

public sealed class ServerSnapshot
{
    public Guid Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTimeOffset CheckedAtUtc { get; set; }

    public int Players { get; set; }

    public int MaxPlayers { get; set; }

    public bool IsOnline { get; set; }

    public long? PingMilliseconds { get; set; }

    public Server? Server { get; set; }
}
