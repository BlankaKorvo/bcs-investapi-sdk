namespace Bcs.InvestApi.Tests.WebSockets;

using System.Net.WebSockets;
using Bcs.InvestApi.Tokens;
using Bcs.InvestApi.WebSockets;
using Xunit;

public sealed class BcsWebSocketConnectorTests
{
    [Fact]
    public async Task ConnectAsync_NullUri_Throws()
    {
        var connector = new BcsWebSocketConnector(
            new RecordingTokenProvider("access-token-1"),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(20));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            connector.ConnectAsync(null!));
    }

    [Fact]
    public async Task ConnectAsync_FetchesAccessTokenBeforeOpeningSocket()
    {
        var tokens = new RecordingTokenProvider("access-token-1");
        var connector = new BcsWebSocketConnector(
            tokens,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(20));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Connect will fail because the token is cancelled before the actual handshake.
        // We do not care about the exception kind here; we only verify that the connector
        // asked the token provider for a fresh token first.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            connector.ConnectAsync(new Uri("wss://example.test/ws"), cts.Token));

        Assert.Equal(1, tokens.RequestCount);
    }

    [Fact]
    public async Task ConnectAsync_RequestsFreshTokenForEachCall()
    {
        var tokens = new RecordingTokenProvider("access-token-1", "access-token-2");
        var connector = new BcsWebSocketConnector(
            tokens,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(20));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            connector.ConnectAsync(new Uri("wss://example.test/ws"), cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            connector.ConnectAsync(new Uri("wss://example.test/ws"), cts.Token));

        Assert.Equal(2, tokens.RequestCount);
    }

    [Fact]
    public void ConfigureKeepAlive_AppliesIntervalAndTimeoutToSocket()
    {
        using var socket = new ClientWebSocket();

        BcsWebSocketConnector.ConfigureKeepAlive(
            socket,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(30), socket.Options.KeepAliveInterval);
        Assert.Equal(TimeSpan.FromSeconds(20), socket.Options.KeepAliveTimeout);
    }

    private sealed class RecordingTokenProvider : IBcsAccessTokenProvider
    {
        private readonly Queue<string> _tokens;

        public RecordingTokenProvider(params string[] tokens)
        {
            _tokens = new Queue<string>(tokens);
        }

        public int RequestCount { get; private set; }

        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return ValueTask.FromResult(_tokens.Count > 0 ? _tokens.Dequeue() : "fallback");
        }

        public void InvalidateAccessToken(string rejectedAccessToken)
        {
        }
    }
}
