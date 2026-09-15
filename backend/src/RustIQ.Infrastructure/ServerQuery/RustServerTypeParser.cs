using System.Text.RegularExpressions;
using RustIQ.Domain.Servers;

namespace RustIQ.Infrastructure.ServerQuery;

/// <summary>
/// Works out the Rust server browser category - Official, Community or Modded - from what a server
/// advertises about itself.
/// </summary>
/// <remarks>
/// BattleMetrics used to hand this over as details.rust_type, but discovery runs off Steam and A2S
/// now, so the type has to come out of the keyword string every Rust server publishes.
///
/// The categories follow what BattleMetrics reported, because that is what RustIQ has always shown:
/// the modded tag ("^z") is Modded, and the vanilla tag ("^v") - a server declaring it runs the game
/// as shipped - is Official.
///
/// The vanilla tag is only taken at face value from a server that runs no plugin framework. Nothing
/// stops an admin writing "^v" over a server that plays nothing like vanilla, and the ones that do
/// give themselves away by also advertising Oxide ("^o") or Carbon ("^y"): "666 RUST /ДЛЯ НОВИЧКОВ
/// /РОССИЯ" sends "^m,^v,...,^o" while its own sibling servers send "^o^z". A vanilla server that
/// really is vanilla has no framework to declare, so the pair is treated as the tag being false.
/// This does demote the handful of genuinely vanilla servers that run a framework for admin tooling
/// - Rustopia.gg, Rusticated.com, Reddit.com/r/PlayRust and RustSpain.com among them - to Community.
///
/// Facepunch's own servers are Official too, and they are the reason the name still matters: the
/// Softcore, Hardcore and Primitive ones send no vanilla tag at all, so nothing but the name marks
/// them. Rust publishes no official tag - the game client gets that list from Facepunch - and every
/// one of those servers is named "[Region] Facepunch ...", "Facepunch ..." or, for the single odd
/// one out, "[Region] Softcore 1".
/// </remarks>
public static class RustServerTypeParser
{
    private const string ModdedWordTag = "modded";
    private const string VanillaWordTag = "vanilla";
    private const string OxideWordTag = "oxide";
    private const string UmodWordTag = "umod";
    private const string CarbonWordTag = "carbon";

    // Anchored to the start of the name so that a community server merely mentioning Facepunch
    // somewhere in its hostname is not promoted to Official. The second alternative covers the
    // Facepunch servers that leave their own name off - "[EA] Softcore 1" is the only one today -
    // and is kept tight enough that the hundreds of community servers with "softcore" in their name
    // do not match: the whole name has to be a region tag, a game mode and a number.
    private static readonly Regex OfficialNamePattern = new(
        @"^\s*(?:(?:\[[^\]]{1,16}\]\s*)?Facepunch\b|\[[^\]]{1,16}\]\s*(?:Softcore|Hardcore|Primitive)\s*\d*\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static RustServerType GetServerType(string? name, string? keywords)
    {
        var isModded = HasTag(keywords, RustServerKeywordParser.ModdedBrowserTag, ModdedWordTag);

        if (isModded)
        {
            return RustServerType.Modded;
        }

        // Checked ahead of the tags so that the servers Facepunch runs are read off the name alone.
        if (name is not null && OfficialNamePattern.IsMatch(name))
        {
            return RustServerType.Official;
        }

        var isVanilla = HasTag(keywords, RustServerKeywordParser.VanillaBrowserTag, VanillaWordTag);

        var runsPluginFramework =
            HasTag(keywords, RustServerKeywordParser.OxideBrowserTag, OxideWordTag)
            || RustServerKeywordParser.HasWordTag(keywords, UmodWordTag)
            || HasTag(keywords, RustServerKeywordParser.CarbonBrowserTag, CarbonWordTag);

        if (isVanilla && !runsPluginFramework)
        {
            return RustServerType.Official;
        }

        // Everything else that answered with Rust keywords is a community server. Only servers that
        // told us nothing at all stay Unknown.
        return RustServerKeywordParser.LooksLikeRustKeywords(keywords)
            ? RustServerType.Community
            : RustServerType.Unknown;
    }

    /// <summary>
    /// Tells whether the keyword string carries a tag in either spelling Rust servers use for it -
    /// the caret form the game writes, for example "^o", or the spelled out form admins still put in
    /// sv_tags by hand, for example "oxide".
    /// </summary>
    private static bool HasTag(string? keywords, char browserTag, string wordTag)
    {
        return RustServerKeywordParser.HasBrowserTag(keywords, browserTag)
            || RustServerKeywordParser.HasWordTag(keywords, wordTag);
    }
}
