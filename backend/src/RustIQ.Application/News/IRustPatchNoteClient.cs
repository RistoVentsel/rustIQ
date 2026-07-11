namespace RustIQ.Application.News;

public interface IRustPatchNoteClient
{
    Task<IReadOnlyList<RustPatchNoteDto>> GetLatestPatchNotesAsync(
        int count,
        CancellationToken cancellationToken);
}
