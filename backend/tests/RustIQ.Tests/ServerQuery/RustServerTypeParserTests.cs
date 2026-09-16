using RustIQ.Domain.Servers;
using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class RustServerTypeParserTests
{
    // Every keyword string below was read off the live Facepunch server list, so the tag spellings
    // are the ones the game actually sends rather than the ones the wiki documents.
    private const string ModdedKeywords =
        "mp200,cp61,ptrak,qp0,$r?,v2633,^b,^o^z,NA,^t,born1789048903,gmrust,cs163794,ts16";

    // No vanilla tag and no modded tag: the server answered, but it claims nothing about how it
    // plays. Oxide on its own is not a claim either way.
    private const string CommunityKeywords =
        "mp150,cp1,ptrak,qp0,$r?,v2633,born1789347129,gmrust,cs163794,ts8,^o";

    // "Intoxicated ZA Vanilla 3 - 03 Sep - Full wipe", tagged vanilla and running no framework.
    private const string CommunityVanillaKeywords =
        "mp150,cp26,ptrak,qp0,$raf,v2633,^m,^v,born1788458331,gmrust,cs162929,ts8";

    // "[EU] Facepunch 15".
    private const string FacepunchKeywords =
        "mp300,cp214,ptrak,qp0,$r?,v2633,^m,^v,^t,born1788457930,gmrust,cs163794,ts8";

    // "[EA] Softcore 1". Facepunch's Softcore, Hardcore and Primitive servers send no vanilla tag.
    private const string FacepunchSoftcoreKeywords =
        "mp350,cp152,ptrak,qp0,$r?,v2633,^m,^t,born1788457267,gmsoftcore,cs163794,ts4";

    // What the listings on 34.185.214.166 answer with under the names of Rustafied, Rustoria, Atlas,
    // Bestrust - and "[EU] Facepunch 1". Not one caret tag, every claim spelled out by hand, none of
    // the bookkeeping the game writes, and a "cp" nowhere near the players actually on the server.
    private const string ImpersonatedKeywords =
        "mp100,cp53,ptrak,qp0,v2633,monthly,official,tutorial,vanilla,h1d09a7a0,stok,born1789530758,gmrust";

    // What answers as "[KR] Facepunch 1" from a Google Cloud address in Korea, with nobody on it.
    private const string CarbonFacepunchKeywords =
        "mp100,cp0,ptrak,qp0,$r?,v2633,^y,^m,^v,^t,born1788535091,gmrust,cs163794,ts8";

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
    public void GetServerType_ReadsTheSpelledOutModdedTag()
    {
        Assert.Equal(
            RustServerType.Modded,
            RustServerTypeParser.GetServerType("Some Server", "mp200,cp10,modded,gmrust"));
    }

    [Theory]
    [InlineData("Intoxicated ZA Vanilla 3 - 03 Sep - Full wipe", CommunityVanillaKeywords)]
    [InlineData("Rustoria.co - EU Main", "mp250,cp89,ptrak,qp0,$ruk1,v2633,^w,^v,EU,^t,born1789048903,gmrust,cs163794,ts16")]
    [InlineData("Some Server", "mp200,cp10,^v,gmrust,cs163794,ts8")]
    [InlineData("Some Server", "mp200,cp10,vanilla,gmrust,cs163794,ts8")]
    public void GetServerType_DoesNotTreatTheVanillaTagAsOfficial(string name, string keywords)
    {
        // "^v" says the server runs the game as shipped, which is a claim about how it plays and not
        // about who runs it. The Rust browser files every community server carrying it - 394 of them
        // in the live list - under Community, and reading it as Official is what put Intoxicated,
        // Rustafied, Rustoria and Rusty Moose in RustIQ's Official list.
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType(name, keywords));
    }

    [Fact]
    public void GetServerType_FallsBackToCommunityForAServerThatClaimsNothing()
    {
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType("Rustilla | Monthly | Low Decay | AU", CommunityKeywords));
    }

    [Theory]
    [InlineData("[EU] Facepunch 15", FacepunchKeywords)]
    [InlineData("[EU East] Facepunch Premium 2", FacepunchKeywords)]
    [InlineData("Facepunch Staging Procgen 4k", FacepunchKeywords)]
    [InlineData("[EU] Facepunch Softcore 1", FacepunchSoftcoreKeywords)]
    [InlineData("[EU East] Facepunch Primitive 1", FacepunchSoftcoreKeywords)]
    [InlineData("[US East] Facepunch Hardcore 1", FacepunchSoftcoreKeywords)]
    public void GetServerType_RecognisesTheServersFacepunchRuns(string name, string keywords)
    {
        // Nothing a Rust server publishes says who runs it, so the naming Facepunch uses for every
        // server it runs is all there is to go on.
        Assert.Equal(
            RustServerType.Official,
            RustServerTypeParser.GetServerType(name, keywords));
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
    [InlineData("[EU] Facepunch 1")]
    [InlineData("[EU] Facepunch Softcore 2")]
    public void GetServerType_DoesNotTakeTheFacepunchNameFromAListingTheGameDidNotWrite(string name)
    {
        // The name is copied, but the keyword string is typed rather than published: no content
        // revision, no team size and no relay code, which is what separates the whole Google Cloud
        // block these sit in from the 11,405 servers that answer for themselves.
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType(name, ImpersonatedKeywords));
    }

    [Theory]
    [InlineData(CarbonFacepunchKeywords)]
    [InlineData("mp300,cp10,^v,^o,gmrust,cs163794,ts8")]
    [InlineData("mp300,cp10,^v,oxide,gmrust,cs163794,ts8")]
    [InlineData("mp300,cp10,^v,umod,gmrust,cs163794,ts8")]
    [InlineData("mp300,cp10,jp7,^v,gmrust,cs163794,ts8")]
    public void GetServerType_DoesNotTakeTheFacepunchNameFromAServerRunningPlugins(string keywords)
    {
        // A server Facepunch runs has no framework to declare and no join queue to count, so either
        // next to the name is the name being borrowed - as it is by the Carbon server answering as
        // "[KR] Facepunch 1" from a Google Cloud address with nobody on it.
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType("[KR] Facepunch 1", keywords));
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

    [Theory]
    [InlineData("mp300,cp10,^m,gmrust,cs163794")]
    [InlineData("mp300,cp10,^m,gmrust,ts8")]
    [InlineData("mp300,cp10,$r?,^m,gmrust")]
    public void GetServerType_TakesAnyOneOfTheTokensRustWritesAsProofTheGameAnswered(string keywords)
    {
        // Three tokens rather than one because any single one of them is Facepunch's to stop sending.
        Assert.Equal(
            RustServerType.Official,
            RustServerTypeParser.GetServerType("[EU] Facepunch 15", keywords));
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
