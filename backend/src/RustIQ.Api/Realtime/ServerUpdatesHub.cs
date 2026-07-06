using Microsoft.AspNetCore.SignalR;

namespace RustIQ.Api.Realtime;

public sealed class ServerUpdatesHub : Hub
{
    public Task WatchServer(Guid serverId)
    {
        return Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetServerGroupName(serverId),
            Context.ConnectionAborted);
    }

    public Task UnwatchServer(Guid serverId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetServerGroupName(serverId),
            Context.ConnectionAborted);
    }

    public static string GetServerGroupName(Guid serverId)
    {
        return $"server:{serverId}";
    }
}
