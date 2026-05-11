namespace Bcs.InvestApi.Contracts.Orders;

public sealed record BcsUpdateOrderResponse
{
    public Guid? ClientOrderId { get; init; }

    public string? Status { get; init; }
}
