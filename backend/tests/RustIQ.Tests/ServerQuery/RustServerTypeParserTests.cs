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

    // "Rustafied.com - EU Main": tagged vanilla, running no framework.
    private const string RustafiedKeywords =
        "mp250,cp89,ptrak,qp0,$ruk1,v2633,^w,^v,EU,^t,born1789048903,gmrust,cs163794,ts16";

    // "Rustopia.gg - EU Large": the same, next to the Oxide tag. Rustopia, Rusticated,
    // Reddit.com/r/PlayRust and RustSpain all run a framework for admin tooling.
    private const string OxideOperatorKeywords =
        "mp150,cp31,ptrak,qp0,$r?,v2633,^w,^v,EU,^t,born1789045019,gmrust,cs163794,ts16,^o,rustmaps";

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
    [InlineData("EU|DK VANILLA | LOW UPKEEP | MONTHLY", CommunityVanillaKeywords)]
    [InlineData("Some Server", "mp200,cp10,^v,gmrust,cs163794,ts8")]
    [InlineData("Some Server", "mp200,cp10,vanilla,gmrust,cs163794,ts8")]
    public void GetServerType_DoesNotTreatTheVanillaTagAsOfficial(string name, string keywords)
    {
        // "^v" says the server runs the game as shipped, which is a claim about how it plays and not
        // about who runs it, and 997 servers in the live list carry it without being anybody RustIQ
        // lists as Official. Reading it as Official is what put Intoxicated in that list.
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
    [InlineData("Rustafied.com - EU Main", RustafiedKeywords)]
    [InlineData("Rustoria.co - EU Mondays", RustafiedKeywords)]
    [InlineData("Rusty Moose |US Monthly|", RustafiedKeywords)]
    [InlineData("Reddit.com/r/PlayRust - EU Trio", RustafiedKeywords)]
    [InlineData("RustSpain.com | EU Main", RustafiedKeywords)]
    [InlineData("Rustopia.gg - EU Large", OxideOperatorKeywords)]
    [InlineData("Rusticated.com - US Main", OxideOperatorKeywords)]
    [InlineData("[EU] Rustoria.co - 2x Vanilla | No BP Wipes", OxideOperatorKeywords)]
    [InlineData("[AU] Rusticated.com - Creative Sandbox | Build | Never Wipes", OxideOperatorKeywords)]
    public void GetServerType_ListsTheEstablishedOperatorsAlongsideFacepunch(string name, string keywords)
    {
        // Unlike Facepunch, several of these run Oxide or Carbon for admin tooling on servers that
        // otherwise play as shipped, so the framework check that guards the Facepunch name is not
        // applied to theirs.
        Assert.Equal(
            RustServerType.Official,
            RustServerTypeParser.GetServerType(name, keywords));
    }

    [Theory]
    [InlineData("Rustafied.com - EU Main | JUST WIPED 16.09")]
    [InlineData("Rustoria.co - EU Small")]
    [InlineData("Rusty Moose |EU Biweekly|")]
    [InlineData("RustSpain.com | EU Trio Mondays")]
    public void GetServerType_DoesNotTakeAnOperatorNameFromAListingTheGameDidNotWrite(string name)
    {
        // The whole point of the bookkeeping check: these operators are the names the Google Cloud
        // listing blocks copy, and copying the name is all those listings do.
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType(name, ImpersonatedKeywords));
    }

    [Theory]
    [InlineData("Rustoriafun.co")]
    [InlineData("OnlyRustaFied |PVE|X0.5|EVENT|NPC+|Bosses|XP|RP|NOkill|")]
    [InlineData("Steady's Rustopia")]
    [InlineData("Rusticated Rust")]
    [InlineData("Moose Knuckle Solo/Duo/Trio,  08/28")]
    [InlineData("[EU] Rustoria |x5||Events||VIP||wipe every month|")]
    public void GetServerType_DoesNotPromoteAServerThatOnlyBorrowsAnOperatorsBrand(string name)
    {
        // Matched on the domain each operator puts in front of its own servers, so the brand on its
        // own - which belongs to these people too - does not promote.
        Assert.Equal(
            RustServerType.Community,
            RustServerTypeParser.GetServerType(name, RustafiedKeywords));
    }

    [Fact]
    public void GetServerType_LeavesAnOperatorsOwnModdedServersUnderModded()
    {
        // Rustoria's 10x and Base Invaders, Rusticated's Creative and Bedwars. Somebody filtering for
        // modded servers should still find them.
        Assert.Equal(
            RustServerType.Modded,
            RustServerTypeParser.GetServerType(
                "Rustoria.co 15/9 - 10X | No BPs | Kits | Shop -> JUST WIPED",
                ModdedKeywords));
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
