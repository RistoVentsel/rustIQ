using RustIQ.Application.ServerQuery;

namespace RustIQ.Application.Study;

public interface IRustServerDescriptionClient
{
    /// <summary>
    /// Reads the free text description a server publishes in its A2S rules. Many servers state their
    /// wipe schedule only there ("wipes every Friday 15:00 CET"), so it is a predictor input that
    /// the keywords do not carry.
    /// </summary>
    Task<RustServerDescriptionResult> GetDescriptionAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken);
}

// Answered tells "no description" apart from "no answer at all".
public sealed record RustServerDescriptionResult(bool Answered, string? Description);
