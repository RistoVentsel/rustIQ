namespace RustIQ.Domain.Servers;

public sealed class ServerRanking
{
    public Guid Id { get; set; }

    public Guid ServerId { get; set; }

    public int Rank { get; set; }

    public double AveragePlayers { get; set; }

    public int PeakPlayers { get; set; }

    public int SnapshotCount { get; set; }

    public DateTimeOffset WindowStartUtc { get; set; }

    public DateTimeOffset WindowEndUtc { get; set; }

    public DateTimeOffset GeneratedAtUtc { get; set; }

    public Server? Server { get; set; }
}
