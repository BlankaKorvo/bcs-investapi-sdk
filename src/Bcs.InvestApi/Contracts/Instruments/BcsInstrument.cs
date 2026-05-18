namespace Bcs.InvestApi.Contracts.Instruments;

using System.Text.Json.Serialization;

public sealed record BcsInstrument
{
    public string? Ticker { get; init; }

    public IReadOnlyList<BcsInstrumentBoard> Boards { get; init; } =
        Array.Empty<BcsInstrumentBoard>();

    public string? ShortName { get; init; }

    public string? DisplayName { get; init; }

    public string? Type { get; init; }

    public string? Isin { get; init; }

    public string? RegistrationCode { get; init; }

    public string? IssuerName { get; init; }

    public string? TradingCurrency { get; init; }

    public decimal? FaceValue { get; init; }

    public int? Scale { get; init; }

    public decimal? MinimumStep { get; init; }

    public decimal? AccruedInt { get; init; }

    public string? CurrencyStepPrice { get; init; }

    public string? SettleCode { get; init; }

    public string? InstrumentType { get; init; }

    public string? SettlementCurrency { get; init; }

    public string? SettlementDate { get; init; }

    public string? MaturityDate { get; init; }

    public decimal? LotSize { get; init; }

    public bool? IsQualifiedOnly { get; init; }

    public bool? IsCanShort { get; init; }

    public string? BaseAsset { get; init; }

    public int? QualifiedTestId { get; init; }

    public int? QualifiedTestIdTm { get; init; }

    public bool? AvailableForUnqualified { get; init; }

    public string? CurrencyNominal { get; init; }

    public decimal? StepPrice { get; init; }

    public bool? IsBcsProduct { get; init; }

    public long? CouponsPerYear { get; init; }

    public decimal? CouponRate { get; init; }

    public string? NextCoupon { get; init; }

    public decimal? ComplexProduct { get; init; }

    public string? BaseAssetFuture { get; init; }

    public string? SubType { get; init; }

    public decimal? PercentTargetCurrent { get; init; }

    public string? BusinessSector { get; init; }

    public decimal? PeNorm { get; init; }

    public decimal? PriceTangible { get; init; }

    public decimal? EpsGrowthRate { get; init; }

    public decimal? PredictedDps { get; init; }

    public decimal? DividendYield { get; init; }

    public decimal? PriceChangeYear { get; init; }

    public decimal? TargetPrice { get; init; }

    public decimal? Mktcap { get; init; }

    public bool? IsBlocked { get; init; }

    public int? BusinessSectorId { get; init; }

    public string? PrimaryBoard { get; init; }

    public IReadOnlyList<string> SecondaryBoards { get; init; } =
        Array.Empty<string>();

    public bool? IsCanMargin { get; init; }

    public bool? IsReplacementBond { get; init; }

    public string? SubTitle { get; init; }

    public string? CouponTypeName { get; init; }

    public string? EmissionDate { get; init; }

    public string? CreditRating { get; init; }

    public string? LiquidityRating { get; init; }

    public int? BcsScore { get; init; }

    public string? BcsScoreColor { get; init; }

    public string? NrdCode { get; init; }

    public decimal? Strike { get; init; }

    public string? BaseAssetSecuritySecCode { get; init; }

    public string? BaseAssetSecurityClassCode { get; init; }

    public string? BusinessCountry { get; init; }

    public string? BusinessCountryCode { get; init; }

    public decimal? PriceChangeHalfYear { get; init; }

    public decimal? PriceChangeMonth { get; init; }

    public decimal? PriceChangeEarlyYear { get; init; }

    public string? FirstCurrCode { get; init; }

    public bool? AmortisedMty { get; init; }
}

public sealed record BcsInstrumentBoard
{
    public string? ClassCode { get; init; }

    public string? Exchange { get; init; }
}
