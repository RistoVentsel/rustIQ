namespace RustIQ.Application.ServerQuery;

public sealed class FacepunchDiscoveryOptions
{
    public const string SectionName = "FacepunchDiscovery";

    public bool Enabled { get; init; } = true;

    // The whole list arrives as one response of a few megabytes, so this is generous compared with
    // the other clients.
    public int TimeoutMilliseconds { get; init; } = 60000;

    // High enough to take the whole list, which is around 11500 servers. Lower it to cap how much
    // the refresh lanes have to poll: Facepunch returns the list sorted by player count, so a cap
    // keeps the busiest servers and drops the empty ones.
    public int MaxServersPerRefresh { get; init; } = 15000;
}
