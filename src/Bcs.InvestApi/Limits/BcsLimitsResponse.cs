namespace Bcs.InvestApi.Limits;

using System.Text.Json.Serialization;

public sealed record BcsLimitsResponse
{
    public IReadOnlyList<BcsDepoLimit> DepoLimit { get; init; } = Array.Empty<BcsDepoLimit>();

    public IReadOnlyList<BcsFutureHolding> FutureHolding { get; init; } = Array.Empty<BcsFutureHolding>();

    public IReadOnlyList<BcsMoneyLimit> MoneyLimits { get; init; } = Array.Empty<BcsMoneyLimit>();

    public IReadOnlyList<BcsFuturesLimit> FuturesLimits { get; init; } = Array.Empty<BcsFuturesLimit>();
}

public sealed record BcsDepoLimit
{
    public string? Ticker { get; init; }

    public string? ClassCode { get; init; }

    public string? Exchange { get; init; }

    public decimal AveragePrice { get; init; }

    public BcsLimitQuantity? Quantity { get; init; }

    public BcsLimitQuantity? QuantityBatch { get; init; }

    public string? InstrumentType { get; init; }

    public DateTimeOffset LoadDate { get; init; }

    public decimal LockedBuyValue { get; init; }

    public decimal LockedSellValue { get; init; }

    public decimal LockedBuyQuantity { get; init; }

    public decimal LockedSellQuantity { get; init; }
}

public sealed record BcsFutureHolding
{
    public string? Ticker { get; init; }

    public string? ClassCode { get; init; }

    public string? Exchange { get; init; }

    public decimal CbplPlanned { get; init; }

    public decimal VarMargin { get; init; }

    public decimal PositionValue { get; init; }

    public decimal TotalNet { get; init; }

    public DateTimeOffset ExecutionDate { get; init; }

    public decimal TotalVarMargin { get; init; }

    public decimal RealVarMargin { get; init; }

    public decimal AveragePrice { get; init; }

    public string? InstrumentType { get; init; }

    public DateTimeOffset TradeDate { get; init; }
}

public sealed record BcsMoneyLimit
{
    public string? Exchange { get; init; }

    public string? CurrencyCode { get; init; }

    public decimal Locked { get; init; }

    public decimal AveragePrice { get; init; }

    public string? InstrumentType { get; init; }

    public BcsLimitQuantity? Quantity { get; init; }

    public DateTimeOffset LoadDate { get; init; }
}

public sealed record BcsFuturesLimit
{
    public string? CurrencyCode { get; init; }

    public string? Exchange { get; init; }

    [JsonPropertyName("accruedint")]
    public decimal AccruedInt { get; init; }

    public decimal CbpLimit { get; init; }

    public decimal CbplUsed { get; init; }

    public decimal CbplPlanned { get; init; }

    public decimal CbplUsedForOrders { get; init; }

    public decimal CbplUsedForPositions { get; init; }

    public decimal OptionsPremium { get; init; }

    public string? InstrumentType { get; init; }

    public DateTimeOffset LoadDate { get; init; }

    public decimal VarMargin { get; init; }

    public decimal RealVarMargin { get; init; }
}

public sealed record BcsLimitQuantity
{
    public string? Type { get; init; }

    public decimal Value { get; init; }
}
