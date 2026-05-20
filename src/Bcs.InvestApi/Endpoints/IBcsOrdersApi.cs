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

    Task<BcsCreateOrderResponse> CreateOrderAsync(
        BcsCreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<BcsOrderStatusResponse> GetOrderStatusAsync(
        Guid originalClientOrderId,
        CancellationToken cancellationToken = default);

    Task<BcsCancelOrderResponse> CancelOrderAsync(
        Guid originalClientOrderId,
        Guid clientOrderId,
        CancellationToken cancellationToken = default);

    Task<BcsUpdateOrderResponse> UpdateOrderAsync(
        Guid originalClientOrderId,
        BcsUpdateOrderRequest request,
        CancellationToken cancellationToken = default);
}
