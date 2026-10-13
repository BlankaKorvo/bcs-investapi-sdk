namespace Bcs.InvestApi.Tests.WebSockets;

using System.Net;
using System.Net.WebSockets;
using System.Text;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Exceptions;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Bcs.InvestApi.WebSockets;
using Xunit;

public sealed class BcsWebSocketConnectorTests
{
    private static readonly Uri Endpoint = new("wss://example.test/ws");

    [Fact]
    public async Task ConnectAsync_NullUri_Throws()
    {
        var connector = new BcsWebSocketConnector(
            new RecordingTokenProvider("access-token-1"),
            new RecordingTransport());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            connector.ConnectAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConnectAsync_PassesTheFreshAccessTokenAsBearer_AndReturnsTheTransportSocket()
    {
        var tokens = new RecordingTokenProvider("access-token-1");
        var transport = new RecordingTransport();
        var connector = new BcsWebSocketConnector(tokens, transport);

        var socket = await connector.ConnectAsync(Endpoint, TestContext.Current.CancellationToken);

        Assert.Same(transport.Sockets.Single(), socket);
        var connect = Assert.Single(transport.Connects);
        Assert.Equal(Endpoint, connect.Url);
        Assert.Equal("Bearer access-token-1", connect.Authorization);
        Assert.Equal(1, tokens.RequestCount);
    }

    [Fact]
    public async Task ConnectAsync_RequestsFreshTokenForEachCall()
    {
        var tokens = new RecordingTokenProvider("access-token-1", "access-token-2");
        var transport = new RecordingTransport();
        var connector = new BcsWebSocketConnector(tokens, transport);

        await connector.ConnectAsync(Endpoint, TestContext.Current.CancellationToken);
        await connector.ConnectAsync(Endpoint, TestContext.Current.CancellationToken);

        Assert.Equal(2, tokens.RequestCount);
        Assert.Equal(
            new[] { "Bearer access-token-1", "Bearer access-token-2" },
            transport.Connects.Select(c => c.Authorization));
    }

    [Fact]
    public async Task ConnectAsync_HandshakeRefusedWith401_DropsTheAccessToken_AndRethrows()
    {
        var tokens = new RecordingTokenProvider("access-token-1");
        var transport = new RecordingTransport { Refuse = HttpStatusCode.Unauthorized };
        var connector = new BcsWebSocketConnector(tokens, transport);

        var ex = await Assert.ThrowsAsync<BcsApiException>(() =>
            connector.ConnectAsync(Endpoint, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(new[] { "access-token-1" }, tokens.Invalidated);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ConnectAsync_HandshakeRefusedWithOtherStatus_KeepsTheAccessToken(HttpStatusCode status)
    {
        var tokens = new RecordingTokenProvider("access-token-1");
        var transport = new RecordingTransport { Refuse = status };
        var connector = new BcsWebSocketConnector(tokens, transport);

        var ex = await Assert.ThrowsAsync<BcsApiException>(() =>
            connector.ConnectAsync(Endpoint, TestContext.Current.CancellationToken));

        Assert.Equal(status, ex.StatusCode);
        Assert.Empty(tokens.Invalidated);
    }

    [Fact]
    public async Task ConnectAndSubscribeAsync_NullUri_Throws()
    {
        var connector = new BcsWebSocketConnector(
            new RecordingTokenProvider("access-token-1"),
            new RecordingTransport());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            connector.ConnectAndSubscribeAsync(null!, new object(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConnectAndSubscribeAsync_NullSubscribeMessage_Throws()
    {
        var connector = new BcsWebSocketConnector(
            new RecordingTokenProvider("access-token-1"),
            new RecordingTransport());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            connector.ConnectAndSubscribeAsync<object>(Endpoint, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConnectAndSubscribeAsync_SendsTheSubscribeMessageAsTheFirstTextFrame()
    {
        var transport = new RecordingTransport();
        var connector = new BcsWebSocketConnector(new RecordingTokenProvider("access-token-1"), transport);

        await connector.ConnectAndSubscribeAsync(
            Endpoint,
            new { subscribeType = 0 },
            TestContext.Current.CancellationToken);

        var frame = Assert.Single(transport.Sockets.Single().Sent);
        Assert.Equal("""{"subscribeType":0}""", frame);
    }

    [Fact]
    public async Task ConnectAndSubscribeAsync_SendFails_DisposesTheSocket()
    {
        var transport = new RecordingTransport { FailSend = true };
        var connector = new BcsWebSocketConnector(new RecordingTokenProvider("access-token-1"), transport);

        await Assert.ThrowsAsync<WebSocketException>(() =>
            connector.ConnectAndSubscribeAsync(Endpoint, new object(), TestContext.Current.CancellationToken));

        Assert.True(transport.Sockets.Single().Disposed);
    }

    [Fact]
    public async Task ClientFactory_PassesTheWebSocketTransport_AndTheTokenFromTheAuthEndpoint()
    {
        var http = new CapturingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {"access_token":"access-from-auth","expires_in":86400,"refresh_expires_in":7776000,
                 "refresh_token":"refresh-2","token_type":"bearer"}
                """,
                Encoding.UTF8,
                "application/json"),
        }));
        var transport = new RecordingTransport();
        using var client = BcsInvestApiClientFactory.Create(
            new BcsInvestApiSettings
            {
                RefreshToken = "refresh-1",
                ClientId = BcsAuthClientIds.TradeApiRead,
                AuthUrl = new Uri("https://auth.test/token"),
            },
            http,
            webSocketTransport: transport);

        await client.WebSockets.ConnectAsync(BcsWebSocketEndpoints.OrderEvents, TestContext.Current.CancellationToken);

        var connect = Assert.Single(transport.Connects);
        Assert.Equal(BcsWebSocketEndpoints.OrderEvents, connect.Url);
        Assert.Equal("Bearer access-from-auth", connect.Authorization);
    }

    [Fact]
    public void DefaultTransport_ConfigureKeepAlive_AppliesIntervalAndTimeoutToSocket()
    {
        using var socket = new ClientWebSocket();

        BcsClientWebSocketTransport.ConfigureKeepAlive(
            socket,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(30), socket.Options.KeepAliveInterval);
        Assert.Equal(TimeSpan.FromSeconds(20), socket.Options.KeepAliveTimeout);
    }

    [Theory]
    [InlineData((HttpStatusCode)0, false)]
    [InlineData(HttpStatusCode.SwitchingProtocols, false)]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    public void DefaultTransport_HandshakeIsRefused_WhenTheServerAnsweredWithAnHttpStatusOtherThan101(
        HttpStatusCode status,
        bool refused)
        => Assert.Equal(refused, BcsClientWebSocketTransport.IsRefusedHandshake(status));

    [Fact]
    public async Task DefaultTransport_CancelledBeforeHandshake_Throws()
    {
        var transport = new BcsClientWebSocketTransport(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(20));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            transport.ConnectAsync(Endpoint, "Bearer access-token-1", cts.Token));
    }

    private sealed class RecordingTokenProvider : IBcsAccessTokenProvider
    {
        private readonly Queue<string> _tokens;

        public RecordingTokenProvider(params string[] tokens)
        {
            _tokens = new Queue<string>(tokens);
        }

        public int RequestCount { get; private set; }

        public List<string> Invalidated { get; } = new();

        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return ValueTask.FromResult(_tokens.Count > 0 ? _tokens.Dequeue() : "fallback");
        }

        public void InvalidateAccessToken(string rejectedAccessToken) => Invalidated.Add(rejectedAccessToken);
    }

    private sealed class RecordingTransport : IBcsWebSocketTransport
    {
        public HttpStatusCode? Refuse { get; init; }

        public bool FailSend { get; init; }

        public List<(Uri Url, string Authorization)> Connects { get; } = new();

        public List<OpenWebSocket> Sockets { get; } = new();

        public Task<WebSocket> ConnectAsync(Uri url, string authorization, CancellationToken cancellationToken)
        {
            Connects.Add((url, authorization));
            if (Refuse is { } status)
            {
                throw new BcsApiException(status, string.Empty, url.ToString());
            }

            var socket = new OpenWebSocket(FailSend);
            Sockets.Add(socket);
            return Task.FromResult<WebSocket>(socket);
        }
    }

    private sealed class OpenWebSocket : WebSocket
    {
        private readonly bool _failSend;

        public OpenWebSocket(bool failSend) => _failSend = failSend;

        public List<string> Sent { get; } = new();

        public bool Disposed { get; private set; }

        public override WebSocketCloseStatus? CloseStatus => null;

        public override string? CloseStatusDescription => null;

        public override WebSocketState State => WebSocketState.Open;

        public override string? SubProtocol => null;

        public override void Abort()
        {
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override void Dispose() => Disposed = true;

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (_failSend)
            {
                throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
            }

            Sent.Add(Encoding.UTF8.GetString(buffer));
            return Task.CompletedTask;
        }
    }
}
