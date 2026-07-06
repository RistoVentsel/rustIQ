using RustIQ.Api.Realtime;
using RustIQ.Application.Realtime;
using RustIQ.Domain.Servers;

namespace RustIQ.Tests.Realtime;

public sealed class ServerUpdateMessageTests
{
    [Fact]
    public void ServerUpdateMessage_CarriesLiveServerUpdateShape()
    {
        var serverId = Guid.NewGuid();
        var checkedAtUtc = DateTimeOffset.Parse("2026-07-06T12:00:00Z");

        var message = new ServerUpdateMessage(
            serverId,
            Players: 42,
            MaxPlayers: 100,
            Status: ServerStatus.Online,
            CheckedAtUtc: checkedAtUtc,
            UpdatedSecondsAgo: 1,
            MapName: "Moose Maps",
            Seed: 1337,
            Size: 4500,
            RustMapsUrl: "https://rustmaps.com/map/test",
            ThumbnailUrl: "https://content.rustmaps.com/thumbnail.webp");

        Assert.Equal(serverId, message.ServerId);
        Assert.Equal(42, message.Players);
        Assert.Equal(100, message.MaxPlayers);
        Assert.Equal(ServerStatus.Online, message.Status);
        Assert.Equal(checkedAtUtc, message.CheckedAtUtc);
        Assert.Equal(1, message.UpdatedSecondsAgo);
        Assert.Equal("Moose Maps", message.MapName);
        Assert.Equal(1337, message.Seed);
        Assert.Equal(4500, message.Size);
        Assert.Equal("https://rustmaps.com/map/test", message.RustMapsUrl);
        Assert.Equal("https://content.rustmaps.com/thumbnail.webp", message.ThumbnailUrl);
    }

    [Fact]
    public void GetServerGroupName_UsesServerGroupPrefix()
    {
        var serverId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var groupName = ServerUpdatesHub.GetServerGroupName(serverId);

        Assert.Equal("server:aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", groupName);
    }
}
