using RustIQ.Domain.Servers;
using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class RustServerTypeParserTests
{
    // Every keyword string below was read off a live Rust server, so the tag spellings are the ones
    // the game actually sends rather than the ones the wiki documents.
    private const string ModdedKeywords =
        "mp200,cp61,ptrak,qp0,$r?,v2633,^b,^o^z,NA,^t,born1789048903,gmrust,cs163794,ts16";

    private const string CommunityKeywords =
        "mp250,cp89,ptrak,qp0,$ruk1,v2633,^w,^v,EU,^t,born1789048903,gmrust,cs163794,ts16";

    private const string OxideCommunityKeywords =
        "mp150,cp31,ptrak,qp0,$r?,v2633,^w,^v,EU,^t,born1789045019,gmrust,cs163794,ts16,^o,rustmaps";

    private const string OfficialKeywords =
        "mp300,cp172,ptrak,qp0,$r?,v2633,^m,^v,^t,born1788457930,gmrust,cs163794,ts8";

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
    public void GetServerType_TreatsAPluginFrameworkOnItsOwnAsCommunity()
    {
        // "^o" is Oxide and "^y" is Carbon. Both only say a plugin framework is installed, which
        // plenty of vanilla community servers run, so neither makes a server modded.
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType("Rusticated.com - EU Main", OxideCommunityKeywords));
    }

    [Fact]
    public void GetServerType_FallsBackToCommunityForAnyOtherRustServer()
    {
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType("Rustoria.co - EU Main", CommunityKeywords));
    }

    [Theory]
    [InlineData("[EU] Facepunch 15")]
    [InlineData("[US East] Facepunch Premium 1")]
    [InlineData("[EU East] Facepunch Primitive 1")]
    [InlineData("Facepunch Staging")]
    public void GetServerType_RecognisesFacepunchsOwnServersAsOfficial(string name)
    {
        // Official is the one category Rust never advertises in its tags, so it is read off the
        // naming Facepunch uses for every server it runs.
        Assert.Equal(
            RustServerType.Official,
            RustServerTypeParser.GetServerType(name, OfficialKeywords));
    }

    [Fact]
    public void GetServerType_DoesNotPromoteACommunityServerThatMerelyMentionsFacepunch()
    {
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType("Better than Facepunch 5x", CommunityKeywords));
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
