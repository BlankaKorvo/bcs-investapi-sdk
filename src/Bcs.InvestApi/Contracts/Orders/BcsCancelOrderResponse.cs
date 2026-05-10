namespace Bcs.InvestApi.Contracts.Orders;

public sealed record BcsCancelOrderResponse
{
    public Guid? ClientOrderId { get; init; }

    public string? Status { get; init; }
}
