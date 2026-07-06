namespace RustIQ.Application.ServerQuery;

public sealed class RustServerQueryOptions
{
    public const string SectionName = "RustServerQuery";

    public string Host { get; init; } = string.Empty;

    public int QueryPort { get; init; }

    public int TimeoutMilliseconds { get; init; }
}
