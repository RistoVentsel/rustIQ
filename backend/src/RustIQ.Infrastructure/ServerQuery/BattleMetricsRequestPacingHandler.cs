namespace RustIQ.Infrastructure.ServerQuery;

public sealed class BattleMetricsRequestPacingHandler(BattleMetricsRequestPacer requestPacer)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await requestPacer.WaitForNextRequestAsync(cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
