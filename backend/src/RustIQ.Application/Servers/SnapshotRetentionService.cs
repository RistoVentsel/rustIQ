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

    /// <summary>
    /// Drops endpoints that have been known for longer than the retention window without RustIQ ever
    /// reaching them. Discovery will re-add any that Steam still lists, so this settles on removing
    /// endpoints that have genuinely gone away rather than ones that merely refuse queries.
    /// </summary>
    public Task<int> PruneUnreachableServersAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return Task.FromResult(0);
        }

        var createdBeforeUtc = DateTimeOffset.UtcNow.AddDays(-options.UnreachableServerRetentionDays);

        return serverRepository.DeleteUnreachableServersOlderThanAsync(
            createdBeforeUtc,
            Math.Max(1, options.MaxUnreachableServerDeletesPerRun),
            cancellationToken);
    }

    /// <summary>
    /// Drops servers that were reachable once but have since gone quiet: no successful query and no
    /// discovery listing for the whole retention window. If one comes back, discovery adds it again as
    /// a new server, so the cost of a wrong guess is the lost history, not a missing server.
    /// </summary>
    public Task<int> PruneOfflineServersAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return Task.FromResult(0);
        }

        var cutoffUtc = DateTimeOffset.UtcNow.AddDays(-options.OfflineServerRetentionDays);

        return serverRepository.DeleteOfflineServersOlderThanAsync(
            cutoffUtc,
            Math.Max(1, options.MaxUnreachableServerDeletesPerRun),
            cancellationToken);
    }
}
