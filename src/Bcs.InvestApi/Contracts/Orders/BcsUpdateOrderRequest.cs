namespace Bcs.InvestApi.Contracts.Orders;

public sealed record BcsUpdateOrderRequest
{
    public Guid ClientOrderId { get; init; }

    public long OrderQuantity { get; init; }

    /// <summary>
    /// Limit price. Optional: omit (leave <c>null</c>) when the new price does not need to change.
    /// BCS validates <c>price &gt;= 1e-7</c> when present.
    /// </summary>
    public decimal? Price { get; init; }
}
