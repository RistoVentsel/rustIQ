namespace RustIQ.Domain.Study;

/// <summary>
/// PostgreSQL's cumulative row write counters for one table (pg_stat_user_tables). Per table, so the
/// study tables' writes can be told apart from the production ones. The counters reset only when the
/// database statistics are reset or PostgreSQL restarts.
/// </summary>
public sealed class StudyTableWriteSample
{
    public long Id { get; set; }

    public DateTimeOffset SampledAtUtc { get; set; }

    public string TableName { get; set; } = string.Empty;

    public long Inserts { get; set; }

    public long Updates { get; set; }

    public long Deletes { get; set; }
}
