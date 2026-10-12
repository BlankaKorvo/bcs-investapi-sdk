namespace Bcs.InvestApi.Contracts.Orders;

/// <summary>
/// Order status (<c>GET /orders?orderIdType=&amp;orderId=</c>); the synchronous order methods
/// (<c>POST /orders/sync</c>) answer in the same format. <see cref="Data"/> holds the order's reports.
/// </summary>
public sealed record BcsOrderStatusResponse
{
    public string? ClientOrderId { get; init; }

    public string? OriginalClientOrderId { get; init; }

    public IReadOnlyList<BcsOrderStatusData> Data { get; init; } =
        Array.Empty<BcsOrderStatusData>();
}

/// <summary>
/// One order report: an element of <see cref="BcsOrderStatusResponse.Data"/> and the
/// <c>data</c> object of an order-events stream message (<see cref="BcsOrderEventMessage"/>).
/// Codes (<c>orderStatus</c>, <c>executionType</c>, <c>side</c>, <c>orderType</c>) are BCS strings as sent.
/// </summary>
public sealed record BcsOrderStatusData
{
    public string? MessageType { get; init; }

    public string? OrderStatus { get; init; }

    public string? ExecutionType { get; init; }

    public decimal? OrderQuantity { get; init; }

    public decimal? ExecutedQuantity { get; init; }

    public decimal? LastQuantity { get; init; }

    public decimal? RemainedQuantity { get; init; }

    public string? Ticker { get; init; }

    public string? ClassCode { get; init; }

    public string? Side { get; init; }

    public string? OrderType { get; init; }

    public decimal? AveragePrice { get; init; }

    public string? OrderId { get; init; }

    public string? ExecutionId { get; init; }

    public decimal? Price { get; init; }

    public string? Currency { get; init; }

    public string? ClientCode { get; init; }

    public DateTimeOffset? TransactionTime { get; init; }

    public DateOnly? TradeDate { get; init; }

    public string? OrderNumber { get; init; }

    public decimal? AccruedCoupon { get; init; }

    public decimal? ExecutionValue { get; init; }

    public decimal? Commission { get; init; }

    public string? SecurityExchange { get; init; }

    public string? RejectReason { get; init; }

    /// <summary><c>1</c> — Day, <c>2</c> — Fill Or Kill, <c>3</c> — Good Till Date.</summary>
    public int? TimeInForce { get; init; }

    /// <summary><c>YYYYMMDD</c>; present when <see cref="TimeInForce"/> is <c>3</c>.</summary>
    public string? ExpiryDate { get; init; }

    public decimal? StopPrice { get; init; }

    public decimal? TakePrice { get; init; }

    public long? Visible { get; init; }

    public decimal? Spread { get; init; }

    /// <summary><c>0</c> — price currency, <c>1</c> — percent.</summary>
    public int? SpreadType { get; init; }

    public decimal? Offset { get; init; }

    /// <summary><c>0</c> — price currency, <c>1</c> — percent.</summary>
    public int? OffsetType { get; init; }
}
