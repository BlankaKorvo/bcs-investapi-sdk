namespace Bcs.InvestApi.Tests;

using System.Net;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Errors;
using Bcs.InvestApi.Contracts.Exceptions;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsApiExceptionTests
{
    private static readonly Uri AuthUrl = new("https://example.test/token");
    private static readonly Uri LimitsUrl = new("https://example.test/trade-api-bff-limit/api/v1/limits");

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

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetLimitsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(errorJson, exception.ResponseBody);
        Assert.Equal("limits", exception.Endpoint);
        Assert.Contains("limits", exception.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetLimitsAsync_Unauthorized_ThrowsBcsApiExceptionAfterSingleApiRequest()
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
        var service = new BcsLimitsService(settings, httpClient, tokens, CreateReadSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetLimitsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(unauthorizedJson, exception.ResponseBody);
        Assert.Equal("limits", exception.Endpoint);
        Assert.Equal(new[] { "access-1" }, limitAccessTokens);
        Assert.Equal(1, authRequestCount);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetLimitsAsync_InvalidTokenUnauthorized_InvalidatesAccessTokenWithoutRetry()
    {
        const string invalidTokenJson = """{"error":"invalid_token"}""";

        var settings = CreateSettings();
        var authRefreshTokens = new List<string>();
        var limitAccessTokens = new List<string?>();
        var handler = new CapturingHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri == AuthUrl)
            {
                authRefreshTokens.Add(await ReadFormValueAsync(
                    request,
                    "refresh_token",
                    cancellationToken));

                return JsonResponse(
                    HttpStatusCode.OK,
                    authRefreshTokens.Count == 1
                        ? AuthResponseJson("access-1", "runtime-refresh-2")
                        : AuthResponseJson("access-2", "runtime-refresh-3"));
            }

            if (request.RequestUri == LimitsUrl)
            {
                limitAccessTokens.Add(request.Headers.Authorization?.Parameter);

                if (limitAccessTokens.Count == 1)
                {
                    var invalidTokenResponse = JsonResponse(HttpStatusCode.Unauthorized, invalidTokenJson);
                    invalidTokenResponse.Headers.WwwAuthenticate.ParseAdd("Bearer error=\"invalid_token\"");
                    return invalidTokenResponse;
                }

                return JsonResponse(HttpStatusCode.OK, "{}");
            }

            throw new InvalidOperationException($"Unexpected request URI '{request.RequestUri}'.");
        });

        using var httpClient = new HttpClient(handler);
        await using var tokens = CreateTokenManager(settings, httpClient);
        var service = new BcsLimitsService(settings, httpClient, tokens, CreateReadSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetLimitsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(invalidTokenJson, exception.ResponseBody);
        Assert.Equal(new[] { "access-1" }, limitAccessTokens);
        Assert.Equal(new[] { "settings-refresh-1" }, authRefreshTokens);
        Assert.Equal(2, handler.RequestCount);

        var limits = await service.GetLimitsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(limits.DepoLimit);
        Assert.Equal(new[] { "access-1", "access-2" }, limitAccessTokens);
        Assert.Equal(new[] { "settings-refresh-1", "runtime-refresh-2" }, authRefreshTokens);
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

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetPortfolioAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(errorJson, exception.ResponseBody);
        Assert.Equal("portfolio", exception.Endpoint);
        Assert.Contains("portfolio", exception.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetDailyTradingScheduleAsync_ErrorStatus_ThrowsBcsApiExceptionWithResponseBody()
    {
        const string errorJson = """
        {
          "timestamp": 1715000000,
          "traceId": "trace-3",
          "type": "RESOURCE_EXHAUSTED",
          "errors": [
            { "type": "RATE_LIMIT", "field": "requestsPerMinute", "payload": { "limit": 100 } }
          ],
          "displayOptions": { "retryAfter": 60 }
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.TooManyRequests, errorJson)));
        var service = new BcsTradingScheduleService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            CreateReadSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() =>
            service.GetDailyTradingScheduleAsync("TQBR", "SBER", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(errorJson, exception.ResponseBody);
        Assert.Equal("daily-trading-schedule", exception.Endpoint);
        Assert.Contains("daily-trading-schedule", exception.Message);
        Assert.Contains("Type='RESOURCE_EXHAUSTED'", exception.Message);
        Assert.Contains("TraceId='trace-3'", exception.Message);

        Assert.NotNull(exception.ApiError);
        Assert.Equal(1715000000, exception.ApiError!.Timestamp);
        Assert.Equal("trace-3", exception.ApiError.TraceId);
        Assert.Equal(BcsApiErrorTypes.ResourceExhausted, exception.ApiError.Type);
        Assert.Single(exception.ApiError.Errors);
        Assert.Equal("RATE_LIMIT", exception.ApiError.Errors[0].Type);
        Assert.Equal("requestsPerMinute", exception.ApiError.Errors[0].Field);
        Assert.NotNull(exception.ApiError.DisplayOptions);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetLimitsAsync_LegacyErrorBody_ApiErrorIsNull()
    {
        const string errorJson = """{"code":"limit_exceeded","message":"Daily limit exceeded"}""";

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.TooManyRequests, errorJson)));
        var service = new BcsLimitsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            CreateReadSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetLimitsAsync(TestContext.Current.CancellationToken));

        Assert.Null(exception.ApiError);
        Assert.Equal(errorJson, exception.ResponseBody);
    }

    // The documentation names no WWW-Authenticate challenge for a rejected token: any 401 drops it,
    // otherwise the rejected token would be sent again until it expires (24 h).
    [Fact]
    public async Task GetLimitsAsync_UnauthorizedWithoutChallengeHeader_InvalidatesAccessTokenWithoutRetry()
    {
        const string unauthorizedJson = """
        {
          "timestamp": 1777618800000,
          "traceId": "trace-401",
          "type": "UNAUTHORIZED",
          "errors": [],
          "displayOptions": {}
        }
        """;

        var settings = CreateSettings();
        var authRefreshTokens = new List<string>();
        var limitAccessTokens = new List<string?>();
        var handler = new CapturingHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri == AuthUrl)
            {
                authRefreshTokens.Add(await ReadFormValueAsync(
                    request,
                    "refresh_token",
                    cancellationToken));

                return JsonResponse(
                    HttpStatusCode.OK,
                    authRefreshTokens.Count == 1
                        ? AuthResponseJson("access-1", "runtime-refresh-2")
                        : AuthResponseJson("access-2", "runtime-refresh-3"));
            }

            if (request.RequestUri == LimitsUrl)
            {
                limitAccessTokens.Add(request.Headers.Authorization?.Parameter);

                return limitAccessTokens.Count == 1
                    ? JsonResponse(HttpStatusCode.Unauthorized, unauthorizedJson)
                    : JsonResponse(HttpStatusCode.OK, "{}");
            }

            throw new InvalidOperationException($"Unexpected request URI '{request.RequestUri}'.");
        });

        using var httpClient = new HttpClient(handler);
        await using var tokens = CreateTokenManager(settings, httpClient);
        var service = new BcsLimitsService(settings, httpClient, tokens, CreateReadSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetLimitsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(BcsApiErrorTypes.Unauthorized, exception.ApiError?.Type);
        Assert.Equal(new[] { "access-1" }, limitAccessTokens);
        Assert.Equal(2, handler.RequestCount);

        await service.GetLimitsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "access-1", "access-2" }, limitAccessTokens);
        Assert.Equal(new[] { "settings-refresh-1", "runtime-refresh-2" }, authRefreshTokens);
        Assert.Equal(4, handler.RequestCount);
    }

    [Fact]
    public async Task GetCandlesAsync_CandleLimitExceeded_IsParsedAsTheDocumentedErrorType()
    {
        const string errorJson = """
        {
          "timestamp": 1777618800000,
          "traceId": "trace-candles",
          "type": "CANDLE_LIMIT_EXCEEDED",
          "errors": [],
          "displayOptions": {}
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, errorJson)));
        var service = new BcsMarketDataService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            CreateReadSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetCandlesAsync(
            "TQBR",
            "SBER",
            new DateTimeOffset(2026, 05, 01, 7, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 05, 02, 7, 0, 0, TimeSpan.Zero),
            BcsCandleTimeFrames.Minute1,
            TestContext.Current.CancellationToken));

        Assert.Equal("CANDLE_LIMIT_EXCEEDED", BcsApiErrorTypes.CandleLimitExceeded);
        Assert.Equal(BcsApiErrorTypes.CandleLimitExceeded, exception.ApiError?.Type);
    }

    [Fact]
    public async Task GetOrderStatusAsync_UserNotFound_IsParsedAsTheDocumentedErrorType()
    {
        const string errorJson = """
        {
          "timestamp": 1777618800000,
          "traceId": "trace-user",
          "type": "USER_NOT_FOUND",
          "errors": [],
          "displayOptions": {}
        }
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.NotFound, errorJson)));
        var service = new BcsOrdersService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            CreateReadSender());

        var exception = await Assert.ThrowsAsync<BcsApiException>(() => service.GetOrderStatusAsync(
            BcsOrderIdType.ClientOrderId,
            "12345678-1234-1234-9adb-123456789876",
            TestContext.Current.CancellationToken));

        Assert.Equal("USER_NOT_FOUND", BcsApiErrorTypes.UserNotFound);
        Assert.Equal(BcsApiErrorTypes.UserNotFound, exception.ApiError?.Type);
    }

    private static IBcsHttpSender CreateReadSender() =>
        new BcsHttpRequestSender();

    private static BcsInvestApiSettings CreateSettings() =>
        new()
        {
            RefreshToken = "settings-refresh-1",
            ClientId = BcsAuthClientIds.TradeApiRead,
            AuthUrl = AuthUrl,
            BaseUrl = new Uri("https://example.test"),
        };

    private static BcsTokenManager CreateTokenManager(
        BcsInvestApiSettings settings,
        HttpClient httpClient)
    {
        var auth = BcsAuthService.ForSharedClient(httpClient, settings);
        return new BcsTokenManager(
            auth,
            settings,
            new FakeTimeProvider(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero)));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private static async Task<string> ReadFormValueAsync(
        HttpRequestMessage request,
        string key,
        CancellationToken cancellationToken)
    {
        var content = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var valuePrefix = key + "=";
        var encodedValue = content
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Single(value => value.StartsWith(valuePrefix, StringComparison.Ordinal));

        return WebUtility.UrlDecode(encodedValue[valuePrefix.Length..]) ?? string.Empty;
    }

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

        public void InvalidateAccessToken(string rejectedAccessToken)
        {
        }
    }
}
