namespace RustIQ.Domain.Servers;

public sealed class ServerPlayerCurrent
{
    public Guid Id { get; set; }

    public Guid ServerId { get; set; }

    public string? PlayerName { get; set; }

    public int Score { get; set; }

    public double DurationSeconds { get; set; }

    public DateTimeOffset SeenAtUtc { get; set; }

    public Server? Server { get; set; }
}
