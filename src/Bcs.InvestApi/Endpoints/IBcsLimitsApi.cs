namespace Bcs.InvestApi;

using Bcs.InvestApi.Contracts.Limits;

/// <summary>Limits endpoint of the BCS Trade API.</summary>
public interface IBcsLimitsApi
{
    Task<BcsLimitsResponse> GetLimitsAsync(CancellationToken cancellationToken = default);
}
