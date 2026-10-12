namespace Bcs.InvestApi;

using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Orders;

/// <summary>Orders endpoints of the BCS Trade API.</summary>
public interface IBcsOrdersApi
{
    Task<BcsOrdersSearchResponse> SearchOrdersAsync(
        BcsOrdersSearchRequest request,
        int page,
        int size,
        IEnumerable<BcsOrderSort>? sort = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an order (<c>POST /orders</c>); the answer only confirms the request was accepted.
    /// </summary>
    Task<BcsCreateOrderResponse> CreateOrderAsync(
        BcsCreateOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an order and waits for its processing (<c>POST /orders/sync</c>); the answer is the
    /// order state in the order-status format.
    /// </summary>
    Task<BcsOrderStatusResponse> CreateOrderSyncAsync(
        BcsCreateOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the order's status (<c>GET /orders?orderIdType=&amp;orderId=</c>). For
    /// <see cref="BcsOrderIdType.ClientOrderId"/> <paramref name="orderId"/> is the order's UUID; for
    /// <see cref="BcsOrderIdType.ExchangeOrderId"/> it is the exchange identifier
    /// <c>YYMMDD-ClassCode-OrderNumber</c>. A missing order is a <c>404</c>
    /// (<see cref="Contracts.Exceptions.BcsApiException"/>).
    /// </summary>
    Task<BcsOrderStatusResponse> GetOrderStatusAsync(
        BcsOrderIdType orderIdType,
        string orderId,
        CancellationToken cancellationToken = default);
}
