namespace Bcs.InvestApi.Infrastructure;

internal sealed class BcsHttpExchange : IDisposable
{
    public BcsHttpExchange(HttpRequestMessage request, HttpResponseMessage response)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Response = response ?? throw new ArgumentNullException(nameof(response));
    }

    public HttpRequestMessage Request { get; }

    public HttpResponseMessage Response { get; }

    public void Dispose()
    {
        Response.Dispose();
        Request.Dispose();
    }
}
