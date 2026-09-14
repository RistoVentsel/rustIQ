using System.Text.RegularExpressions;
using RustIQ.Domain.Servers;

namespace RustIQ.Infrastructure.ServerQuery;

/// <summary>
/// Works out the Rust server browser category - Official, Community or Modded - from what a server
/// advertises about itself.
/// </summary>
/// <remarks>
/// BattleMetrics used to hand this over as details.rust_type, but discovery runs off Steam and A2S
/// now, so the type has to come out of the keyword string every Rust server publishes. Modded is a
/// browser tag, "^z", and is authoritative. Oxide ("^o") and Carbon ("^y") are not: they only say a
/// plugin framework is installed, and plenty of vanilla community servers run one.
///
/// Official is the one category Rust never advertises - the game client gets that list from
/// Facepunch. Every official server is named "[Region] Facepunch ..." or "Facepunch ...", and none
/// of them carries the modded tag, which is what this recognises.
/// </remarks>
public static class RustServerTypeParser
{
    private const string ModdedWordTag = "modded";

    // Anchored to the start of the name so that a community server merely mentioning Facepunch
    // somewhere in its hostname is not promoted to Official.
    private static readonly Regex OfficialNamePattern = new(
        @"^\s*(\[[^\]]{1,16}\]\s*)?Facepunch\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static RustServerType GetServerType(string? name, string? keywords)
    {
        var isModded = RustServerKeywordParser.HasBrowserTag(
                keywords,
                RustServerKeywordParser.ModdedBrowserTag)
            || RustServerKeywordParser.HasWordTag(keywords, ModdedWordTag);

        if (isModded)
        {
            return RustServerType.Modded;
        }

        if (name is not null && OfficialNamePattern.IsMatch(name))
        {
            return RustServerType.Official;
        }

        // Everything else that answered with Rust keywords is a community server. Only servers that
        // told us nothing at all stay Unknown.
        return RustServerKeywordParser.LooksLikeRustKeywords(keywords)
            ? RustServerType.Community
            : RustServerType.Unknown;
    }
}
