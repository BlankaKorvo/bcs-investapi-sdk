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

    [JsonPropertyName("isin")]
    public string? Isin { get; init; }

    public string? RegistrationCode { get; init; }

    public string? IssuerName { get; init; }

    public string? TradingCurrency { get; init; }

    public decimal? FaceValue { get; init; }

    public int? Scale { get; init; }

    public decimal? MinimumStep { get; init; }

    [JsonPropertyName("accruedInt")]
    public decimal? AccruedInt { get; init; }

    public string? CurrencyStepPrice { get; init; }

    public string? SettleCode { get; init; }

    public string? InstrumentType { get; init; }

    public string? SettlementCurrency { get; init; }

    public string? SettlementDate { get; init; }

    public string? MaturityDate { get; init; }

    public decimal? LotSize { get; init; }

    [JsonPropertyName("promoIdx")]
    public int? PromoIdx { get; init; }

    public bool? IsQualifiedOnly { get; init; }

    public bool? IsCanShort { get; init; }

    public string? BaseAsset { get; init; }

    public int? QualifiedTestId { get; init; }

    public int? QualifiedTestIdTm { get; init; }

    public bool? AvailableForUnqualified { get; init; }

    public string? CurrencyNominal { get; init; }

    public decimal? StepPrice { get; init; }

    [JsonPropertyName("isBcsProduct")]
    public bool? IsBcsProduct { get; init; }

    public string? LogoLink { get; init; }

    public long? CouponsPerYear { get; init; }

    public decimal? CouponRate { get; init; }

    public string? NextCoupon { get; init; }

    public decimal? ComplexProduct { get; init; }

    public string? BaseAssetFuture { get; init; }

    public string? SubType { get; init; }

    public decimal? PercentTargetCurrent { get; init; }

    public string? BusinessSector { get; init; }

    [JsonPropertyName("peNorm")]
    public decimal? PeNorm { get; init; }

    [JsonPropertyName("priceTangible")]
    public decimal? PriceTangible { get; init; }

    [JsonPropertyName("epsGrowthRate")]
    public decimal? EpsGrowthRate { get; init; }

    [JsonPropertyName("predictedDps")]
    public decimal? PredictedDps { get; init; }

    public decimal? DividendYield { get; init; }

    public decimal? PriceChangeYear { get; init; }

    public decimal? TargetPrice { get; init; }

    [JsonPropertyName("mktcap")]
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

    public int? ExcludeTypeFlags { get; init; }

    public string? CreditRating { get; init; }

    public string? LiquidityRating { get; init; }

    [JsonPropertyName("bcsScore")]
    public int? BcsScore { get; init; }

    [JsonPropertyName("bcsScoreColor")]
    public string? BcsScoreColor { get; init; }

    [JsonPropertyName("cfi")]
    public string? Cfi { get; init; }

    [JsonPropertyName("nrdCode")]
    public string? NrdCode { get; init; }

    public decimal? Strike { get; init; }

    public string? BaseAssetSecuritySecCode { get; init; }

    public string? BaseAssetSecurityClassCode { get; init; }

    public string? BusinessCountry { get; init; }

    public string? BusinessCountryCode { get; init; }

    public decimal? PriceChangeHalfYear { get; init; }

    public decimal? PriceChangeMonth { get; init; }

    public decimal? PriceChangeEarlyYear { get; init; }

    public IReadOnlyList<int> ExcludeTypes { get; init; } =
        Array.Empty<int>();

    public string? DisplayNameSecond { get; init; }

    public string? FirstCurrCode { get; init; }

    [JsonPropertyName("amortisedMty")]
    public bool? AmortisedMty { get; init; }
}

public sealed record BcsInstrumentBoard
{
    public string? ClassCode { get; init; }

    public string? Exchange { get; init; }
}
