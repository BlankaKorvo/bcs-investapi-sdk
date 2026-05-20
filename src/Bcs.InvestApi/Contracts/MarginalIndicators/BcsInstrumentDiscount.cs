namespace Bcs.InvestApi.Contracts.MarginalIndicators;

public sealed record BcsInstrumentDiscount
{
    public string? Ticker { get; init; }

    public decimal? DiscountLong { get; init; }

    public decimal? DiscountShort { get; init; }
}
