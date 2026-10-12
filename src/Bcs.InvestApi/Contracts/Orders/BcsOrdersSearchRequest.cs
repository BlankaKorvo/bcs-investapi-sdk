namespace Bcs.InvestApi.Contracts.Orders;

using Bcs.InvestApi.Contracts.Enums;

public sealed record BcsOrdersSearchRequest
{
    public DateTimeOffset? StartDateTime { get; init; }
    public DateTimeOffset? EndDateTime { get; init; }
    public BcsOrderSide? Side { get; init; }
    public IReadOnlyList<BcsOrderStatus>? OrderStatus { get; init; }
    public IReadOnlyList<BcsSearchOrderType>? OrderTypes { get; init; }
    public IReadOnlyList<string>? Tickers { get; init; }
    public IReadOnlyList<string>? ClassCodes { get; init; }
}
