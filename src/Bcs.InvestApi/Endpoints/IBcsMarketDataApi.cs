namespace Bcs.InvestApi;

using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.MarketData;

/// <summary>Market data endpoints of the BCS Trade API.</summary>
public interface IBcsMarketDataApi
{
    Task<BcsCandlesResponse> GetCandlesAsync(
        string classCode,
        string ticker,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        BcsCandleTimeFrames timeFrame,
        CancellationToken cancellationToken = default);
}
