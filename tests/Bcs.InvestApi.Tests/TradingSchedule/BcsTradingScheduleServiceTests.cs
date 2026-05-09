namespace Bcs.InvestApi.Tests.TradingSchedule;

using System.Net;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Bcs.InvestApi.TradingSchedule;
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
            new BcsReadHttpSender(CreateSettings()));

        var schedule = await service.GetDailyTradingScheduleAsync("TQBR", "SBER");

        Assert.True(schedule.IsWorkDay);
        var entry = Assert.Single(schedule.DailySchedule);
        Assert.Equal(new TimeOnly(10, 00, 00), entry.StartDate);
        Assert.Equal(new TimeOnly(18, 40, 00), entry.EndDate);
        Assert.Equal("Торговый период", entry.TradingSessionType);
        Assert.Equal("OPEN", entry.TradingSessionStatus);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Theory]
    [InlineData("", "SBER")]
    [InlineData(" ", "SBER")]
    [InlineData("TQBR", "")]
    [InlineData("TQBR", " ")]
    public async Task GetDailyTradingScheduleAsync_WithBlankRequiredQueryParameter_Throws(
        string classCode,
        string ticker)
    {
        var service = new BcsTradingScheduleService(
            CreateSettings(),
            new HttpClient(new CapturingHttpMessageHandler((_, _) =>
                Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")))),
            new StaticTokenProvider("access-token-1"),
            new BcsReadHttpSender(CreateSettings()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetDailyTradingScheduleAsync(classCode, ticker));
    }

    private static BcsInvestApiSettings CreateSettings() =>
        new()
        {
            BaseUrl = new Uri("https://example.test"),
            HttpRetryAttempts = 0,
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
