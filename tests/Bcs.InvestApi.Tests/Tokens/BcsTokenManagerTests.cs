namespace Bcs.InvestApi.Tests.Tokens;

using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Time;
using Bcs.InvestApi.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class BcsTokenManagerTests
{
    private static readonly TimeSpan AsyncGuardTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task GetCurrentTokenSetAsync_WhenNoRefreshHappened_ReturnsNull()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => throw new InvalidOperationException("Auth endpoint must not be called."));
        var manager = CreateManager(handler, clock, refreshToken: "settings-refresh-1");

        var current = await manager.GetCurrentTokenSetAsync();

        Assert.Null(current);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public void BcsTokenSet_PublicProperties_AreInitOnly()
    {
        var mutableProperties = typeof(BcsTokenSet)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.SetMethod is not null)
            .Where(property => !property.SetMethod!.ReturnParameter
                .GetRequiredCustomModifiers()
                .Contains(typeof(IsExternalInit)))
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(mutableProperties);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenNoCurrentToken_UsesSettingsRefreshTokenAndKeepsTokenInMemory()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-1", "current-refresh-2"))));
        var manager = CreateManager(handler, clock, refreshToken: "settings-refresh-1");

        var accessToken = await manager.GetAccessTokenAsync();
        var current = await manager.GetCurrentTokenSetAsync();

        Assert.Equal("access-1", accessToken);
        Assert.NotNull(current);
        Assert.Equal("current-refresh-2", current.RefreshToken);
        Assert.Equal(clock.UtcNow.AddSeconds(86400), current.AccessTokenExpiresAtUtc);
        Assert.Equal(clock.UtcNow.AddSeconds(7776000), current.RefreshTokenExpiresAtUtc);
        Assert.Contains("refresh_token=settings-refresh-1", handler.LastRequestContent);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenCurrentAccessTokenIsStillValid_DoesNotCallAuthEndpoint()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-1", "current-refresh-2"))));
        var manager = CreateManager(handler, clock, refreshToken: "settings-refresh-1");

        var firstAccessToken = await manager.GetAccessTokenAsync();
        var secondAccessToken = await manager.GetAccessTokenAsync();

        Assert.Equal("access-1", firstAccessToken);
        Assert.Equal("access-1", secondAccessToken);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenCurrentAccessTokenRequiresRefresh_UsesCurrentRefreshToken()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var responseCount = 0;
        var handler = new CapturingHttpMessageHandler((_, _) =>
        {
            var responseNumber = Interlocked.Increment(ref responseCount);

            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                responseNumber == 1
                    ? AuthResponseJson("access-1", "current-refresh-2")
                    : AuthResponseJson("access-2", "current-refresh-3")));
        });
        var manager = CreateManager(handler, clock, refreshToken: "settings-refresh-1");
        await manager.RefreshAsync();
        clock.UtcNow = clock.UtcNow.AddHours(23).AddMinutes(56);

        var accessToken = await manager.GetAccessTokenAsync();
        var current = await manager.GetCurrentTokenSetAsync();

        Assert.Equal("access-2", accessToken);
        Assert.Equal("current-refresh-3", current?.RefreshToken);
        Assert.Equal(2, handler.RequestCount);
        Assert.Contains("refresh_token=current-refresh-2", handler.LastRequestContent);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenCurrentRefreshTokenExpired_UsesSettingsRefreshToken()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var responseCount = 0;
        var handler = new CapturingHttpMessageHandler((_, _) =>
        {
            var responseNumber = Interlocked.Increment(ref responseCount);

            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                responseNumber == 1
                    ? AuthResponseJson("access-1", "expired-refresh-1", expiresIn: 10, refreshExpiresIn: 10)
                    : AuthResponseJson("access-2", "current-refresh-3")));
        });
        var manager = CreateManager(handler, clock, refreshToken: "settings-refresh-1");
        await manager.RefreshAsync();
        clock.UtcNow = clock.UtcNow.AddSeconds(11);

        var accessToken = await manager.GetAccessTokenAsync();

        Assert.Equal("access-2", accessToken);
        Assert.Equal(2, handler.RequestCount);
        Assert.Contains("refresh_token=settings-refresh-1", handler.LastRequestContent);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenCurrentRefreshTokenIsEmpty_UsesSettingsRefreshToken()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-2", "current-refresh-2"))));
        var manager = CreateManager(handler, clock, refreshToken: "settings-refresh-1");
        SetCurrentTokenSet(
            manager,
            new BcsTokenSet
            {
                AccessToken = "access-1",
                RefreshToken = string.Empty,
                TokenType = "bearer",
                ExpiresIn = 10,
                RefreshExpiresIn = 7776000,
                AccessTokenExpiresAtUtc = clock.UtcNow.AddSeconds(-1),
                RefreshTokenExpiresAtUtc = clock.UtcNow.AddDays(90),
                ReceivedAtUtc = clock.UtcNow.AddSeconds(-10),
                Scope = "trade-api-read",
                SessionState = "session-state-1",
            });

        var accessToken = await manager.GetAccessTokenAsync();
        var current = await manager.GetCurrentTokenSetAsync();

        Assert.Equal("access-2", accessToken);
        Assert.Equal("current-refresh-2", current?.RefreshToken);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("refresh_token=settings-refresh-1", handler.LastRequestContent);
    }

    [Fact]
    public async Task RefreshAsync_WhenCurrentAccessTokenIsStillValid_StillCallsAuthEndpoint()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var responseCount = 0;
        var handler = new CapturingHttpMessageHandler((_, _) =>
        {
            var responseNumber = Interlocked.Increment(ref responseCount);

            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                AuthResponseJson($"access-{responseNumber}", $"current-refresh-{responseNumber + 1}")));
        });
        var manager = CreateManager(handler, clock, refreshToken: "settings-refresh-1");

        await manager.RefreshAsync();
        var tokenSet = await manager.RefreshAsync();
        var current = await manager.GetCurrentTokenSetAsync();

        Assert.Equal("access-2", tokenSet.AccessToken);
        Assert.Equal("current-refresh-3", current?.RefreshToken);
        Assert.Equal(2, handler.RequestCount);
        Assert.Contains("refresh_token=current-refresh-2", handler.LastRequestContent);
    }

    [Fact]
    public void Constructor_WhenNoRefreshTokenIsConfigured_ThrowsInvalidOperationException()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => throw new InvalidOperationException("Auth endpoint must not be called."));

        var exception = Assert.Throws<InvalidOperationException>(() => CreateManager(handler, clock, refreshToken: null));

        Assert.Equal(
            "BCS refresh token is not configured. Set Bcs:RefreshToken or pass refresh token explicitly.",
            exception.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task RefreshAsync_WhenAuthRequestThrowsTransientException_DoesNotSetCurrentToken()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Connection reset after processing."));
        var manager = CreateManager(handler, clock, refreshToken: "settings-refresh-1");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => manager.RefreshAsync().AsTask());
        var current = await manager.GetCurrentTokenSetAsync();

        Assert.Contains("Connection reset", exception.Message);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("refresh_token=settings-refresh-1", handler.LastRequestContent);
        Assert.Null(current);
    }

    [Fact]
    public async Task RefreshAsync_WhenCancellationIsRequestedBeforeRefresh_DoesNotCallAuthEndpoint()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => throw new InvalidOperationException("Auth endpoint must not be called."));
        var manager = CreateManager(handler, clock, refreshToken: "settings-refresh-1");
        using var callerCts = new CancellationTokenSource();
        callerCts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.RefreshAsync(callerCts.Token).AsTask());
        var current = await manager.GetCurrentTokenSetAsync();

        Assert.Equal(0, handler.RequestCount);
        Assert.Null(current);
    }

    [Fact]
    public async Task RefreshAsync_WhenCallerCancelsDuringAuthRequest_PropagatesCancellation()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var requestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var authCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new CapturingHttpMessageHandler(async (_, cancellationToken) =>
        {
            var neverCompletes = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() =>
            {
                authCancellationObserved.TrySetResult();
                neverCompletes.TrySetCanceled(cancellationToken);
            });

            requestEntered.SetResult();
            return await neverCompletes.Task.ConfigureAwait(false);
        });
        var manager = CreateManager(handler, clock, refreshToken: "settings-refresh-1");
        using var callerCts = new CancellationTokenSource();

        var refreshTask = manager.RefreshAsync(callerCts.Token).AsTask();
        await requestEntered.Task.WaitAsync(AsyncGuardTimeout);

        callerCts.Cancel();
        await authCancellationObserved.Task.WaitAsync(AsyncGuardTimeout);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refreshTask.WaitAsync(AsyncGuardTimeout));
        var current = await manager.GetCurrentTokenSetAsync();

        Assert.Equal(1, handler.RequestCount);
        Assert.Null(current);
    }

    [Fact]
    public async Task RefreshAsync_WhenAuthDoesNotComplete_UsesRefreshOperationTimeout()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var authCancellationObserved = false;
        var handler = new CapturingHttpMessageHandler(async (_, cancellationToken) =>
        {
            var neverCompletes = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() =>
            {
                authCancellationObserved = true;
                neverCompletes.TrySetCanceled(cancellationToken);
            });

            return await neverCompletes.Task.ConfigureAwait(false);
        });
        var manager = CreateManager(
            handler,
            clock,
            refreshToken: "settings-refresh-1",
            tokenRefreshOperationTimeout: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.RefreshAsync().AsTask().WaitAsync(AsyncGuardTimeout));
        var current = await manager.GetCurrentTokenSetAsync();

        Assert.Equal(1, handler.RequestCount);
        Assert.True(authCancellationObserved);
        Assert.Null(current);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTenCallersRefreshConcurrently_SendsOneAuthRequest()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var requestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new CapturingHttpMessageHandler(async (_, cancellationToken) =>
        {
            requestEntered.TrySetResult();
            await releaseResponse.Task.WaitAsync(cancellationToken);

            return JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-1", "current-refresh-2"));
        });
        var manager = CreateManager(handler, clock, refreshToken: "settings-refresh-1");

        var accessTokenTasks = Enumerable
            .Range(0, 10)
            .Select(_ => manager.GetAccessTokenAsync().AsTask())
            .ToArray();
        await requestEntered.Task.WaitAsync(AsyncGuardTimeout);
        Assert.Equal(1, handler.RequestCount);

        releaseResponse.SetResult();
        var accessTokens = await Task.WhenAll(accessTokenTasks).WaitAsync(AsyncGuardTimeout);

        Assert.All(accessTokens, accessToken => Assert.Equal("access-1", accessToken));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task RefreshAsync_WhenResolvedFromDiAndCalledTwice_CreatesHttpClientPerRefreshRequest()
    {
        var refreshResponseCount = 0;
        var handlers = new List<CapturingHttpMessageHandler>();
        var httpClientFactory = new CountingHttpClientFactory(() =>
        {
            var handler = new CapturingHttpMessageHandler((_, _) =>
            {
                var responseNumber = Interlocked.Increment(ref refreshResponseCount);

                return Task.FromResult(JsonResponse(
                    HttpStatusCode.OK,
                    AuthResponseJson($"access-{responseNumber}", $"current-refresh-{responseNumber + 1}")));
            });

            handlers.Add(handler);

            return new HttpClient(handler);
        });

        var services = new ServiceCollection();
        services.AddBcsInvestApiClient(settings =>
        {
            settings.RefreshToken = "settings-refresh-1";
            settings.ClientId = BcsAuthClientIds.TradeApiRead;
            settings.AuthUrl = new Uri("https://example.test/token");
            settings.TokenRefreshSkew = TimeSpan.FromMinutes(5);
            settings.AutoRefreshInterval = TimeSpan.FromMilliseconds(50);
        });
        services.AddSingleton<IHttpClientFactory>(httpClientFactory);

        await using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<BcsTokenManager>();
        var secondManager = provider.GetRequiredService<BcsTokenManager>();
        var tokenProvider = provider.GetRequiredService<IBcsAccessTokenProvider>();
        var client = provider.GetRequiredService<BcsInvestApiClient>();

        Assert.Same(manager, secondManager);
        Assert.Same(manager, tokenProvider);
        Assert.Same(manager, client.Tokens);
        Assert.Equal(0, httpClientFactory.CreateClientCallCount);

        var first = await manager.RefreshAsync();
        var second = await tokenProvider.RefreshAsync();

        Assert.Equal("access-1", first.AccessToken);
        Assert.Equal("current-refresh-2", first.RefreshToken);
        Assert.Equal("access-2", second.AccessToken);
        Assert.Equal("current-refresh-3", second.RefreshToken);
        Assert.Equal(2, httpClientFactory.CreateClientCallCount);
        Assert.Equal(2, handlers.Count);
        Assert.Equal(2, handlers.Sum(handler => handler.RequestCount));
        Assert.All(handlers, handler => Assert.Equal(1, handler.DisposeCount));
        Assert.Contains("refresh_token=current-refresh-2", handlers[^1].LastRequestContent);
    }

    [Fact]
    public async Task StartAutoRefresh_WhenFailureSubscriberThrows_KeepsRefreshLoopRunning()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var failureObserved = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var successObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempt = 0;
        var handler = new CapturingHttpMessageHandler((_, _) =>
        {
            if (Interlocked.Increment(ref attempt) == 1)
            {
                throw new HttpRequestException("Temporary network failure.");
            }

            successObserved.TrySetResult();
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-1", "current-refresh-2")));
        });
        var manager = CreateManager(
            handler,
            clock,
            refreshToken: "settings-refresh-1",
            autoRefreshInterval: TimeSpan.FromMilliseconds(25));
        manager.AutoRefreshFailed += (_, args) =>
        {
            failureObserved.TrySetResult(args.Exception);
            throw new InvalidOperationException("Subscriber failure.");
        };

        manager.StartAutoRefresh();
        var failure = await failureObserved.Task.WaitAsync(AsyncGuardTimeout);
        Assert.IsType<HttpRequestException>(failure);
        Assert.Same(failure, manager.LastAutoRefreshException);
        Assert.True(manager.IsAutoRefreshRunning);

        await successObserved.Task.WaitAsync(AsyncGuardTimeout);
        await manager.StopAutoRefreshAsync();

        Assert.Equal(2, handler.RequestCount);
        Assert.Null(manager.LastAutoRefreshException);
        Assert.False(manager.IsAutoRefreshRunning);
    }

    [Fact]
    public async Task StartAutoRefresh_WhenAuthReturnsInvalidGrant_StopsRefreshLoop()
    {
        const string errorJson = """
        {
          "error": "invalid_grant",
          "error_description": "Refresh token expired"
        }
        """;

        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var failureObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, errorJson)));
        var manager = CreateManager(
            handler,
            clock,
            refreshToken: "settings-refresh-1",
            autoRefreshInterval: TimeSpan.FromMilliseconds(25));
        manager.AutoRefreshFailed += (_, args) => failureObserved.TrySetResult();

        manager.StartAutoRefresh();
        await failureObserved.Task.WaitAsync(AsyncGuardTimeout);
        await WaitUntilAsync(() => !manager.IsAutoRefreshRunning, TimeSpan.FromMilliseconds(200));

        var exception = Assert.IsType<BcsAuthException>(manager.LastAutoRefreshException);
        Assert.Equal("invalid_grant", exception.Error);
        Assert.Equal(1, handler.RequestCount);
        Assert.False(manager.IsAutoRefreshRunning);
    }

    [Fact]
    public void CreateFactory_WhenNoRefreshToken_ThrowsAtCreate()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BcsInvestApiClientFactory.Create(new BcsInvestApiSettings
        {
            ClientId = BcsAuthClientIds.TradeApiRead,
            AuthUrl = new Uri("https://example.test/token"),
        }));

        Assert.Equal(
            "BCS refresh token is not configured. Set Bcs:RefreshToken or pass refresh token explicitly.",
            exception.Message);
    }

    [Fact]
    public async Task CreateFactory_WithRefreshToken_CreatesClientWithoutFileStore()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-1", "current-refresh-2"))));

        await using var client = BcsInvestApiClientFactory.Create(
            new BcsInvestApiSettings
            {
                RefreshToken = "settings-refresh-1",
                ClientId = BcsAuthClientIds.TradeApiRead,
                AuthUrl = new Uri("https://example.test/token"),
            },
            handler,
            clock);

        var accessToken = await client.Tokens.GetAccessTokenAsync();
        var current = await client.Tokens.GetCurrentTokenSetAsync();

        Assert.Equal("access-1", accessToken);
        Assert.Equal("current-refresh-2", current?.RefreshToken);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("refresh_token=settings-refresh-1", handler.LastRequestContent);
    }

    private static BcsTokenManager CreateManager(
        CapturingHttpMessageHandler handler,
        FakeBcsClock clock,
        string? refreshToken,
        TimeSpan? tokenRefreshOperationTimeout = null,
        TimeSpan? autoRefreshInterval = null)
    {
        var settings = new BcsInvestApiSettings
        {
            RefreshToken = refreshToken,
            ClientId = BcsAuthClientIds.TradeApiRead,
            AuthUrl = new Uri("https://example.test/token"),
            TokenRefreshSkew = TimeSpan.FromMinutes(5),
            AutoRefreshInterval = autoRefreshInterval ?? TimeSpan.FromMilliseconds(50),
            TokenRefreshOperationTimeout = tokenRefreshOperationTimeout ?? TimeSpan.FromSeconds(60),
        };

        var auth = new BcsAuthService(new HttpClient(handler), settings);
        return new BcsTokenManager(auth, settings, clock);
    }

    private static void SetCurrentTokenSet(BcsTokenManager manager, BcsTokenSet tokenSet)
    {
        var field = typeof(BcsTokenManager).GetField("_currentTokenSet", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BcsTokenManager current token field was not found.");

        field.SetValue(manager, tokenSet);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        Assert.True(condition(), "Condition was not met before timeout.");
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private static string AuthResponseJson(
        string accessToken,
        string refreshToken,
        long expiresIn = 86400,
        long refreshExpiresIn = 7776000) =>
        $$"""
        {
          "access_token": "{{accessToken}}",
          "expires_in": {{expiresIn}},
          "refresh_expires_in": {{refreshExpiresIn}},
          "refresh_token": "{{refreshToken}}",
          "token_type": "bearer",
          "not-before-policy": "0",
          "session_state": "session-state-1",
          "scope": "trade-api-read"
        }
        """;

    private sealed class CountingHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpClient> _httpClientFactory;

        public CountingHttpClientFactory(Func<HttpClient> httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public int CreateClientCallCount { get; private set; }

        public HttpClient CreateClient(string name)
        {
            CreateClientCallCount++;
            return _httpClientFactory();
        }
    }
}
