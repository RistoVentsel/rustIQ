using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using RustIQ.Api.Realtime;
using RustIQ.Application.Realtime;
using RustIQ.Application.Servers;
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
            QueuePlayers: 6,
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
        Assert.Equal(6, message.QueuePlayers);
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

    [Fact]
    public void ServerUpdateMessageFactory_MapsDifferentServersIndependently()
    {
        var now = DateTimeOffset.Parse("2026-07-07T12:00:05Z");
        var factory = new ServerUpdateMessageFactory();
        var firstServer = new ServerDto(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            101,
            "203.0.113.10",
            "one.test",
            28017,
            28015,
            "EE",
            59.437,
            24.7536,
            now.AddDays(-3),
            now.AddDays(1),
            "One",
            "Procedural Map",
            12,
            100,
            3,
            RustServerType.Official,
            ServerStatus.Online,
            ServerDiscoverySource.Steam,
            ServerSummarySource.A2S,
            ServerPlayerCountSource.BattleMetrics,
            PlayerListSource.A2S,
            ServerDataQuality.Normal,
            null,
            now.AddSeconds(-2),
            now.AddSeconds(-1),
            now.AddMinutes(-10),
            now.AddSeconds(-3),
            now.AddHours(-1),
            now.AddSeconds(-3),
            new ServerMapMetadataDto("Map One", 111, 3500, "https://rustmaps.com/map/one", "https://content.rustmaps.com/one.webp", null, "BattleMetrics", now.AddMinutes(-5), now.AddMinutes(-4)),
            new ServerSnapshotDto(now.AddSeconds(-3), 12, 100, true, 25),
            []);
        var secondServer = new ServerDto(
            Guid.Parse("ffffffff-eeee-dddd-cccc-bbbbbbbbbbbb"),
            202,
            "203.0.113.11",
            "two.test",
            28018,
            28016,
            "US",
            39.0997,
            -94.5786,
            now.AddDays(-4),
            now.AddDays(2),
            "Two",
            "Procedural Map",
            0,
            50,
            null,
            RustServerType.Community,
            ServerStatus.Offline,
            ServerDiscoverySource.Steam,
            ServerSummarySource.A2S,
            ServerPlayerCountSource.BattleMetrics,
            PlayerListSource.A2S,
            ServerDataQuality.Degraded,
            "timeout",
            now.AddMinutes(-2),
            now.AddSeconds(-4),
            now.AddMinutes(-10),
            now.AddSeconds(-4),
            now.AddHours(-1),
            now.AddSeconds(-4),
            null,
            new ServerSnapshotDto(now.AddSeconds(-4), 0, 50, false, null),
            []);

        var firstMessage = factory.Create(firstServer, now);
        var secondMessage = factory.Create(secondServer, now);

        Assert.Equal(firstServer.Id, firstMessage.ServerId);
        Assert.Equal(12, firstMessage.Players);
        Assert.Equal(3, firstMessage.QueuePlayers);
        Assert.Equal("Map One", firstMessage.MapName);
        Assert.Equal(3, firstMessage.UpdatedSecondsAgo);

        Assert.Equal(secondServer.Id, secondMessage.ServerId);
        Assert.Equal(0, secondMessage.Players);
        Assert.Equal(ServerStatus.Offline, secondMessage.Status);
        Assert.Null(secondMessage.MapName);
        Assert.Equal(4, secondMessage.UpdatedSecondsAgo);
    }

    [Fact]
    public async Task WatchServer_AddsConnectionToExpectedGroup()
    {
        var groups = new RecordingGroupManager();
        var hub = new ServerUpdatesHub
        {
            Context = new TestHubCallerContext("connection-1"),
            Groups = groups
        };
        var serverId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        await hub.WatchServer(serverId);

        var add = Assert.Single(groups.AddCalls);
        Assert.Equal("connection-1", add.ConnectionId);
        Assert.Equal(ServerUpdatesHub.GetServerGroupName(serverId), add.GroupName);
    }

    [Fact]
    public async Task UnwatchServer_RemovesConnectionFromExpectedGroup()
    {
        var groups = new RecordingGroupManager();
        var hub = new ServerUpdatesHub
        {
            Context = new TestHubCallerContext("connection-2"),
            Groups = groups
        };
        var serverId = Guid.Parse("ffffffff-eeee-dddd-cccc-bbbbbbbbbbbb");

        await hub.UnwatchServer(serverId);

        var remove = Assert.Single(groups.RemoveCalls);
        Assert.Equal("connection-2", remove.ConnectionId);
        Assert.Equal(ServerUpdatesHub.GetServerGroupName(serverId), remove.GroupName);
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddCalls { get; } = [];

        public List<(string ConnectionId, string GroupName)> RemoveCalls { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddCalls.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            RemoveCalls.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private sealed class TestHubCallerContext(string connectionId) : HubCallerContext
    {
        private readonly IDictionary<object, object?> _items = new Dictionary<object, object?>();

        public override string ConnectionId { get; } = connectionId;

        public override string? UserIdentifier => null;

        public override ClaimsPrincipal? User => null;

        public override IDictionary<object, object?> Items => _items;

        public override IFeatureCollection Features => new FeatureCollection();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort()
        {
        }
    }
}
