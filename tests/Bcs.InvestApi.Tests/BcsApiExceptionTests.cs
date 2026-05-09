namespace Bcs.InvestApi.Tests;

using System.Net;
using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Limits;
using Bcs.InvestApi.Portfolio;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsApiExceptionTests
{
    private static readonly Uri AuthUrl = new("https://example.test/token");
    private static readonly Uri LimitsUrl = new("https://example.test/trade-api-bff-limit/api/v1/limits");
    private static readonly Uri PortfolioUrl = new("https://example.test/trade-api-bff-portfolio/api/v1/portfolio");

    [Fact]
    public async Task GetLimitsAsync_ErrorStatus_ThrowsBcsApiExceptionWithResponseBody()
    {
        const string errorJson = """
        {
          "code": "limit_exceeded",
          "message": "Daily limit exceeded",
          "trace_id": "trace-1"
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.TooManyRequests, errorJson)));
        var service = new BcsLimitsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            CreateReadSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetLimitsAsync());

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(errorJson, exception.ResponseBody);
        Assert.Equal("limits", exception.Endpoint);
        Assert.Contains("limits", exception.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetLimitsAsync_UnauthorizedOnce_ForcesRefreshAndRetriesReadRequest()
    {
        const string limitsJson = """
        {
          "depoLimit": [],
          "futureHolding": [],
          "moneyLimits": [],
          "futuresLimits": []
        }
        """;

        var settings = CreateSettings();
        var authRequestCount = 0;
        var limitAccessTokens = new List<string?>();
        var handler = new CapturingHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri == AuthUrl)
            {
                authRequestCount++;
                return Task.FromResult(JsonResponse(
                    HttpStatusCode.OK,
                    AuthResponseJson($"access-{authRequestCount}", $"refresh-{authRequestCount + 1}")));
            }

            if (request.RequestUri == LimitsUrl)
            {
                limitAccessTokens.Add(request.Headers.Authorization?.Parameter);

                return Task.FromResult(limitAccessTokens.Count == 1
                    ? JsonResponse(HttpStatusCode.Unauthorized, """{"error":"token_expired"}""")
                    : JsonResponse(HttpStatusCode.OK, limitsJson));
            }

            throw new InvalidOperationException($"Unexpected request URI '{request.RequestUri}'.");
        });

        using var httpClient = new HttpClient(handler);
        await using var tokens = CreateTokenManager(settings, httpClient);
        var service = new BcsLimitsService(settings, httpClient, tokens, CreateReadSender(settings));

        var limits = await service.GetLimitsAsync();

        Assert.Empty(limits.DepoLimit);
        Assert.Equal(new[] { "access-1", "access-2" }, limitAccessTokens);
        Assert.Equal(2, authRequestCount);
        Assert.Equal(4, handler.RequestCount);
    }

    [Fact]
    public async Task GetLimitsAsync_UnauthorizedTwice_ThrowsBcsApiExceptionAfterSingleRefresh()
    {
        const string unauthorizedJson = """{"error":"token_expired"}""";

        var settings = CreateSettings();
        var authRequestCount = 0;
        var limitAccessTokens = new List<string?>();
        var handler = new CapturingHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri == AuthUrl)
            {
                authRequestCount++;
                return Task.FromResult(JsonResponse(
                    HttpStatusCode.OK,
                    AuthResponseJson($"access-{authRequestCount}", $"refresh-{authRequestCount + 1}")));
            }

            if (request.RequestUri == LimitsUrl)
            {
                limitAccessTokens.Add(request.Headers.Authorization?.Parameter);
                return Task.FromResult(JsonResponse(HttpStatusCode.Unauthorized, unauthorizedJson));
            }

            throw new InvalidOperationException($"Unexpected request URI '{request.RequestUri}'.");
        });

        using var httpClient = new HttpClient(handler);
        await using var tokens = CreateTokenManager(settings, httpClient);
        var service = new BcsLimitsService(settings, httpClient, tokens, CreateReadSender(settings));

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetLimitsAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(unauthorizedJson, exception.ResponseBody);
        Assert.Equal("limits", exception.Endpoint);
        Assert.Equal(new[] { "access-1", "access-2" }, limitAccessTokens);
        Assert.Equal(2, authRequestCount);
        Assert.Equal(4, handler.RequestCount);
    }

    [Fact]
    public async Task GetPortfolioAsync_UnauthorizedOnce_ForcesRefreshAndRetriesReadRequest()
    {
        const string portfolioJson = """
        [
          {
            "ticker": "SBER",
            "quantity": 1
          }
        ]
        """;

        var settings = CreateSettings();
        var authRequestCount = 0;
        var portfolioAccessTokens = new List<string?>();
        var handler = new CapturingHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri == AuthUrl)
            {
                authRequestCount++;
                return Task.FromResult(JsonResponse(
                    HttpStatusCode.OK,
                    AuthResponseJson($"access-{authRequestCount}", $"refresh-{authRequestCount + 1}")));
            }

            if (request.RequestUri == PortfolioUrl)
            {
                portfolioAccessTokens.Add(request.Headers.Authorization?.Parameter);

                return Task.FromResult(portfolioAccessTokens.Count == 1
                    ? JsonResponse(HttpStatusCode.Unauthorized, """{"error":"token_expired"}""")
                    : JsonResponse(HttpStatusCode.OK, portfolioJson));
            }

            throw new InvalidOperationException($"Unexpected request URI '{request.RequestUri}'.");
        });

        using var httpClient = new HttpClient(handler);
        await using var tokens = CreateTokenManager(settings, httpClient);
        var service = new BcsPortfolioService(settings, httpClient, tokens, CreateReadSender(settings));

        var portfolio = await service.GetPortfolioAsync();

        var position = Assert.Single(portfolio);
        Assert.Equal("SBER", position.Ticker);
        Assert.Equal(1m, position.Quantity);
        Assert.Equal(new[] { "access-1", "access-2" }, portfolioAccessTokens);
        Assert.Equal(2, authRequestCount);
        Assert.Equal(4, handler.RequestCount);
    }

    [Fact]
    public async Task GetPortfolioAsync_ErrorStatus_ThrowsBcsApiExceptionWithResponseBody()
    {
        const string errorJson = """
        {
          "code": "portfolio_unavailable",
          "message": "Portfolio is temporarily unavailable",
          "trace_id": "trace-2"
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.Forbidden, errorJson)));
        var service = new BcsPortfolioService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            CreateReadSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetPortfolioAsync());

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(errorJson, exception.ResponseBody);
        Assert.Equal("portfolio", exception.Endpoint);
        Assert.Contains("portfolio", exception.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    private static BcsReadHttpSender CreateReadSender() =>
        new(CreateSettings());

    private static BcsReadHttpSender CreateReadSender(BcsInvestApiSettings settings) =>
        new(settings);

    private static BcsInvestApiSettings CreateSettings() =>
        new()
        {
            RefreshToken = "settings-refresh-1",
            ClientId = BcsAuthClientIds.TradeApiRead,
            AuthUrl = AuthUrl,
            BaseUrl = new Uri("https://example.test"),
            HttpRetryAttempts = 0,
        };

    private static BcsTokenManager CreateTokenManager(
        BcsInvestApiSettings settings,
        HttpClient httpClient)
    {
        var auth = new BcsAuthService(httpClient, settings);
        return new BcsTokenManager(
            auth,
            settings,
            new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero)));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private static string AuthResponseJson(
        string accessToken,
        string refreshToken) =>
        $$"""
        {
          "access_token": "{{accessToken}}",
          "expires_in": 86400,
          "refresh_expires_in": 7776000,
          "refresh_token": "{{refreshToken}}",
          "token_type": "bearer",
          "not-before-policy": "0",
          "session_state": "session-state-1",
          "scope": "trade-api-read"
        }
        """;

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
