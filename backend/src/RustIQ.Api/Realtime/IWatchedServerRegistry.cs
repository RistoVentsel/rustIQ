namespace RustIQ.Api.Realtime;

public interface IWatchedServerRegistry
{
    void Watch(string connectionId, Guid serverId);

    void Unwatch(string connectionId, Guid serverId);

    void UnwatchAll(string connectionId);

    IReadOnlyList<Guid> GetWatchedServerIds();
}
