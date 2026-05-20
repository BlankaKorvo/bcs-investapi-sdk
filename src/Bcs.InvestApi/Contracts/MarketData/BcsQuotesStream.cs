namespace Bcs.InvestApi.Contracts.MarketData;

/// <summary>
/// Subscribe/unsubscribe message sent over the market-data WebSocket
/// (<see cref="WebSockets.BcsWebSocketEndpoints.MarketData"/>) to receive quote updates per instrument.
/// To unsubscribe, resend the same payload with <see cref="SubscribeType"/> = <see cref="BcsMarketDataSubscribeType.Unsubscribe"/>.
/// </summary>
public sealed record BcsQuotesSubscribeRequest
{
    public BcsMarketDataSubscribeType SubscribeType { get; init; } = BcsMarketDataSubscribeType.Subscribe;
    public BcsMarketDataType DataType { get; init; } = BcsMarketDataType.Quotes;

    public IReadOnlyList<BcsMarketDataInstrument> Instruments { get; init; } =
        Array.Empty<BcsMarketDataInstrument>();
}

/// <summary>
/// Message received from the market-data quotes stream. The same shape carries quote data
/// (<c>Quotes</c>) and errors; inspect <see cref="ResponseType"/> and <see cref="Errors"/> to discriminate.
/// </summary>
public sealed record BcsQuotesMessage
{
    /// <summary><c>Quotes</c> for quote data or errors.</summary>
    public string? ResponseType { get; init; }

    public string? Ticker { get; init; }
    public string? ClassCode { get; init; }

    /// <summary>Quote object kind reported by the API (e.g. <c>refresh</c>).</summary>
    public string? Type { get; init; }

    public DateTimeOffset? DateTime { get; init; }

    /// <summary>
    /// Trading status: 2 — suspended, 17 — open, 18 — closed, 100 — closing, 101 — opening,
    /// 102 — auction, 103 — closing auction, 104 — discrete auction.
    /// </summary>
    public int? SecurityTradingStatus { get; init; }

    public string? Currency { get; init; }

    /// <summary>Last trade price.</summary>
    public decimal? Last { get; init; }

    /// <summary>Best bid price.</summary>
    public decimal? Bid { get; init; }

    /// <summary>Best offer (ask) price.</summary>
    public decimal? Offer { get; init; }

    public decimal? Open { get; init; }
    public decimal? Close { get; init; }
    public decimal? High { get; init; }
    public decimal? Low { get; init; }

    /// <summary>Theoretical price (for options).</summary>
    public decimal? TheoreticalPrice { get; init; }

    /// <summary>Bid yield (for bonds).</summary>
    public decimal? BidYield { get; init; }

    /// <summary>Offer yield (for bonds).</summary>
    public decimal? OfferYield { get; init; }

    /// <summary>Price change for the current session, in price currency.</summary>
    public decimal? Change { get; init; }

    /// <summary>Price change for the current session, in percent.</summary>
    public decimal? ChangeRate { get; init; }

    public IReadOnlyList<BcsMarketDataError> Errors { get; init; } =
        Array.Empty<BcsMarketDataError>();
}
