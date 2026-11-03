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

    // Whether the A2S query behind this snapshot was answered. IsOnline cannot tell: a failed query
    // inside the offline grace period still records the server as online with its last counts.
    // Null for snapshots that did not come from an A2S query, and for rows older than this column.
    public bool? QuerySucceeded { get; set; }

    public ServerPollSource? PollSource { get; set; }

    public Server? Server { get; set; }
}
