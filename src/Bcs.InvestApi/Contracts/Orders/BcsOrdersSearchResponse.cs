namespace Bcs.InvestApi.Contracts.Orders;

using Bcs.InvestApi.Contracts.Enums;

public sealed record BcsOrdersSearchResponse
{
    public IReadOnlyList<BcsOrder> Records { get; init; } =
        Array.Empty<BcsOrder>();

    public long? TotalRecords { get; init; }

    public int? TotalPages { get; init; }
}

public sealed record BcsOrder
{
    public long? OrderNum { get; init; }

    public string? OrderId { get; init; }

    public string? ClientCode { get; init; }

    public DateTimeOffset? ExecutionDateTime { get; init; }

    public decimal? ExecutedValue { get; init; }

    public DateTimeOffset? OrderDateTime { get; init; }

    public DateOnly? TradeDate { get; init; }

    public DateTimeOffset? UpdateDateTime { get; init; }

    public string? Ticker { get; init; }

    public string? ClassCode { get; init; }

    public decimal? TakePrice { get; init; }

    public decimal? StopPrice { get; init; }

    public decimal? Price { get; init; }

    public string? SettlementCurrency { get; init; }

    public decimal? OrderQuantity { get; init; }

    public decimal? RemainedQuantity { get; init; }

    public decimal? ExecutedQuantity { get; init; }

    public string? RejectReason { get; init; }

    public decimal? AveragePrice { get; init; }

    public decimal? CalculationVolume { get; init; }

    public decimal? ContractSum { get; init; }

    public BcsOrderStatus? OrderStatus { get; init; }

    public BcsOrderType? OrderType { get; init; }

    public BcsOrderSide? Side { get; init; }

    public decimal? OrderQuantityLots { get; init; }

    public decimal? RemainedQuantityLots { get; init; }

    public decimal? ExecutedQuantityLots { get; init; }

    public string? LinkedOrder { get; init; }

    public string? StopOrder { get; init; }

    public decimal? Visible { get; init; }

    public int? MarketTakeProfit { get; init; }

    public int? MarketStopLoss { get; init; }

    public decimal? PositionPriceStop { get; init; }

    public decimal? PositionPriceLimit { get; init; }
}
