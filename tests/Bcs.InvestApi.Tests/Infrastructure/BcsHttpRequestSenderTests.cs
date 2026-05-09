namespace Bcs.InvestApi.Tests.Infrastructure;

using System.Net;
using Bcs.InvestApi.Infrastructure;
using Xunit;

public sealed class BcsHttpRequestSenderTests
{
    [Fact]
    public async Task SendAsync_ReturnsFirstResponse()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var sender = new BcsHttpRequestSender();

        using var exchange = await sender.SendAsync(
            new HttpClient(handler),
            () => new HttpRequestMessage(HttpMethod.Get, "https://example.test/limits"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, exchange.Response.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task SendAsync_WhenHttpClientThrows_DisposesRequest()
    {
        var content = new TrackingContent();
        var handler = new CapturingHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Connection reset."));
        var sender = new BcsHttpRequestSender();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendAsync(
                new HttpClient(handler),
                () =>
                {
                    return new HttpRequestMessage(HttpMethod.Post, "https://example.test/orders")
                    {
                        Content = content,
                    };
                },
                CancellationToken.None));

        Assert.Contains("Connection reset", exception.Message);
        Assert.Equal(1, handler.RequestCount);
        Assert.True(content.IsDisposed);
    }

    private sealed class TrackingContent : HttpContent
    {
        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
