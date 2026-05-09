namespace Bcs.InvestApi.Tests.MarketData;

using System.Net;
using Bcs.InvestApi;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.MarketData;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsMarketDataServiceTests
{
    [Fact]
    public async Task GetCandlesAsync_SendsGetRequestAndDeserializesResponse()
    {
        const string candlesJson = """
        {
          "ticker": "SBER",
          "classCode": "TQBR",
          "startDate": "2025-11-14T07:00:00Z",
          "endDate": "2025-11-14T10:00:00Z",
          "timeFrame": "H1",
          "bars": [
            {
              "time": "2025-11-14T07:00:00Z",
              "open": 301.5,
              "close": 302.1,
              "high": 303,
              "low": 300.7,
              "volume": 1500
            }
          ]
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, candlesJson)));
        var service = new BcsMarketDataService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var candles = await service.GetCandlesAsync(
            " TQBR ",
            " SBER ",
            new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 11, 14, 10, 0, 0, TimeSpan.Zero),
            BcsCandleTimeFrames.Hour1);

        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal(
            new Uri("https://example.test/trade-api-market-data-connector/api/v1/candles-chart?classCode=%20TQBR%20&ticker=%20SBER%20&startDate=2025-11-14T07%3A00%3A00.0000000Z&endDate=2025-11-14T10%3A00%3A00.0000000Z&timeFrame=H1"),
            handler.LastRequest?.RequestUri);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Null(handler.LastRequestContent);

        Assert.Equal("SBER", candles.Ticker);
        Assert.Equal("TQBR", candles.ClassCode);
        Assert.Equal(new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero), candles.StartDate);
        Assert.Equal(new DateTimeOffset(2025, 11, 14, 10, 0, 0, TimeSpan.Zero), candles.EndDate);
        Assert.Equal(BcsCandleTimeFrames.Hour1, candles.TimeFrame);

        var bar = Assert.Single(candles.Bars);
        Assert.Equal(new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero), bar.Time);
        Assert.Equal(301.5m, bar.Open);
        Assert.Equal(302.1m, bar.Close);
        Assert.Equal(303m, bar.High);
        Assert.Equal(300.7m, bar.Low);
        Assert.Equal(1500m, bar.Volume);
    }

    [Theory]
    [InlineData(null, "SBER", "M1")]
    [InlineData("", "SBER", "M1")]
    [InlineData("TQBR", null, "M1")]
    [InlineData("TQBR", "", "M1")]
    [InlineData("TQBR", "SBER", null)]
    [InlineData("TQBR", "SBER", "")]
    public async Task GetCandlesAsync_WithNullOrEmptyRequiredQueryParameter_Throws(
        string? classCode,
        string? ticker,
        string? timeFrame)
    {
        var service = CreateService();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.GetCandlesAsync(
                classCode!,
                ticker!,
                new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2025, 11, 14, 8, 0, 0, TimeSpan.Zero),
                timeFrame!));
    }

    [Fact]
    public async Task GetCandlesAsync_WithUnknownTimeFrame_SendsRequest()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = new BcsMarketDataService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetCandlesAsync(
            "TQBR",
            "SBER",
            new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 11, 14, 8, 0, 0, TimeSpan.Zero),
            "M2");

        Assert.Equal(
            new Uri("https://example.test/trade-api-market-data-connector/api/v1/candles-chart?classCode=TQBR&ticker=SBER&startDate=2025-11-14T07%3A00%3A00.0000000Z&endDate=2025-11-14T08%3A00%3A00.0000000Z&timeFrame=M2"),
            handler.LastRequest?.RequestUri);
    }

    [Fact]
    public async Task GetCandlesAsync_SendsCallerTimeFrameValue()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = new BcsMarketDataService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetCandlesAsync(
            "TQBR",
            "SBER",
            new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 11, 14, 8, 0, 0, TimeSpan.Zero),
            " h1 ");

        Assert.Equal(
            new Uri("https://example.test/trade-api-market-data-connector/api/v1/candles-chart?classCode=TQBR&ticker=SBER&startDate=2025-11-14T07%3A00%3A00.0000000Z&endDate=2025-11-14T08%3A00%3A00.0000000Z&timeFrame=%20h1%20"),
            handler.LastRequest?.RequestUri);
    }

    [Fact]
    public async Task GetCandlesAsync_WithEndDateBeforeStartDate_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetCandlesAsync(
                "TQBR",
                "SBER",
                new DateTimeOffset(2025, 11, 14, 8, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
                BcsCandleTimeFrames.Minute1));
    }

    [Fact]
    public async Task GetCandlesAsync_WithMoreThanMaxFixedFrameCandles_ThrowsBeforeRequest()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = new BcsMarketDataService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetCandlesAsync(
                "TQBR",
                "SBER",
                new DateTimeOffset(2025, 11, 14, 7, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2025, 11, 15, 7, 1, 0, TimeSpan.Zero),
                BcsCandleTimeFrames.Minute1));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetCandlesAsync_WithMoreThanMaxMonthCandles_ThrowsBeforeRequest()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = new BcsMarketDataService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());
        var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetCandlesAsync(
                "TQBR",
                "SBER",
                startDate,
                startDate.AddMonths(1441),
                BcsCandleTimeFrames.Month));

        Assert.Equal(0, handler.RequestCount);
    }

    private static BcsMarketDataService CreateService() =>
        new(
            CreateSettings(),
            new HttpClient(new CapturingHttpMessageHandler((_, _) =>
                Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")))),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

    private static BcsInvestApiSettings CreateSettings() =>
        new()
        {
            BaseUrl = new Uri("https://example.test"),
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
