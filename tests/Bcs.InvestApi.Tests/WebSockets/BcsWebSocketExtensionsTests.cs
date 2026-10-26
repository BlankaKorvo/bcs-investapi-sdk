namespace Bcs.InvestApi.Tests.WebSockets;

using System.Net.WebSockets;
using System.Text;
using Bcs.InvestApi.Contracts.Exceptions;
using Bcs.InvestApi.Contracts.Limits;
using Bcs.InvestApi.WebSockets;
using Xunit;

public sealed class BcsWebSocketExtensionsTests
{
    [Fact]
    public async Task ReceiveJsonMessageAsync_SingleFrame_DeserializesPayload()
    {
        const string payload = """
        {
          "depoLimit": [
            {
              "ticker": "SBER",
              "classCode": "TQBR",
              "exchange": "MOEX",
              "averagePrice": 300.526666,
              "quantity": { "type": "T365", "value": 2 },
              "quantityBatch": { "type": "T365", "value": 2 },
              "instrumentType": "STOCK",
              "loadDate": "2025-11-13T21:00:00.000Z",
              "lockedBuyValue": 0,
              "lockedSellValue": 0,
              "lockedBuyQuantity": 0,
              "lockedSellQuantity": 0
            }
          ],
          "futureHolding": [],
          "moneyLimits": [],
          "futuresLimits": []
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(payload);
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Frame.Data(bytes, WebSocketMessageType.Text, endOfMessage: true),
        });

        var message = await socket.ReceiveJsonMessageAsync<BcsLimitsResponse>(TestContext.Current.CancellationToken);

        Assert.NotNull(message);
        var depo = Assert.Single(message!.DepoLimit);
        Assert.Equal("SBER", depo.Ticker);
        Assert.Equal("STOCK", depo.InstrumentType);
        Assert.Equal(300.526666m, depo.AveragePrice);
        Assert.NotNull(depo.Quantity);
        Assert.Equal("T365", depo.Quantity!.Type);
        Assert.Equal(2m, depo.Quantity.Value);
    }

    [Fact]
    public async Task ReceiveJsonMessageAsync_MultipleFrames_ConcatenatesAndDeserializes()
    {
        const string payload = """{"depoLimit":[],"futureHolding":[],"moneyLimits":[],"futuresLimits":[]}""";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var half = bytes.Length / 2;
        var firstFrame = bytes.AsSpan(0, half).ToArray();
        var secondFrame = bytes.AsSpan(half).ToArray();

        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Frame.Data(firstFrame, WebSocketMessageType.Text, endOfMessage: false),
            FakeWebSocket.Frame.Data(secondFrame, WebSocketMessageType.Text, endOfMessage: true),
        });

        var message = await socket.ReceiveJsonMessageAsync<BcsLimitsResponse>(TestContext.Current.CancellationToken);

        Assert.NotNull(message);
        Assert.Empty(message!.DepoLimit);
    }

    [Fact]
    public async Task ReceiveJsonMessageAsync_GrowsBufferForLargePayload()
    {
        // Tens of KB of ignored content guarantees the receive buffer must grow past its initial 8KB rent.
        var tickers = string.Join(',', Enumerable.Range(0, 8000).Select(i => $"\"T{i}\""));
        var payload = "{\"depoLimit\":[],\"futureHolding\":[],\"moneyLimits\":[],\"futuresLimits\":[],\"extraDebug\":[" + tickers + "]}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        Assert.True(bytes.Length > 32 * 1024);

        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Frame.Data(bytes, WebSocketMessageType.Text, endOfMessage: true),
        });

        var message = await socket.ReceiveJsonMessageAsync<BcsLimitsResponse>(TestContext.Current.CancellationToken);

        Assert.NotNull(message);
        Assert.Empty(message!.DepoLimit);
    }

    [Fact]
    public async Task ReceiveJsonMessageAsync_CleanCloseBeforeAnyData_ReturnsDefault()
    {
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Frame.Close(),
        });

        var message = await socket.ReceiveJsonMessageAsync<BcsLimitsResponse>(TestContext.Current.CancellationToken);

        Assert.Null(message);
    }

    [Fact]
    public async Task ReceiveJsonMessageAsync_CloseMidMessage_Throws()
    {
        var payload = Encoding.UTF8.GetBytes("""{"depoLimit":[]""");
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Frame.Data(payload, WebSocketMessageType.Text, endOfMessage: false),
            FakeWebSocket.Frame.Close(),
        });

        await Assert.ThrowsAsync<WebSocketException>(() =>
            socket.ReceiveJsonMessageAsync<BcsLimitsResponse>(TestContext.Current.CancellationToken));
    }

    // A message that cannot be read shows its text: the first unexpected form of a stream is seen at once (BUG-63).
    [Fact]
    public async Task ReceiveJsonMessageAsync_MessageThatCannotBeRead_ThrowsWithItsText()
    {
        const string payload = """{"depoLimit":{"ticker":"SBER"}}""";
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Frame.Data(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, endOfMessage: true),
        });

        var exception = await Assert.ThrowsAsync<BcsResponseFormatException>(() =>
            socket.ReceiveJsonMessageAsync<BcsLimitsResponse>(TestContext.Current.CancellationToken));

        Assert.Equal("websocket", exception.Endpoint);
        Assert.Contains(payload, exception.Message);
        Assert.Equal("$.depoLimit", exception.Path);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task ReceiveJsonMessageAsync_NullWebSocket_Throws()
    {
        WebSocket? socket = null;

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            socket!.ReceiveJsonMessageAsync<BcsLimitsResponse>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendJsonMessageAsync_SerializesWithWebOptionsAsSingleTextFrame()
    {
        var socket = new FakeWebSocket(Array.Empty<FakeWebSocket.Frame>());

        await socket.SendJsonMessageAsync(new SubscribeProbe
        {
            SubscribeType = "subscribe",
            ClassCode = "TQBR",
        }, TestContext.Current.CancellationToken);

        var sent = Assert.Single(socket.SentFrames);
        Assert.Equal(WebSocketMessageType.Text, sent.MessageType);
        Assert.True(sent.EndOfMessage);

        var json = Encoding.UTF8.GetString(sent.Payload);
        Assert.Equal("""{"subscribeType":"subscribe","classCode":"TQBR"}""", json);
    }

    [Fact]
    public async Task SendJsonMessageAsync_NullWebSocket_Throws()
    {
        WebSocket? socket = null;

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            socket!.SendJsonMessageAsync(new SubscribeProbe(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReceiveJsonMessagesAsync_StreamsMessagesUntilCleanClose()
    {
        var first = Encoding.UTF8.GetBytes("""{"depoLimit":[],"futureHolding":[],"moneyLimits":[],"futuresLimits":[]}""");
        var second = Encoding.UTF8.GetBytes("""{"depoLimit":[],"futureHolding":[],"moneyLimits":[],"futuresLimits":[]}""");
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Frame.Data(first, WebSocketMessageType.Text, endOfMessage: true),
            FakeWebSocket.Frame.Data(second, WebSocketMessageType.Text, endOfMessage: true),
            FakeWebSocket.Frame.Close(),
        });

        var count = 0;
        await foreach (var message in socket.ReceiveJsonMessagesAsync<BcsLimitsResponse>(TestContext.Current.CancellationToken))
        {
            Assert.NotNull(message);
            count++;
        }

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ReceiveJsonMessagesAsync_StopsWhenCancelled()
    {
        var payload = Encoding.UTF8.GetBytes("""{"depoLimit":[],"futureHolding":[],"moneyLimits":[],"futuresLimits":[]}""");
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Frame.Data(payload, WebSocketMessageType.Text, endOfMessage: true),
            FakeWebSocket.Frame.Close(),
        });
        using var cts = new CancellationTokenSource();

        var count = 0;
        await foreach (var message in socket.ReceiveJsonMessagesAsync<BcsLimitsResponse>(cts.Token))
        {
            Assert.NotNull(message);
            count++;
            cts.Cancel();
        }

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ReceiveJsonMessagesAsync_NullWebSocket_Throws()
    {
        WebSocket? socket = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in socket!.ReceiveJsonMessagesAsync<BcsLimitsResponse>(TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Fact]
    public async Task ReceiveJsonMessagesAsync_CloseBeforeAnyData_YieldsNothing()
    {
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Frame.Close(),
        });

        var received = new List<BcsLimitsResponse>();
        await foreach (var message in socket.ReceiveJsonMessagesAsync<BcsLimitsResponse>(TestContext.Current.CancellationToken))
        {
            received.Add(message);
        }

        Assert.Empty(received);
    }

    [Fact]
    public async Task ReceiveJsonMessagesAsync_CloseMidMessage_Throws()
    {
        var payload = Encoding.UTF8.GetBytes("""{"depoLimit":[]""");
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Frame.Data(payload, WebSocketMessageType.Text, endOfMessage: false),
            FakeWebSocket.Frame.Close(),
        });

        await Assert.ThrowsAsync<WebSocketException>(async () =>
        {
            await foreach (var _ in socket.ReceiveJsonMessagesAsync<BcsLimitsResponse>(TestContext.Current.CancellationToken))
            {
            }
        });
    }

    private sealed record SubscribeProbe
    {
        public string? SubscribeType { get; init; }

        public string? ClassCode { get; init; }
    }

    private sealed class FakeWebSocket : WebSocket
    {
        private readonly Queue<Frame> _frames;
        private Frame? _current;
        private int _currentOffset;

        public FakeWebSocket(IEnumerable<Frame> frames)
        {
            _frames = new Queue<Frame>(frames);
        }

        public List<Frame> SentFrames { get; } = new();

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State { get; } = WebSocketState.Open;
        public override string? SubProtocol => null;

        public override void Abort()
        {
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override void Dispose()
        {
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (_current is null || _currentOffset == _current.Payload.Length)
            {
                if (_frames.Count == 0)
                {
                    throw new InvalidOperationException("No more frames queued for FakeWebSocket.");
                }

                _current = _frames.Dequeue();
                _currentOffset = 0;
            }

            if (_current.IsClose)
            {
                return Task.FromResult(new WebSocketReceiveResult(
                    count: 0,
                    messageType: WebSocketMessageType.Close,
                    endOfMessage: true,
                    closeStatus: WebSocketCloseStatus.NormalClosure,
                    closeStatusDescription: null));
            }

            var remaining = _current.Payload.Length - _currentOffset;
            var toCopy = Math.Min(buffer.Count, remaining);
            Buffer.BlockCopy(_current.Payload, _currentOffset, buffer.Array!, buffer.Offset, toCopy);
            _currentOffset += toCopy;
            var endOfMessage = _current.EndOfMessage && _currentOffset == _current.Payload.Length;

            return Task.FromResult(new WebSocketReceiveResult(
                count: toCopy,
                messageType: _current.MessageType,
                endOfMessage: endOfMessage));
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            SentFrames.Add(new Frame(buffer.ToArray(), messageType, endOfMessage, IsClose: false));
            return Task.CompletedTask;
        }

        internal sealed record Frame(byte[] Payload, WebSocketMessageType MessageType, bool EndOfMessage, bool IsClose)
        {
            internal static Frame Data(byte[] payload, WebSocketMessageType type, bool endOfMessage) =>
                new(payload, type, endOfMessage, IsClose: false);

            internal static Frame Close() =>
                new(Array.Empty<byte>(), WebSocketMessageType.Close, EndOfMessage: true, IsClose: true);
        }
    }
}
