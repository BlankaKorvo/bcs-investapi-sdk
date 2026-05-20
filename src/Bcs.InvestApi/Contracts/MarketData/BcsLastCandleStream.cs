namespace Bcs.InvestApi.Contracts.MarketData;

/// <summary>
/// Subscribe/unsubscribe message sent over the market-data WebSocket
/// (<see cref="WebSockets.BcsWebSocketEndpoints.MarketData"/>) to receive the
/// latest candle per instrument and time frame.
/// To unsubscribe, resend the same payload with <see cref="SubscribeType"/> = <see cref="BcsMarketDataSubscribeType.Unsubscribe"/>.
/// </summary>
public sealed record BcsMarketDataSubscribeRequest
{
    public BcsMarketDataSubscribeType SubscribeType { get; init; } = BcsMarketDataSubscribeType.Subscribe;
    public BcsMarketDataType DataType { get; init; } = BcsMarketDataType.Candles;

    /// <summary>Candle time frame: <c>M1</c>, <c>M5</c>, <c>M15</c>, <c>M30</c>, <c>H1</c>, <c>H4</c>, <c>D</c>, <c>W</c>, <c>MN</c>.</summary>
    public string? TimeFrame { get; init; }

    public IReadOnlyList<BcsMarketDataInstrument> Instruments { get; init; } =
        Array.Empty<BcsMarketDataInstrument>();
}

public sealed record BcsMarketDataInstrument
{
    public string? ClassCode { get; init; }
    public string? Ticker { get; init; }
}

/// <summary>Message type of a market-data subscription request.</summary>
public enum BcsMarketDataSubscribeType
{
    Subscribe = 0,
    Unsubscribe = 1,
}

/// <summary>Kind of market data requested.</summary>
public enum BcsMarketDataType
{
    OrderBook = 0,
    Candles = 1,
    AnonymousTrades = 2,
    Quotes = 3,
}

/// <summary>
/// Message received from the market-data WebSocket. The same shape carries the
/// subscription acknowledgement (<c>CandleStickSuccess</c>), candle data (<c>CandleStick</c>),
/// and errors; inspect <see cref="ResponseType"/> and <see cref="Errors"/> to discriminate.
/// </summary>
public sealed record BcsLastCandleMessage
{
    /// <summary><c>CandleStickSuccess</c> for a subscription ack, <c>CandleStick</c> for candle data or errors.</summary>
    public string? ResponseType { get; init; }

    public BcsMarketDataSubscribeType? SubscribeType { get; init; }
    public string? Ticker { get; init; }
    public string? ClassCode { get; init; }
    public string? TimeFrame { get; init; }

    public decimal? Open { get; init; }
    public decimal? Close { get; init; }
    public decimal? High { get; init; }
    public decimal? Low { get; init; }
    public decimal? Volume { get; init; }

    public DateTimeOffset? DateTime { get; init; }

    public IReadOnlyList<BcsMarketDataError> Errors { get; init; } =
        Array.Empty<BcsMarketDataError>();
}

public sealed record BcsMarketDataError
{
    public string? Message { get; init; }

    /// <summary><c>NO_DATE</c>, <c>NOT_FOUND</c>, <c>INCORRECT_JSON</c>, <c>BAD_REQUEST</c>, <c>UNAUTHORIZED</c>.</summary>
    public string? Code { get; init; }
}
