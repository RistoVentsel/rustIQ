using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

/// <summary>
/// One capped slice of servers to refresh map metadata for, plus where the rotating part of the
/// selection stopped so the next run can carry on from there instead of re-reading the same slice.
/// </summary>
public sealed record MapMetadataRefreshBatch(
    IReadOnlyList<Server> Servers,
    Guid? NextRotationCursor);
