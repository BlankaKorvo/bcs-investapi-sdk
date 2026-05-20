namespace Bcs.InvestApi;

using Bcs.InvestApi.Contracts.Portfolio;

/// <summary>Portfolio endpoint of the BCS Trade API.</summary>
public interface IBcsPortfolioApi
{
    Task<IReadOnlyList<BcsPortfolioItem>> GetPortfolioAsync(CancellationToken cancellationToken = default);
}
