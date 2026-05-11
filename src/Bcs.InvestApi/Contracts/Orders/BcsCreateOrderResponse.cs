namespace Bcs.InvestApi.Contracts.Orders;

public sealed record BcsCreateOrderResponse
{
    public Guid? ClientOrderId { get; init; }

    public string? Status { get; init; }
}
