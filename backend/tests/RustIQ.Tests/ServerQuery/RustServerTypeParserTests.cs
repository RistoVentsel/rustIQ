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

    private const string OxideVanillaKeywords =
        "mp150,cp31,ptrak,qp0,$r?,v2633,^w,^v,EU,^t,born1789045019,gmrust,cs163794,ts16,^o,rustmaps";

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

    [Fact]
    public void GetServerType_KeepsAVanillaServerOfficialWhenItRunsAPluginFramework()
    {
        // "^o" is Oxide and "^y" is Carbon. Both only say a plugin framework is installed, which
        // plenty of vanilla servers run, so neither downgrades a server that tagged itself vanilla.
        Assert.Equal(
            RustServerType.Official,
            RustServerTypeParser.GetServerType("Rustopia.gg - EU Large", OxideVanillaKeywords));
    }

    [Fact]
    public void GetServerType_ReadsTheSpelledOutVanillaTag()
    {
        Assert.Equal(
            RustServerType.Official,
            RustServerTypeParser.GetServerType("Some Server", "mp200,cp10,monthly,vanilla,gmrust"));
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
