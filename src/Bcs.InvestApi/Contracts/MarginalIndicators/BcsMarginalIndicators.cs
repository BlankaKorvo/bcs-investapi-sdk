namespace Bcs.InvestApi.Contracts.MarginalIndicators;

/// <summary>
/// Real-time margin indicators snapshot pushed by the marginal-indicators WebSocket
/// (<see cref="WebSockets.BcsWebSocketEndpoints.MarginalIndicators"/>).
/// No subscribe message is required; the server pushes the full object after connect.
/// </summary>
public sealed record BcsMarginalIndicators
{
    public BcsMarginAgreementData? AgreementData { get; init; }
    public BcsMarginPortfolioCurrentValue? PortfolioCurrentValue { get; init; }
    public BcsMarginPortfolioStability? PortfolioStability { get; init; }
    public BcsMarginFortsStability? FortsStability { get; init; }
    public BcsMarginParameters? MarginParameters { get; init; }
    public IReadOnlyList<BcsMarginCurrencyAmount> CashByCurrency { get; init; } =
        Array.Empty<BcsMarginCurrencyAmount>();
    public BcsMarginMoneyDebt? MoneyDebt { get; init; }
    public BcsMarginSecurities? MarginSecurities { get; init; }
    public BcsMarginFuturesParameters? FuturesParameters { get; init; }
}

/// <summary>agreementData — brokerage account details.</summary>
public sealed record BcsMarginAgreementData
{
    public string? Number { get; init; }
    public bool? IsIia { get; init; }
    public string? DisplayName { get; init; }
    public string? SubaccountId { get; init; }
    public string? Color { get; init; }
}

/// <summary>portfolioCurrentValue — portfolio value.</summary>
public sealed record BcsMarginPortfolioCurrentValue
{
    public decimal? CurrentValueRub { get; init; }
    public decimal? DailyPortfolioChangeRub { get; init; }
    public decimal? DailyPortfolioChangePerc { get; init; }
    public decimal? TotalPortfolioChangeRub { get; init; }
    public decimal? TotalPortfolioChangePerc { get; init; }
    public decimal? CollateralAvailable { get; init; }
    public decimal? VarMargin { get; init; }
}

/// <summary>portfolioStability — portfolio stability.</summary>
public sealed record BcsMarginPortfolioStability
{
    public bool? IsMarginOn { get; init; }
    public decimal? BorrowedFunds { get; init; }
    public int? StabilityStatusId { get; init; }
    public string? StabilityStatusName { get; init; }
    public decimal? RefillSum { get; init; }
}

/// <summary>fortsStability — FORTS stability.</summary>
public sealed record BcsMarginFortsStability
{
    public decimal? OwnFunds { get; init; }
    public decimal? FreeCash { get; init; }
    public int? StabilityStatusId { get; init; }
    public string? StabilityStatusName { get; init; }
    public decimal? RefillSum { get; init; }
    public decimal? FortsMarginCall { get; init; }
    public decimal? FortsForcedClosure { get; init; }
    public int? RiskLevel { get; init; }
    public decimal? OptionsPremium { get; init; }
}

/// <summary>marginParameters — margin portfolio parameters.</summary>
public sealed record BcsMarginParameters
{
    public decimal? MarginValue { get; init; }
    public decimal? Collateral { get; init; }
    public decimal? MarginCall { get; init; }
    public decimal? ForcedClosure { get; init; }
}

/// <summary>A currency/amount pair (used by cashByCurrency and moneyDebt.debtByCurrency).</summary>
public sealed record BcsMarginCurrencyAmount
{
    public string? Currency { get; init; }
    public decimal? Sum { get; init; }
}

/// <summary>moneyDebt — debts by currency.</summary>
public sealed record BcsMarginMoneyDebt
{
    public decimal? TotalMoneyDebt { get; init; }
    public IReadOnlyList<BcsMarginCurrencyAmount> DebtByCurrency { get; init; } =
        Array.Empty<BcsMarginCurrencyAmount>();
}

/// <summary>marginSecurities — margin positions.</summary>
public sealed record BcsMarginSecurities
{
    public decimal? TotalMarginSecurity { get; init; }
    public IReadOnlyList<BcsMarginSecurity> MarginBySecurity { get; init; } =
        Array.Empty<BcsMarginSecurity>();
}

public sealed record BcsMarginSecurity
{
    public string? Ticker { get; init; }
    public string? Board { get; init; }
    public string? DisplayName { get; init; }
    public string? LogoLink { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? CurrentValue { get; init; }
    public string? Currency { get; init; }
    public decimal? Discount { get; init; }
}

/// <summary>futuresParameters — futures positions.</summary>
public sealed record BcsMarginFuturesParameters
{
    public decimal? FuturesValue { get; init; }
    public IReadOnlyList<BcsMarginFuturesPosition> FuturesPortfolio { get; init; } =
        Array.Empty<BcsMarginFuturesPosition>();
}

public sealed record BcsMarginFuturesPosition
{
    public string? Ticker { get; init; }
    public string? Board { get; init; }
    public decimal? CurrentValue { get; init; }
    public decimal? BalanceValue { get; init; }
    public decimal? FuturesCollateral { get; init; }
    public decimal? Discount { get; init; }
    public string? DisplayName { get; init; }
    public string? LogoLink { get; init; }
}
