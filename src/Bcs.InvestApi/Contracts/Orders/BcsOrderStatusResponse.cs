namespace Bcs.InvestApi.Contracts.Orders;

using System.Text.Json;
using System.Text.Json.Serialization;
using Bcs.InvestApi.Infrastructure;

/// <summary>
/// Order status (<c>GET /orders?orderIdType=&amp;orderId=</c>); the synchronous order methods
/// (<c>POST /orders/sync</c>) answer in the same format. <see cref="Data"/> holds reports of the order, the latest last.
/// </summary>
/// <remarks>
/// <c>data</c> comes in either of two forms (<see cref="BcsOrderReportsConverter"/>). BCS sends one object — the order after
/// its latest event: on the account on 2026-10-26 reading it as a list failed at its first byte, and so the deprecated
/// <c>GET /orders/{originalClientOrderId}</c> and the order streams document it. The pages of these methods document an
/// array. Neither form promises every report from the first: the earlier executions of the order may be missing.
/// <c>data</c> is required — without it the answer does not describe the order.
/// </remarks>
public sealed record BcsOrderStatusResponse
{
    /// <summary>The documentation calls it the request's identifier; the order's UUID is <see cref="OriginalClientOrderId"/>.</summary>
    public string? ClientOrderId { get; init; }

    public string? OriginalClientOrderId { get; init; }

    [JsonRequired]
    [JsonConverter(typeof(BcsOrderReportsConverter))]
    public IReadOnlyList<BcsOrderStatusData> Data { get; init; } =
        Array.Empty<BcsOrderStatusData>();

    /// <summary>Fields of the answer this contract does not name, as sent: an answer of an unexpected form shows them.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalFields { get; init; }
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

    /// <summary>Fields of the report this contract does not name, as sent: a report of an unexpected form shows them.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalFields { get; init; }
}
