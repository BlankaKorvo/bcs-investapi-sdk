namespace Bcs.InvestApi;

using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Trades;

/// <summary>Trades endpoints of the BCS Trade API.</summary>
public interface IBcsTradesApi
{
    Task<BcsTradesSearchResponse> SearchTradesAsync(
        BcsTradesSearchRequest request,
        int page,
        int size,
        IEnumerable<BcsTradeSort>? sort = null,
        CancellationToken cancellationToken = default);
}
