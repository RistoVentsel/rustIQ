namespace RustIQ.Application.ServerQuery;

public interface IBattleMetricsPlayerCountClient
{
    Task<long?> FindServerIdAsync(
        string host,
        int queryPort,
        CancellationToken cancellationToken);

    Task<BattleMetricsPlayerCountResult?> GetPlayerCountAsync(
        long battleMetricsServerId,
        CancellationToken cancellationToken);
}
