namespace RustIQ.Application.ServerQuery;

public sealed class RustServerQueryOptions
{
    public const string SectionName = "RustServerQuery";

    public int TimeoutMilliseconds { get; init; } = 3000;

    public int OfflineThresholdSeconds { get; init; } = 60;

    public List<ConfiguredRustServer> Servers { get; init; } = [];
}
