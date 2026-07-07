namespace RustIQ.Api.Workers;

public interface IServerRefreshJobQueue
{
    bool TryQueueRefreshAll();

    ServerRefreshJobStatus GetStatus();
}
