using RustIQ.Domain.Servers;
using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class RustServerTypeParserTests
{
    // Every keyword string below was read off a live Rust server, so the tag spellings are the ones
    // the game actually sends rather than the ones the wiki documents.
    private const string ModdedKeywords =
        "mp200,cp61,ptrak,qp0,$r?,v2633,^b,^o^z,NA,^t,born1789048903,gmrust,cs163794,ts16";

    // No vanilla tag and no modded tag: the server answered, but it claims nothing about how it
    // plays. Oxide on its own is not a claim either way.
    private const string CommunityKeywords =
        "mp150,cp1,ptrak,qp0,$r?,v2633,born1789347129,gmrust,cs163794,ts8,^o";

    private const string VanillaKeywords =
        "mp250,cp89,ptrak,qp0,$ruk1,v2633,^w,^v,EU,^t,born1789048903,gmrust,cs163794,ts16";

    // Tagged vanilla and Oxide at once. Servers really do ship this pair, and it is the pair RustIQ
    // reads as the vanilla tag being false.
    private const string OxideVanillaKeywords =
        "mp150,cp31,ptrak,qp0,$r?,v2633,^w,^v,EU,^t,born1789045019,gmrust,cs163794,ts16,^o,rustmaps";

    // The keywords "666 RUST /DLYA NOVICHKOV /ROSSIYA 15.09" was sending when it turned up in the
    // Official list. Note "^o" alone rather than the "^o^z" its sibling servers send.
    private const string LyingVanillaKeywords =
        "mp650,cp507,ptrak,qp0,$r?,v2633,^m,^v,EU,born1789462690,gmrust,cs163794,ts16,^o";

    private const string CarbonVanillaKeywords =
        "mp650,cp341,ptrak,qp0,$r?,v2633,^m,^v,^y,EU,born1789462690,gmrust,cs163794,ts16";

    // "Magic Rust - Long | Классика x1", tagged vanilla and declaring no framework, but publishing a
    // "jp" join queue count that Rust never writes.
    private const string JoinQueueVanillaKeywords =
        "mp300,cp179,ptrak,jp7,qp0,$r?,v2633,^m,^v,EU,born1788457667,gmrust,cs163794,ts150";

    // What the listings impersonating Rustafied, Rustoria, Atlas, Bestrust and RustForNoobs.com
    // answer with: not one caret tag, the vanilla claim spelled out by hand, and a "cp" nowhere near
    // the players actually on the server.
    private const string ImpersonatedVanillaKeywords =
        "mp100,cp80,ptrak,qp0,v2633,monthly,official,tutorial,vanilla,hd4c17cf0,stok,born1789505589,gmrust";

    // Facepunch's Softcore, Hardcore and Primitive servers send no vanilla tag at all.
    private const string FacepunchSoftcoreKeywords =
        "mp400,cp80,ptrak,qp0,$r?,v2633,^m,^t,born1788457305,gmsoftcore,cs163794,ts4";

    [Fact]
    public void GetServerType_ReadsTheModdedTagEvenWhenRustRunsItIntoTheOxideTag()
    {
        // Rust sends "^o^z" as a single comma-separated token. Six thousand of the eleven thousand
        // servers in the public list report their modded tag that way.
        Assert.Equal(
            RustServerType.Modded,
            RustServerTypeParser.GetServerType("Rusty Moose |US Monthly|", ModdedKeywords));
    }

    [Fact]
    public void GetServerType_TreatsTheVanillaTagAsOfficial()
    {
        // This is the grouping BattleMetrics reported as rust_type "official" and the one RustIQ has
        // always shown: a server declaring it runs the game as shipped.
        Assert.Equal(
            RustServerType.Official,
            RustServerTypeParser.GetServerType("Rustoria.co - EU Main", VanillaKeywords));
    }

    [Theory]
    [InlineData("Rustopia.gg - EU Large", OxideVanillaKeywords)]
    [InlineData("666 RUST /ДЛЯ НОВИЧКОВ /РОССИЯ 15.09", LyingVanillaKeywords)]
    [InlineData("STEEL RUST /ДЛЯ НОВИЧКОВ /КАЗАНЬ", CarbonVanillaKeywords)]
    [InlineData("Some Server", "mp200,cp10,^v,oxide,gmrust")]
    [InlineData("Some Server", "mp200,cp10,^v,umod,gmrust")]
    [InlineData("Magic Rust — Long | Классика x1 | Русский", JoinQueueVanillaKeywords)]
    public void GetServerType_DoesNotTrustTheVanillaTagFromAServerRunningPlugins(
        string name,
        string keywords)
    {
        // A server that really runs the game as shipped has no framework to declare and no join queue
        // to count, so "^v" next to either is the tag being false. "666 RUST" and Magic Rust are the
        // servers these rules were written for: both send it while their own sibling servers send
        // "^o^z".
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType(name, keywords));
    }

    [Fact]
    public void GetServerType_KeepsFacepunchOfficialEvenAlongsideAPluginTag()
    {
        // The name is the only thing marking a server Facepunch runs, so it is read before the tags.
        Assert.Equal(
            RustServerType.Official,
            RustServerTypeParser.GetServerType("[EU] Facepunch 16", OxideVanillaKeywords));
    }

    [Theory]
    [InlineData("Rustoria.co - EU Long | JUST WIPED 16.09", ImpersonatedVanillaKeywords)]
    [InlineData("Some Server", "mp200,cp10,monthly,vanilla,gmrust")]
    public void GetServerType_DoesNotPromoteOnTheSpelledOutVanillaTag(string name, string keywords)
    {
        // Rust writes "^v" itself; the word is typed into sv_tags by hand, and in the live list it is
        // typed by servers copying somebody else's name. No server sends the word next to the caret,
        // so refusing the word on its own costs nothing.
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType(name, keywords));
    }

    [Fact]
    public void GetServerType_FallsBackToCommunityForAServerThatClaimsNeither()
    {
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType("Rustilla | Monthly | Low Decay | AU", CommunityKeywords));
    }

    [Theory]
    [InlineData("[EU] Facepunch Softcore 1")]
    [InlineData("[EU East] Facepunch Primitive 1")]
    [InlineData("[US East] Facepunch Hardcore 1")]
    [InlineData("Facepunch Staging Procgen 4k")]
    public void GetServerType_RecognisesFacepunchsOwnServersThatSendNoVanillaTag(string name)
    {
        // Rust never advertises official in its tags, and these game modes are not tagged vanilla
        // either, so the naming Facepunch uses for every server it runs is all there is to go on.
        Assert.Equal(
            RustServerType.Official,
            RustServerTypeParser.GetServerType(name, FacepunchSoftcoreKeywords));
    }

    [Fact]
    public void GetServerType_RecognisesTheOfficialServerThatLeavesFacepunchOutOfItsName()
    {
        // "[EA] Softcore 1" is the one server Facepunch runs without its own name on it, and it is
        // the only name in the whole public list shaped that way.
        Assert.Equal(
            RustServerType.Official,
            RustServerTypeParser.GetServerType("[EA] Softcore 1", FacepunchSoftcoreKeywords));
    }

    [Theory]
    [InlineData("Better than Facepunch 5x")]
    [InlineData("[EU] Softcore | Solo/Duo | Monthly | No BP Wipe")]
    [InlineData("[EU/AT] Softcore - DanubeRust WIPED")]
    public void GetServerType_DoesNotPromoteACommunityServerThatOnlyLooksOfficial(string name)
    {
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType(name, CommunityKeywords));
    }

    [Fact]
    public void GetServerType_KeepsAModdedServerModdedEvenUnderAnOfficialLookingName()
    {
        // No server Facepunch runs carries the modded tag, so the tag is the stronger signal.
        Assert.Equal(
            RustServerType.Modded,
            RustServerTypeParser.GetServerType("[EU] Facepunch 15", ModdedKeywords));
    }

    [Fact]
    public void GetServerType_KeepsAModdedServerModdedEvenWhenItAlsoTagsItselfVanilla()
    {
        Assert.Equal(
            RustServerType.Modded,
            RustServerTypeParser.GetServerType("Some Server", "mp200,cp10,^v,^z,gmrust"));
    }

    [Fact]
    public void GetServerType_ReadsTheSpelledOutModdedTag()
    {
        Assert.Equal(
            RustServerType.Modded,
            RustServerTypeParser.GetServerType("Some Server", "mp200,cp10,modded,gmrust"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something,that,is,not,rust")]
    public void GetServerType_StaysUnknownWhenTheServerSaidNothingUsable(string? keywords)
    {
        Assert.Equal(
            RustServerType.Unknown,
            RustServerTypeParser.GetServerType("Some Server", keywords));
    }
}
