namespace Bcs.InvestApi.Contracts.Orders;

using Bcs.InvestApi.Contracts.Enums;

public sealed record BcsCreateOrderRequest
{
    public Guid ClientOrderId { get; init; }
    public BcsOrderSide Side { get; init; }
    public BcsOrderType OrderType { get; init; }
    public long OrderQuantity { get; init; }
    public string Ticker { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
}
