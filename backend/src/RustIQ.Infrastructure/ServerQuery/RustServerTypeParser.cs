using System.Text.RegularExpressions;
using RustIQ.Domain.Servers;

namespace RustIQ.Infrastructure.ServerQuery;

/// <summary>
/// Works out the Rust server browser category - Official, Community or Modded - from what a server
/// advertises about itself.
/// </summary>
/// <remarks>
/// Official is the list of servers worth trusting to be what they say they are: the ones Facepunch
/// runs, and the established operators RustIQ shows alongside them - Rustafied, Rustoria, Rustopia,
/// Rusticated, Rusty Moose, Reddit.com/r/PlayRust and RustSpain. It is not a claim a server can make
/// about itself, and nothing in this file treats it as one.
///
/// There is no published flag to read, for Facepunch's servers or anyone else's. Facepunch's own list
/// at https://api.facepunch.com/api/public/serverlist - the list the Rust client seeds its browser
/// from, and the one RustIQ discovers servers through - carries hostname, map, players, tags, address
/// and location, and nothing else; the wiki's tag table at
/// https://wiki.facepunch.com/rust/server-browser-tags documents no official tag, because the caret
/// tags are written by the server and an official tag would be the first thing an impostor typed;
/// Steam's ISteamApps/GetServersAtAddress shows Facepunch's servers logging in anonymously like
/// everyone else's; and the servers answer no differently over A2S. The word "official" does turn up
/// in sv_tags on 34 servers in the live list, and 32 of them are impersonators.
///
/// So the only thing that marks a server as belonging to one of these operators is the name, and the
/// rules below are about not taking that name at face value. That matters most here: the listings
/// copying these names outnumber some of the real ones. Thirty-two of them sit in blocks on four
/// Google Cloud addresses, answering as Rustafied, Rustoria, Rusty Moose, RustSpain, Atlas, Bestrust
/// and - on one of those addresses - "[EU] Facepunch 1". Every one is turned away by the keyword
/// string rather than the name.
///
/// The vanilla tag is deliberately not part of this. A server tagging itself "^v" is telling the
/// browser it runs the game as shipped, which is a claim about how it plays and not about who runs
/// it: in the live list 997 servers carry it that nobody on this list has anything to do with.
/// Reading "^v" as Official is what filled RustIQ's Official list with them.
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
    private static readonly Regex FacepunchNamePattern = new(
        @"^\s*(?:(?:\[[^\]]{1,16}\]\s*)?Facepunch\b|\[[^\]]{1,16}\]\s*(?:Softcore|Hardcore|Primitive)\s*\d*\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // The operators are matched on the domain they put in front of every server they run rather than
    // on the brand, because the brand on its own belongs to other people too: "Rustoriafun.co",
    // "OnlyRustaFied", "Steady's Rustopia", "Rusticated Rust" and "TEATIME.GG RUSTORIAC" are all
    // somebody else's server. Rusty Moose is the one with no domain in its names, and it always
    // follows the brand with the pipe it separates its names with, which "Moose Knuckle Solo/Duo/Trio"
    // does not.
    //
    // Anchored the way the Facepunch pattern is, past an optional region tag, so that a server merely
    // mentioning one of these somewhere in its name is not promoted.
    private static readonly Regex TrustedOperatorNamePattern = new(
        @"^\s*(?:\[[^\]]{1,16}\]\s*)?(?:Rustafied\.com|Rustoria\.co|Rustopia\.gg|Rusticated\.com"
        + @"|RustSpain\.com|Reddit\.com/r/PlayRust|Rusty\s+Moose\s*\|)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static RustServerType GetServerType(string? name, string? keywords)
    {
        if (HasTag(keywords, RustServerKeywordParser.ModdedBrowserTag, ModdedWordTag))
        {
            return RustServerType.Modded;
        }

        if (IsFacepunchOperated(name, keywords) || IsTrustedOperator(name, keywords))
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
    /// Tells whether the server is one Facepunch runs: named the way they name theirs, answering the
    /// way the game answers, and running nothing on top of it.
    /// </summary>
    /// <remarks>
    /// The name alone is not enough. Servers copying the names of the big communities also copy
    /// "[EU] Facepunch 1" and "[EU] Facepunch Softcore 2", and a Google Cloud address in Korea
    /// answers as "[KR] Facepunch 1" while declaring Carbon. The two corroborating checks cost
    /// nothing against the real ones - all 122 genuine Facepunch servers in the live list pass both,
    /// and the 3 impostors fail one or the other - and they are checks an impostor cannot satisfy
    /// while still being what it claims: the bookkeeping is written by the game rather than the
    /// admin, and a server Facepunch runs has no framework to declare and no join queue to count.
    /// </remarks>
    private static bool IsFacepunchOperated(string? name, string? keywords)
    {
        return name is not null
            && FacepunchNamePattern.IsMatch(name)
            && RustServerKeywordParser.HasGameWrittenBookkeeping(keywords)
            && !RunsPlugins(keywords);
    }

    /// <summary>
    /// Tells whether the server belongs to one of the established operators RustIQ lists as Official
    /// alongside Facepunch's own.
    /// </summary>
    /// <remarks>
    /// Held to the same test that the name was not simply typed by somebody else, which is what turns
    /// away the 32 listings copying these names, but not to the framework test. Unlike Facepunch,
    /// several of these operators do run Oxide or Carbon on servers that otherwise play as shipped -
    /// Rustopia.gg, Rusticated.com, Reddit.com/r/PlayRust and RustSpain.com among them - and using one
    /// for admin tooling is not a reason to doubt the name.
    ///
    /// The modded tag is still read first, so the modded servers these operators also run - Rustoria's
    /// 10x, Rustoria's Base Invaders, Rusticated's Creative and Bedwars - stay under Modded, where
    /// somebody filtering for them will find them.
    /// </remarks>
    private static bool IsTrustedOperator(string? name, string? keywords)
    {
        return name is not null
            && TrustedOperatorNamePattern.IsMatch(name)
            && RustServerKeywordParser.HasGameWrittenBookkeeping(keywords);
    }

    private static bool RunsPlugins(string? keywords)
    {
        return HasTag(keywords, RustServerKeywordParser.OxideBrowserTag, OxideWordTag)
            || RustServerKeywordParser.HasWordTag(keywords, UmodWordTag)
            || HasTag(keywords, RustServerKeywordParser.CarbonBrowserTag, CarbonWordTag)
            || RustServerKeywordParser.HasJoinQueueCount(keywords);
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
