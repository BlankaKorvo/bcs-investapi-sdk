namespace Bcs.InvestApi.Tests.Infrastructure;

internal sealed class CapturingHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public CapturingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestContent { get; private set; }

    public int DisposeCount { get; private set; }

    public int RequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequest = request;
        LastRequestContent = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return await _handler(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCount++;
        }

        base.Dispose(disposing);
    }
}
