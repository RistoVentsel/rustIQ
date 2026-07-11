namespace RustIQ.Application.News;

public sealed record RustPatchNoteDto(
    string Title,
    string Summary,
    string Url,
    string? ImageUrl,
    DateTimeOffset PublishedAtUtc);
