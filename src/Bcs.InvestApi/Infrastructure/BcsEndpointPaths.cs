namespace Bcs.InvestApi.Infrastructure;

internal static class BcsEndpointPaths
{
    private const string LimitService = "trade-api-bff-limit/api/v1";
    private const string PortfolioService = "trade-api-bff-portfolio/api/v1";
    private const string InformationService = "trade-api-information-service/api/v1";
    private const string MarketDataService = "trade-api-market-data-connector/api/v1";

    internal const string Limits = LimitService + "/limits";
    internal const string Portfolio = PortfolioService + "/portfolio";

    internal static class TradingSchedule
    {
        internal const string DailySchedule = InformationService + "/trading-schedule/daily-schedule";
    }

    internal static class Instruments
    {
        internal const string ByIsins = InformationService + "/instruments/by-isins";
        internal const string ByTickers = InformationService + "/instruments/by-tickers";
        internal const string ByType = InformationService + "/instruments/by-type";
    }

    internal static class MarketData
    {
        internal const string Candles = MarketDataService + "/candles-chart";
    }
}
