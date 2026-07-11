using Microsoft.Extensions.Caching.Memory;
using RustIQ.Application.News;

namespace RustIQ.Api.Endpoints;

public static class PatchNoteEndpoints
{
    private const string LatestPatchNotesCacheKey = "patch-notes:latest:3";
    private static readonly TimeSpan PatchNoteCacheDuration = TimeSpan.FromMinutes(15);

    public static IEndpointRouteBuilder MapPatchNoteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var patchNotes = endpoints.MapGroup("/patch-notes")
            .WithTags("Patch Notes");

        patchNotes.MapGet("", async (
            IMemoryCache cache,
            IRustPatchNoteClient patchNoteClient,
            CancellationToken cancellationToken) =>
        {
            if (cache.TryGetValue(
                    LatestPatchNotesCacheKey,
                    out IReadOnlyList<RustPatchNoteDto>? cachedPatchNotes)
                && cachedPatchNotes is not null)
            {
                return Results.Ok(cachedPatchNotes);
            }

            var latestPatchNotes = await patchNoteClient.GetLatestPatchNotesAsync(3, cancellationToken);
            cache.Set(LatestPatchNotesCacheKey, latestPatchNotes, PatchNoteCacheDuration);

            return Results.Ok(latestPatchNotes);
        })
        .WithName("GetLatestPatchNotes");

        return endpoints;
    }
}
