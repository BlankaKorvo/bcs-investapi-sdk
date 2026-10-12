namespace Bcs.InvestApi.Contracts.Orders;

/// <summary>
/// Message of the order-events stream (<see cref="WebSockets.BcsWebSocketEndpoints.OrderEvents"/>):
/// one order report per message, pushed after connect without a subscribe request.
/// </summary>
/// <remarks>
/// Identifiers stay strings: the documented example sends <c>"clientOrderId": ""</c>.
/// </remarks>
public sealed record BcsOrderEventMessage
{
    public string? OriginalClientOrderId { get; init; }

    public string? ClientOrderId { get; init; }

    public BcsOrderStatusData? Data { get; init; }
}
