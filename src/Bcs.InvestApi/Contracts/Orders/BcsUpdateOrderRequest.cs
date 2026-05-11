namespace Bcs.InvestApi.Contracts.Orders;

public sealed record BcsUpdateOrderRequest
{
    public Guid ClientOrderId { get; init; }

    public long OrderQuantity { get; init; }

    public decimal Price { get; init; }
}
