namespace RustIQ.Application.Servers;

public sealed class SnapshotRetentionService(
    IServerRepository serverRepository,
    SnapshotRetentionOptions options)
{
    public Task<int> CleanupOldSnapshotsAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return Task.FromResult(0);
        }

        var cutoffUtc = DateTimeOffset.UtcNow.AddDays(-options.RetentionDays);

        return serverRepository.DeleteSnapshotsOlderThanAsync(cutoffUtc, cancellationToken);
    }
}
