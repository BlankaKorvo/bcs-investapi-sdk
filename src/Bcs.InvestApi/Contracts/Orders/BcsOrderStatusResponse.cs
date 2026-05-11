namespace Bcs.InvestApi.Contracts.Orders;

public sealed record BcsOrderStatusResponse
{
    public Guid? ClientOrderId { get; init; }

    public string? OriginalClientOrderId { get; init; }

    public BcsOrderStatusData? Data { get; init; }
}

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

    public string? OrderNumber { get; init; }

    public decimal? AccruedCoupon { get; init; }

    public decimal? ExecutionValue { get; init; }

    public decimal? Commission { get; init; }

    public string? SecurityExchange { get; init; }
}
