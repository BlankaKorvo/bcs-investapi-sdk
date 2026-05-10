namespace Bcs.InvestApi.Tests;

using System.Net;
using Bcs.InvestApi.DTO.Enums;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsEndpointUrlTests
{
    [Fact]
    public async Task GetLimitsAsync_UsesConfiguredBaseUrl()
    {
        var settings = CreateSettings(new Uri("https://mock.example/root"));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = new BcsLimitsService(
            settings,
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetLimitsAsync();

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            new Uri("https://mock.example/root/trade-api-bff-limit/api/v1/limits"),
            handler.LastRequest.RequestUri);
    }

    [Fact]
    public async Task GetPortfolioAsync_UsesConfiguredBaseUrl()
    {
        var settings = CreateSettings(new Uri("https://mock.example/root/"));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var service = new BcsPortfolioService(
            settings,
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetPortfolioAsync();

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            new Uri("https://mock.example/root/trade-api-bff-portfolio/api/v1/portfolio"),
            handler.LastRequest.RequestUri);
    }

    [Fact]
    public async Task GetDailyTradingScheduleAsync_UsesConfiguredBaseUrlAndQuery()
    {
        var settings = CreateSettings(new Uri("https://mock.example/root/"));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"isWorkDay":true,"dailySchedule":[]}""")));
        var service = new BcsTradingScheduleService(
            settings,
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetDailyTradingScheduleAsync("TQBR", "SBER");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            new Uri("https://mock.example/root/trade-api-information-service/api/v1/trading-schedule/daily-schedule?classCode=TQBR&ticker=SBER"),
            handler.LastRequest.RequestUri);
    }

    [Fact]
    public async Task GetInstrumentsByIsinsAsync_UsesConfiguredBaseUrlAndQuery()
    {
        var settings = CreateSettings(new Uri("https://mock.example/root/"));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var service = new BcsInstrumentsService(
            settings,
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetInstrumentsByIsinsAsync(new[] { "RU0007661625" }, page: 2, size: 50);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            new Uri("https://mock.example/root/trade-api-information-service/api/v1/instruments/by-isins?size=50&page=2"),
            handler.LastRequest.RequestUri);
        Assert.Equal("""{"isins":["RU0007661625"]}""", handler.LastRequestContent);
    }

    [Fact]
    public async Task GetInstrumentsByTickersAsync_UsesConfiguredBaseUrlAndQuery()
    {
        var settings = CreateSettings(new Uri("https://mock.example/root/"));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var service = new BcsInstrumentsService(
            settings,
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetInstrumentsByTickersAsync(new[] { "SBER" }, page: 2, size: 50);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            new Uri("https://mock.example/root/trade-api-information-service/api/v1/instruments/by-tickers?size=50&page=2"),
            handler.LastRequest.RequestUri);
        Assert.Equal("""{"tickers":["SBER"]}""", handler.LastRequestContent);
    }

    [Fact]
    public async Task GetInstrumentsByTypeAsync_UsesConfiguredBaseUrlAndQuery()
    {
        var settings = CreateSettings(new Uri("https://mock.example/root/"));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var service = new BcsInstrumentsService(
            settings,
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetInstrumentsByTypeAsync(
            BcsInstrumentTypes.Options,
            page: 2,
            size: 50,
            baseAssetTicker: "SBER");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            new Uri("https://mock.example/root/trade-api-information-service/api/v1/instruments/by-type?type=OPTIONS&baseAssetTicker=SBER&size=50&page=2"),
            handler.LastRequest.RequestUri);
        Assert.Null(handler.LastRequestContent);
    }

    [Fact]
    public async Task GetCandlesAsync_UsesConfiguredBaseUrlAndQuery()
    {
        var settings = CreateSettings(new Uri("https://mock.example/root/"));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"ticker":"SBER","classCode":"TQBR","startDate":"2025-11-14T07:00:00Z","endDate":"2025-11-14T10:00:00Z","timeFrame":"H1","bars":[]}""")));
        var service = new BcsMarketDataService(
            settings,
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetCandlesAsync(
            "TQBR",
            "SBER",
            new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 11, 14, 10, 0, 0, TimeSpan.Zero),
            BcsCandleTimeFrames.Hour1);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            new Uri("https://mock.example/root/trade-api-market-data-connector/api/v1/candles-chart?classCode=TQBR&ticker=SBER&startDate=2025-11-14T07%3A00%3A00.0000000Z&endDate=2025-11-14T10%3A00%3A00.0000000Z&timeFrame=H1"),
            handler.LastRequest.RequestUri);
        Assert.Null(handler.LastRequestContent);
    }

    [Fact]
    public void ValidateTransportSettings_WithHttpBaseUrl_ThrowsByDefault()
    {
        var settings = CreateSettings(new Uri("http://localhost:8080"));

        var exception = Assert.Throws<InvalidOperationException>(settings.ValidateTransportSettings);

        Assert.Contains("BCS base URL", exception.Message);
        Assert.Contains("absolute HTTPS URI", exception.Message);
        Assert.Contains("http://localhost:8080", exception.Message);
    }

    [Fact]
    public void CreateEndpointUrl_WithHttpBaseUrlAndTestingOptIn_UsesLocalBaseUrl()
    {
        var settings = CreateSettings(new Uri("http://localhost:8080"));
        settings.AllowInsecureHttpForTesting = true;

        Assert.Equal(
            new Uri("http://localhost:8080/trade-api-bff-limit/api/v1/limits"),
            settings.CreateEndpointUrl(BcsEndpointPaths.Limits));
    }

    private static BcsInvestApiSettings CreateSettings(Uri baseUrl) =>
        new()
        {
            BaseUrl = baseUrl,
        };

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private sealed class StaticTokenProvider : IBcsAccessTokenProvider
    {
        private readonly string _accessToken;

        public StaticTokenProvider(string accessToken)
        {
            _accessToken = accessToken;
        }

        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_accessToken);
    }
}
