using Microsoft.AspNetCore.SignalR;

namespace RustIQ.Api.Realtime;

public sealed class ServerUpdatesHub(IWatchedServerRegistry watchedServerRegistry) : Hub
{
    public async Task WatchServer(Guid serverId)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetServerGroupName(serverId),
            Context.ConnectionAborted);

        watchedServerRegistry.Watch(Context.ConnectionId, serverId);
    }

    public async Task UnwatchServer(Guid serverId)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetServerGroupName(serverId),
            Context.ConnectionAborted);

        watchedServerRegistry.Unwatch(Context.ConnectionId, serverId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        watchedServerRegistry.UnwatchAll(Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }

    public static string GetServerGroupName(Guid serverId)
    {
        return $"server:{serverId}";
    }
}
