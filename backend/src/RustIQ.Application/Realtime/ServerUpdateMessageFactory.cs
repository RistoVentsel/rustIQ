using RustIQ.Application.Servers;

namespace RustIQ.Application.Realtime;

public sealed class ServerUpdateMessageFactory
{
    public ServerUpdateMessage Create(ServerDto server, DateTimeOffset now)
    {
        return new ServerUpdateMessage(
            server.Id,
            server.Players,
            server.MaxPlayers,
            server.QueuePlayers,
            server.Status,
            server.LatestSnapshot.CheckedAtUtc,
            SecondsAgo(server.UpdatedAtUtc, now),
            server.MapMetadata?.MapName,
            server.MapMetadata?.Seed,
            server.MapMetadata?.Size,
            server.MapMetadata?.RustMapsUrl,
            server.MapMetadata?.ThumbnailUrl);
    }

    private static int SecondsAgo(DateTimeOffset value, DateTimeOffset now)
    {
        return Math.Max(0, (int)(now - value).TotalSeconds);
    }
}
