namespace Bcs.InvestApi.Contracts.Trades;

public sealed record BcsTradesSearchResponse
{
    public IReadOnlyList<BcsTrade> Records { get; init; } =
        Array.Empty<BcsTrade>();

    public long? TotalRecords { get; init; }

    public int? TotalPages { get; init; }
}

public sealed record BcsTrade
{
    public long? OrderNum { get; init; }

    public string? Ticker { get; init; }

    public long? TradeNum { get; init; }

    public string? ClientCode { get; init; }

    public string? ClassCode { get; init; }

    public string? SettlementCurrency { get; init; }

    public string? BaseCurrency { get; init; }

    public string? PriceCurrency { get; init; }

    public string? Side { get; init; }

    public string? InstrumentType { get; init; }

    public int? DealType { get; init; }

    public DateTimeOffset? TradeDateTime { get; init; }

    public decimal? Price { get; init; }

    public decimal? Volume { get; init; }

    public decimal? Go { get; init; }

    public decimal? ContractAmount { get; init; }

    public DateOnly? SettleDate { get; init; }

    public decimal? TradeQuantity { get; init; }

    public decimal? TradeQuantityLots { get; init; }
}
