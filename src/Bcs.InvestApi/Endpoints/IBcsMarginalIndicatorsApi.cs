namespace Bcs.InvestApi;

using Bcs.InvestApi.Contracts.MarginalIndicators;

/// <summary>Marginal indicators endpoint of the BCS Trade API.</summary>
public interface IBcsMarginalIndicatorsApi
{
    Task<IReadOnlyList<BcsInstrumentDiscount>> GetInstrumentsDiscountsAsync(
        CancellationToken cancellationToken = default);
}
