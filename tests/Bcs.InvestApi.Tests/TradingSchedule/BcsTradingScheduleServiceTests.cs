namespace Bcs.InvestApi.Tests.TradingSchedule;

using System.Net;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Errors;
using Bcs.InvestApi.Contracts.Exceptions;
using Bcs.InvestApi.Contracts.TradingSchedule;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsTradingScheduleServiceTests
{
    [Fact]
    public async Task GetDailyTradingScheduleAsync_DeserializesResponse()
    {
        const string scheduleJson = """
        {
          "isWorkDay": true,
          "dailySchedule": [
            {
              "startDate": "10:00:00",
              "endDate": "18:40:00",
              "tradingSessionType": "Торговый период",
              "tradingSessionStatus": "OPEN"
            }
          ]
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, scheduleJson)));
        var service = new BcsTradingScheduleService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var schedule = await service.GetDailyTradingScheduleAsync("TQBR", "SBER");

        Assert.True(schedule.IsWorkDay);
        var entry = Assert.Single(schedule.DailySchedule);
        Assert.Equal(new TimeOnly(10, 00, 00), entry.StartDate);
        Assert.Equal(new TimeOnly(18, 40, 00), entry.EndDate);
        Assert.Equal(BcsTradingSessionTypes.TradingPeriod, entry.TradingSessionType);
        Assert.Equal(BcsTradingSessionStatuses.Open, entry.TradingSessionStatus);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetDailyTradingScheduleAsync_NotFoundWithEmptyDailyScheduleLine_ThrowsBcsApiException()
    {
        const string errorJson = """
        {
          "displayOptions": {
            "text": "dailyScheduleLine is empty"
          },
          "timestamp": 1778329228690,
          "traceId": "99516aa86e67c5b4a5efa4000e7852f0",
          "type": "NOT_FOUND"
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.NotFound, errorJson)));
        var service = new BcsTradingScheduleService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() =>
            service.GetDailyTradingScheduleAsync("TQBR", "SBER"));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(errorJson, exception.ResponseBody);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetDailyTradingScheduleAsync_NotFoundWithDifferentBody_ThrowsBcsApiException()
    {
        const string errorJson = """
        {
          "displayOptions": {
            "text": "instrument not found"
          },
          "timestamp": 1778329228690,
          "traceId": "trace-1",
          "type": "NOT_FOUND"
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.NotFound, errorJson)));
        var service = new BcsTradingScheduleService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() =>
            service.GetDailyTradingScheduleAsync("TQBR", "SBER"));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(errorJson, exception.ResponseBody);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetDailyTradingScheduleAsync_SendsCallerQueryValues()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = new BcsTradingScheduleService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetDailyTradingScheduleAsync(" TQBR ", " sber ");

        Assert.Equal(
            new Uri("https://example.test/trade-api-information-service/api/v1/trading-schedule/daily-schedule?classCode=%20TQBR%20&ticker=%20sber%20"),
            handler.LastRequest?.RequestUri);
    }

    [Theory]
    [InlineData(null, "SBER")]
    [InlineData("", "SBER")]
    [InlineData("TQBR", null)]
    [InlineData("TQBR", "")]
    public async Task GetDailyTradingScheduleAsync_WithNullOrEmptyRequiredQueryParameter_Throws(
        string? classCode,
        string? ticker)
    {
        var service = new BcsTradingScheduleService(
            CreateSettings(),
            new HttpClient(new CapturingHttpMessageHandler((_, _) =>
                Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")))),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.GetDailyTradingScheduleAsync(classCode!, ticker!));
    }

    [Fact]
    public async Task GetTradingScheduleStatusAsync_DeserializesResponse()
    {
        const string statusJson = """
        {
          "tradingSessionTypeId": 5,
          "tradingSessionType": "Торговый период",
          "tradingSessionStatus": "OPEN",
          "nextSessionDate": "2024-07-29T15:51:28.071Z"
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, statusJson)));
        var service = new BcsTradingScheduleService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var status = await service.GetTradingScheduleStatusAsync("TQBR");

        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal(
            new Uri("https://example.test/trade-api-information-service/api/v1/trading-schedule/status?classCode=TQBR"),
            handler.LastRequest?.RequestUri);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);

        Assert.Equal(5, status.TradingSessionTypeId);
        Assert.Equal(BcsTradingSessionTypes.TradingPeriod, status.TradingSessionType);
        Assert.Equal(BcsTradingSessionStatuses.Open, status.TradingSessionStatus);
        Assert.Equal(
            new DateTimeOffset(2024, 07, 29, 15, 51, 28, 071, TimeSpan.Zero),
            status.NextSessionDate);
    }

    [Fact]
    public async Task GetTradingScheduleStatusAsync_EncodesClassCode()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        var service = new BcsTradingScheduleService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetTradingScheduleStatusAsync(" TQBR ");

        Assert.Equal(
            new Uri("https://example.test/trade-api-information-service/api/v1/trading-schedule/status?classCode=%20TQBR%20"),
            handler.LastRequest?.RequestUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetTradingScheduleStatusAsync_WithNullOrEmptyClassCode_Throws(string? classCode)
    {
        var service = new BcsTradingScheduleService(
            CreateSettings(),
            new HttpClient(new CapturingHttpMessageHandler((_, _) =>
                Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")))),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.GetTradingScheduleStatusAsync(classCode!));
    }

    [Fact]
    public async Task GetTradingScheduleStatusAsync_TooManyRequests_ThrowsWithApiError()
    {
        const string errorJson = """
        {
          "timestamp": 1715000000,
          "traceId": "trace-status-1",
          "type": "RESOURCE_EXHAUSTED",
          "errors": [],
          "displayOptions": {}
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.TooManyRequests, errorJson)));
        var service = new BcsTradingScheduleService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() =>
            service.GetTradingScheduleStatusAsync("TQBR"));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal("trading-schedule-status", exception.Endpoint);
        Assert.NotNull(exception.ApiError);
        Assert.Equal(BcsApiErrorTypes.ResourceExhausted, exception.ApiError!.Type);
        Assert.Equal("trace-status-1", exception.ApiError.TraceId);
    }

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

        public void InvalidateAccessToken(string rejectedAccessToken)
        {
        }
    }
}
