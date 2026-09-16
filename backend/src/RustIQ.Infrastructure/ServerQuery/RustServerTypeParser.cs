using System.Text.RegularExpressions;
using RustIQ.Domain.Servers;

namespace RustIQ.Infrastructure.ServerQuery;

/// <summary>
/// Works out the Rust server browser category - Official, Community or Modded - from what a server
/// advertises about itself.
/// </summary>
/// <remarks>
/// Official means a server Facepunch runs, which is what the Official tab of the Rust server browser
/// lists and what BattleMetrics reported as details.rust_type "official" before it closed its API to
/// anonymous callers. It is not a claim a server can make about itself, and nothing in this file
/// treats it as one.
///
/// There is no published flag to read. Facepunch's own list at
/// https://api.facepunch.com/api/public/serverlist - the list the Rust client seeds its browser from,
/// and the one RustIQ discovers servers through - carries hostname, map, players, tags, address and
/// location, and nothing else; the wiki's tag table at
/// https://wiki.facepunch.com/rust/server-browser-tags documents no official tag, because the caret
/// tags are written by the server and an official tag would be the first thing an impostor typed;
/// Steam's ISteamApps/GetServersAtAddress shows Facepunch's servers logging in anonymously like
/// everyone else's; and the servers answer no differently over A2S. The word "official" does turn up
/// in sv_tags on 34 servers in the live list, and 32 of them are impersonators.
///
/// So the only thing that marks a server as Facepunch's is the naming they use for every server they
/// run, and the rules below are about not taking that name at face value.
///
/// The vanilla tag is deliberately not part of this. A server tagging itself "^v" is telling the
/// browser it runs the game as shipped, which is a claim about how it plays and not about who runs
/// it: in the live list 394 servers carry it without Facepunch having anything to do with them -
/// Rustafied, Rustoria, Rusty Moose, Intoxicated and the rest - and the Rust browser files every one
/// of them under Community. Reading "^v" as Official is what put them in RustIQ's Official list.
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
        if (HasTag(keywords, RustServerKeywordParser.ModdedBrowserTag, ModdedWordTag))
        {
            return RustServerType.Modded;
        }

        if (IsFacepunchOperated(name, keywords))
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
            && OfficialNamePattern.IsMatch(name)
            && RustServerKeywordParser.HasGameWrittenBookkeeping(keywords)
            && !RunsPlugins(keywords);
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
