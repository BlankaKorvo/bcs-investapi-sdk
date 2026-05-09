namespace Bcs.InvestApi.Portfolio;

public sealed record BcsPortfolioPosition
{
    public string? Type { get; init; }

    public Guid? SubAccountId { get; init; }

    public Guid? AgreementId { get; init; }

    public string? Account { get; init; }

    public string? Exchange { get; init; }

    public string? Ticker { get; init; }

    public string? DisplayName { get; init; }

    public string? BaseAssetTicker { get; init; }

    public string? Currency { get; init; }

    public string? UpperType { get; init; }

    public string? InstrumentType { get; init; }

    public string? Term { get; init; }

    public decimal Quantity { get; init; }

    public decimal Locked { get; init; }

    public decimal BalancePrice { get; init; }

    public decimal CurrentPrice { get; init; }

    public decimal BalanceValue { get; init; }

    public decimal BalanceValueRub { get; init; }

    public decimal BalanceValueUsd { get; init; }

    public decimal BalanceValueEur { get; init; }

    public decimal CurrentValue { get; init; }

    public decimal CurrentValueRub { get; init; }

    public decimal CurrentValueUsd { get; init; }

    public decimal CurrentValueEur { get; init; }

    public decimal UnrealizedPL { get; init; }

    public decimal UnrealizedPercentPL { get; init; }

    public decimal DailyPL { get; init; }

    public decimal DailyPercentPL { get; init; }

    public decimal PortfolioShare { get; init; }

    public int Scale { get; init; }

    public decimal MinimumStep { get; init; }

    public string? Board { get; init; }

    public string? PriceUnit { get; init; }

    public decimal FaceValue { get; init; }

    public decimal AccruedIncome { get; init; }

    public string? LogoLink { get; init; }

    public bool IsBlocked { get; init; }

    public bool IsBlockedTradeAccount { get; init; }

    public decimal LockedForFutures { get; init; }

    public decimal RatioQuantity { get; init; }

    public string? ExpireDate { get; init; }
}
