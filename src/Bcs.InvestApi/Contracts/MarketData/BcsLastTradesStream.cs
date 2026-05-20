namespace Bcs.InvestApi.Contracts.MarketData;

/// <summary>
/// Subscribe/unsubscribe message sent over the market-data WebSocket
/// (<see cref="WebSockets.BcsWebSocketEndpoints.MarketData"/>) to receive anonymous-trade
/// (time &amp; sales) updates per instrument.
/// To unsubscribe, resend the same payload with <see cref="SubscribeType"/> = <see cref="BcsMarketDataSubscribeType.Unsubscribe"/>.
/// </summary>
public sealed record BcsLastTradesSubscribeRequest
{
    public BcsMarketDataSubscribeType SubscribeType { get; init; } = BcsMarketDataSubscribeType.Subscribe;
    public BcsMarketDataType DataType { get; init; } = BcsMarketDataType.AnonymousTrades;

    public IReadOnlyList<BcsMarketDataInstrument> Instruments { get; init; } =
        Array.Empty<BcsMarketDataInstrument>();
}

/// <summary>
/// Message received from the market-data anonymous-trades stream. The same shape carries the
/// subscription acknowledgement (<c>LastTradesSuccess</c>), trade data (<c>LastTrades</c>),
/// and errors; inspect <see cref="ResponseType"/> and <see cref="Errors"/> to discriminate.
/// </summary>
public sealed record BcsLastTradeMessage
{
    /// <summary><c>LastTradesSuccess</c> for a subscription ack, <c>LastTrades</c> for trade data or errors.</summary>
    public string? ResponseType { get; init; }

    public BcsMarketDataSubscribeType? SubscribeType { get; init; }
    public string? Ticker { get; init; }
    public string? ClassCode { get; init; }

    /// <summary>Trade direction: <c>BUY</c> or <c>SELL</c>.</summary>
    public string? Side { get; init; }

    /// <summary>Trade volume.</summary>
    public decimal? Volume { get; init; }

    /// <summary>Trade price.</summary>
    public decimal? Price { get; init; }

    /// <summary>Number of securities in the trade.</summary>
    public decimal? Quantity { get; init; }

    public DateTimeOffset? DateTime { get; init; }

    public IReadOnlyList<BcsMarketDataError> Errors { get; init; } =
        Array.Empty<BcsMarketDataError>();
}
