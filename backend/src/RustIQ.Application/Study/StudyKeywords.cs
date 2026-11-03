namespace RustIQ.Application.Study;

public static class StudyKeywords
{
    // Player counts, queue counts and the relay token change from one answer to the next, so keeping
    // them would store a new wipe signal row on almost every probe. Max players belongs here too:
    // large servers raise and lower it with demand. All counts are kept in study_probes anyway.
    private static readonly string[] CountPrefixes = ["cp", "mp", "qp", "jp"];
    private const string RelayPrefix = "$r";

    // The column size the study tables store keywords in.
    public const int MaximumLength = 2048;

    public static string? WithoutVolatileTokens(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return null;
        }

        var stableTokens = keywords
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !IsVolatile(token));

        var result = string.Join(',', stableTokens);
        if (result.Length > MaximumLength)
        {
            result = result[..MaximumLength];
        }

        return result.Length == 0 ? null : result;
    }

    private static bool IsVolatile(string token)
    {
        if (token.StartsWith(RelayPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        // Counts are a prefix followed by digits only, so a word tag that merely starts with "cp"
        // is kept.
        foreach (var prefix in CountPrefixes)
        {
            if (token.Length > prefix.Length
                && token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && !token.AsSpan(prefix.Length).ContainsAnyExcept("0123456789"))
            {
                return true;
            }
        }

        return false;
    }
}
