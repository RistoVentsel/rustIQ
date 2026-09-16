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
/// Only the caret spelling promotes. Rust writes the caret tags itself; the spelled out "vanilla"
/// is typed into sv_tags by hand, and in the live public list it is typed almost exclusively by
/// servers impersonating somebody else. Of the 81 servers spelling it out, 72 are listings copying
/// the names of Rustafied, Rustoria, Rusty Moose, Atlas, Bestrust and RustForNoobs.com from a
/// handful of Google Cloud addresses, answering with no caret tag at all and a made up player count,
/// and the other 9 are the MIRAGE RUST 2x servers, which carry the modded tag anyway. Not one server
/// in the list spells the word out next to the "^v" the game writes, so nothing honest is lost.
///
/// The vanilla tag is then only taken at face value from a server running no plugins. Nothing stops
/// an admin writing "^v" over a server that plays nothing like vanilla, and the ones that do give
/// themselves away elsewhere in the keyword string:
///
///   - by advertising Oxide ("^o") or Carbon ("^y") alongside it, the way "666 RUST /ДЛЯ НОВИЧКОВ
///     /РОССИЯ" sends "^m,^v,...,^o" while its own sibling servers send "^o^z";
///   - by publishing a "jp" join queue count, which Rust does not write. None of the 125 Facepunch
///     servers in the list sends one, and of the 158 servers that do, 156 also declare Oxide or
///     Carbon. The two that do not are Magic Rust's "Классика x1" pair, whose three dozen sibling
///     servers all send "^o^z".
///
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

        var isVanilla = RustServerKeywordParser.HasBrowserTag(
            keywords,
            RustServerKeywordParser.VanillaBrowserTag);

        var runsPlugins =
            HasTag(keywords, RustServerKeywordParser.OxideBrowserTag, OxideWordTag)
            || RustServerKeywordParser.HasWordTag(keywords, UmodWordTag)
            || HasTag(keywords, RustServerKeywordParser.CarbonBrowserTag, CarbonWordTag)
            || RustServerKeywordParser.HasJoinQueueCount(keywords);

        if (isVanilla && !runsPlugins)
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
