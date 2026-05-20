namespace Bcs.InvestApi.WebSockets;

/// <summary>
/// Static URL constants for BCS Trade API WebSocket streams.
/// Use with <see cref="BcsWebSocketConnector.ConnectAsync"/> (via <see cref="BcsInvestApiClient.WebSockets"/>).
/// Add new entries as their docs are reviewed against the published contract.
/// </summary>
public static class BcsWebSocketEndpoints
{
    /// <summary>
    /// Streams the same payload as the HTTP <c>/limits</c> endpoint
    /// (<see cref="Contracts.Limits.BcsLimitsResponse"/>).
    /// </summary>
    public static readonly Uri Limits =
        new("wss://ws.broker.ru/trade-api-bff-limit/api/v1/limits/ws");

    /// <summary>
    /// Streams portfolio positions in real time. Each message is an array of
    /// <see cref="Contracts.Portfolio.BcsPortfolioItem"/>, matching the HTTP <c>/portfolio</c> payload.
    /// </summary>
    public static readonly Uri Portfolio =
        new("wss://ws.broker.ru/trade-api-bff-portfolio/api/v1/portfolio/ws");

    /// <summary>
    /// Market-data stream. Subscribe with a <see cref="Contracts.MarketData.BcsMarketDataSubscribeRequest"/>
    /// to receive the latest candle per instrument and time frame in real time;
    /// the server replies with <see cref="Contracts.MarketData.BcsLastCandleMessage"/> messages.
    /// </summary>
    public static readonly Uri MarketData =
        new("wss://ws.broker.ru/trade-api-market-data-connector/api/v1/market-data/ws");

    /// <summary>
    /// Streams order execution status updates in real time. Each message matches the HTTP
    /// order-status payload (<see cref="Contracts.Orders.BcsOrderStatusResponse"/>).
    /// No subscribe message is required; the server pushes updates after connect.
    /// </summary>
    public static readonly Uri OrderExecutions =
        new("wss://ws.broker.ru/trade-api-bff-operations/api/v1/orders/execution/ws");

    /// <summary>
    /// Streams order transaction status updates in real time. Each message matches the HTTP
    /// order-status payload (<see cref="Contracts.Orders.BcsOrderStatusResponse"/>).
    /// No subscribe message is required; the server pushes updates after connect.
    /// </summary>
    public static readonly Uri OrderTransactions =
        new("wss://ws.broker.ru/trade-api-bff-operations/api/v1/orders/transaction/ws");

    /// <summary>
    /// Streams a real-time margin indicators snapshot
    /// (<see cref="Contracts.MarginalIndicators.BcsMarginalIndicators"/>).
    /// No subscribe message is required; the server pushes the full object after connect.
    /// </summary>
    public static readonly Uri MarginalIndicators =
        new("wss://ws.broker.ru/trade-api-bff-marginal-indicators/api/v1/marginal-indicators/ws");
}
