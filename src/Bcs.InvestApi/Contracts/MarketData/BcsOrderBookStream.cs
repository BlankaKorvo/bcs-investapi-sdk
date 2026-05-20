namespace Bcs.InvestApi.Contracts.MarketData;

/// <summary>
/// Subscribe/unsubscribe message sent over the market-data WebSocket
/// (<see cref="WebSockets.BcsWebSocketEndpoints.MarketData"/>) to receive order-book
/// (depth-of-market) updates per instrument.
/// To unsubscribe, resend the same payload with <see cref="SubscribeType"/> = <see cref="BcsMarketDataSubscribeType.Unsubscribe"/>.
/// </summary>
public sealed record BcsOrderBookSubscribeRequest
{
    public BcsMarketDataSubscribeType SubscribeType { get; init; } = BcsMarketDataSubscribeType.Subscribe;
    public BcsMarketDataType DataType { get; init; } = BcsMarketDataType.OrderBook;

    /// <summary>Order-book depth (1–20). Defaults to 20 on the server when omitted.</summary>
    public int? Depth { get; init; }

    public IReadOnlyList<BcsMarketDataInstrument> Instruments { get; init; } =
        Array.Empty<BcsMarketDataInstrument>();
}

/// <summary>
/// Message received from the market-data order-book stream. The same shape carries the
/// subscription acknowledgement (<c>OrderBookSuccess</c>), depth data (<c>OrderBook</c>),
/// and errors; inspect <see cref="ResponseType"/> and <see cref="Errors"/> to discriminate.
/// </summary>
public sealed record BcsOrderBookMessage
{
    /// <summary><c>OrderBookSuccess</c> for a subscription ack, <c>OrderBook</c> for depth data or errors.</summary>
    public string? ResponseType { get; init; }

    public BcsMarketDataSubscribeType? SubscribeType { get; init; }
    public string? Ticker { get; init; }
    public string? ClassCode { get; init; }
    public int? Depth { get; init; }

    /// <summary>Total bid volume (in units). Sent as a string by the API; parsed via web JSON number handling.</summary>
    public decimal? BidVolume { get; init; }

    /// <summary>Total ask volume (in units). Sent as a string by the API; parsed via web JSON number handling.</summary>
    public decimal? AskVolume { get; init; }

    public IReadOnlyList<BcsOrderBookLevel> Bids { get; init; } =
        Array.Empty<BcsOrderBookLevel>();

    public IReadOnlyList<BcsOrderBookLevel> Asks { get; init; } =
        Array.Empty<BcsOrderBookLevel>();

    public DateTimeOffset? DateTime { get; init; }

    public IReadOnlyList<BcsMarketDataError> Errors { get; init; } =
        Array.Empty<BcsMarketDataError>();
}

/// <summary>A single price level in the order book.</summary>
public sealed record BcsOrderBookLevel
{
    public decimal? Price { get; init; }
    public decimal? Quantity { get; init; }
}
