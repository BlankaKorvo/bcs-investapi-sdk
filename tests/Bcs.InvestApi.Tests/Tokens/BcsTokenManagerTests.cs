namespace Bcs.InvestApi.Tests.Tokens;

using System.Net;
using System.Text.Json;
using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Time;
using Bcs.InvestApi.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class BcsTokenManagerTests
{
    [Fact]
    public async Task GetAccessTokenAsync_WhenStoreIsEmpty_UsesSettingsRefreshTokenAndSavesRotatedTokenPair()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-1", "refresh-2"))));
        var store = new BcsInMemoryTokenStore();
        var manager = CreateManager(handler, store, clock, refreshToken: "refresh-1");

        var accessToken = await manager.GetAccessTokenAsync();
        var stored = await store.LoadAsync();

        Assert.Equal("access-1", accessToken);
        Assert.NotNull(stored);
        Assert.Equal("refresh-2", stored.RefreshToken);
        Assert.Equal(clock.UtcNow.AddSeconds(86400), stored.AccessTokenExpiresAtUtc);
        Assert.Equal(clock.UtcNow.AddSeconds(7776000), stored.RefreshTokenExpiresAtUtc);
        Assert.Contains("refresh_token=refresh-1", handler.LastRequestContent);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenStoredAccessTokenIsStillValid_DoesNotCallAuthEndpoint()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => throw new InvalidOperationException("Auth endpoint must not be called."));
        var store = new BcsInMemoryTokenStore();
        await store.SaveAsync(new BcsTokenSet
        {
            AccessToken = "stored-access",
            RefreshToken = "stored-refresh",
            TokenType = "bearer",
            ExpiresIn = 86400,
            RefreshExpiresIn = 7776000,
            ReceivedAtUtc = clock.UtcNow,
            AccessTokenExpiresAtUtc = clock.UtcNow.AddHours(1),
            RefreshTokenExpiresAtUtc = clock.UtcNow.AddDays(30),
        });

        var manager = CreateManager(handler, store, clock, refreshToken: "settings-refresh");

        var accessToken = await manager.GetAccessTokenAsync();

        Assert.Equal("stored-access", accessToken);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenStoredAccessTokenRequiresRefresh_UsesStoredRefreshToken()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-2", "refresh-3"))));
        var store = new BcsInMemoryTokenStore();
        await store.SaveAsync(new BcsTokenSet
        {
            AccessToken = "old-access",
            RefreshToken = "stored-refresh-2",
            TokenType = "bearer",
            ExpiresIn = 86400,
            RefreshExpiresIn = 7776000,
            ReceivedAtUtc = clock.UtcNow.AddHours(-23),
            AccessTokenExpiresAtUtc = clock.UtcNow.AddMinutes(4),
            RefreshTokenExpiresAtUtc = clock.UtcNow.AddDays(30),
        });

        var manager = CreateManager(handler, store, clock, refreshToken: "settings-refresh-1");

        var accessToken = await manager.GetAccessTokenAsync();
        var stored = await store.LoadAsync();

        Assert.Equal("access-2", accessToken);
        Assert.Equal("refresh-3", stored?.RefreshToken);
        Assert.Contains("refresh_token=stored-refresh-2", handler.LastRequestContent);
    }

    [Fact]
    public async Task RefreshAsync_WhenTokenStorePreflightFails_DoesNotCallAuthEndpoint()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => throw new InvalidOperationException("Auth endpoint must not be called."));
        var store = new PreflightFailingTokenStore(new BcsTokenSet
        {
            AccessToken = "old-access",
            RefreshToken = "stored-refresh-2",
            TokenType = "bearer",
            ExpiresIn = 86400,
            RefreshExpiresIn = 7776000,
            ReceivedAtUtc = clock.UtcNow.AddHours(-23),
            AccessTokenExpiresAtUtc = clock.UtcNow.AddMinutes(4),
            RefreshTokenExpiresAtUtc = clock.UtcNow.AddDays(30),
        });

        var manager = CreateManager(handler, store, clock, refreshToken: "settings-refresh-1");

        var exception = await Assert.ThrowsAsync<BcsTokenPersistenceException>(() => manager.RefreshAsync().AsTask());

        Assert.Contains("Auth refresh was not attempted", exception.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task RefreshAsync_WhenSaveFailsAfterAuth_ThrowsPersistenceExceptionWithoutTokens()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-1", "rotated-refresh-secret"))));
        var store = new SaveFailingTokenStore();
        var manager = CreateManager(handler, store, clock, refreshToken: "initial-refresh-secret");

        var exception = await Assert.ThrowsAsync<BcsTokenPersistenceException>(() => manager.RefreshAsync().AsTask());

        Assert.Contains("auth succeeded, but token persistence failed", exception.Message);
        Assert.Contains("refresh token may have rotated", exception.Message);
        Assert.DoesNotContain("initial-refresh-secret", exception.ToString());
        Assert.DoesNotContain("rotated-refresh-secret", exception.ToString());
        Assert.Equal(1, handler.RequestCount);
        Assert.True(store.SaveAttempted);
    }

    [Fact]
    public async Task RefreshAsync_WhenSaveDoesNotCompleteAfterAuth_ThrowsPersistenceExceptionAfterTimeout()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-1", "rotated-refresh-secret"))));
        var store = new HangingSaveTokenStore();
        var manager = CreateManager(
            handler,
            store,
            clock,
            refreshToken: "initial-refresh-secret",
            tokenPersistenceTimeout: TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAsync<BcsTokenPersistenceException>(
            () => manager.RefreshAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Contains("auth succeeded, but token persistence failed", exception.Message);
        Assert.Contains("refresh token may have rotated", exception.Message);
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        Assert.Equal(1, handler.RequestCount);
        Assert.True(store.SaveAttempted);
        Assert.True(store.SaveCancellationObserved);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTwoManagersShareFilePath_SerializesRefreshOperation()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var filePath = Path.Combine(Path.GetTempPath(), "bcs-token-manager-tests", $"{Guid.NewGuid():N}.json");
        var initialStore = new BcsFileTokenStore(filePath);
        await initialStore.SaveAsync(new BcsTokenSet
        {
            AccessToken = "old-access",
            RefreshToken = "stored-refresh-2",
            TokenType = "bearer",
            ExpiresIn = 86400,
            RefreshExpiresIn = 7776000,
            ReceivedAtUtc = clock.UtcNow.AddHours(-23),
            AccessTokenExpiresAtUtc = clock.UtcNow.AddMinutes(4),
            RefreshTokenExpiresAtUtc = clock.UtcNow.AddDays(30),
        });

        var refreshRequestCount = 0;
        var firstRequestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRequestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new CapturingHttpMessageHandler(async (_, cancellationToken) =>
        {
            var requestNumber = Interlocked.Increment(ref refreshRequestCount);
            if (requestNumber == 1)
            {
                firstRequestEntered.TrySetResult();
                await releaseFirstResponse.Task.WaitAsync(cancellationToken);
            }
            else
            {
                secondRequestEntered.TrySetResult();
            }

            return JsonResponse(HttpStatusCode.OK, AuthResponseJson($"access-{requestNumber}", $"refresh-{requestNumber + 1}"));
        });

        var manager1 = CreateManager(handler, new BcsFileTokenStore(filePath), clock, refreshToken: "settings-refresh-1");
        var manager2 = CreateManager(handler, new BcsFileTokenStore(filePath), clock, refreshToken: "settings-refresh-1");

        var firstTask = manager1.GetAccessTokenAsync().AsTask();
        await firstRequestEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondTask = manager2.GetAccessTokenAsync().AsTask();
        var secondRefreshStartedBeforeRelease = await Task.WhenAny(
            secondRequestEntered.Task,
            Task.Delay(TimeSpan.FromMilliseconds(300))) == secondRequestEntered.Task;

        releaseFirstResponse.SetResult();
        var accessTokens = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(secondRefreshStartedBeforeRelease);
        Assert.Equal(new[] { "access-1", "access-1" }, accessTokens);
        Assert.Equal(1, Volatile.Read(ref refreshRequestCount));
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTwoManagersShareInMemoryStore_SerializesRefreshOperation()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var store = new BcsInMemoryTokenStore();
        await store.SaveAsync(new BcsTokenSet
        {
            AccessToken = "old-access",
            RefreshToken = "stored-refresh-2",
            TokenType = "bearer",
            ExpiresIn = 86400,
            RefreshExpiresIn = 7776000,
            ReceivedAtUtc = clock.UtcNow.AddHours(-23),
            AccessTokenExpiresAtUtc = clock.UtcNow.AddMinutes(4),
            RefreshTokenExpiresAtUtc = clock.UtcNow.AddDays(30),
        });

        var refreshRequestCount = 0;
        var firstRequestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRequestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new CapturingHttpMessageHandler(async (_, cancellationToken) =>
        {
            var requestNumber = Interlocked.Increment(ref refreshRequestCount);
            if (requestNumber == 1)
            {
                firstRequestEntered.TrySetResult();
                await releaseFirstResponse.Task.WaitAsync(cancellationToken);
            }
            else
            {
                secondRequestEntered.TrySetResult();
            }

            return JsonResponse(HttpStatusCode.OK, AuthResponseJson($"access-{requestNumber}", $"refresh-{requestNumber + 1}"));
        });

        var manager1 = CreateManager(handler, store, clock, refreshToken: "settings-refresh-1");
        var manager2 = CreateManager(handler, store, clock, refreshToken: "settings-refresh-1");

        var firstTask = manager1.GetAccessTokenAsync().AsTask();
        await firstRequestEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondTask = manager2.GetAccessTokenAsync().AsTask();
        var secondRefreshStartedBeforeRelease = await Task.WhenAny(
            secondRequestEntered.Task,
            Task.Delay(TimeSpan.FromMilliseconds(300))) == secondRequestEntered.Task;

        releaseFirstResponse.SetResult();
        var accessTokens = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(secondRefreshStartedBeforeRelease);
        Assert.Equal(new[] { "access-1", "access-1" }, accessTokens);
        Assert.Equal(1, Volatile.Read(ref refreshRequestCount));
    }

    [Fact]
    public async Task RefreshAsync_WhenCustomStoreAlsoImplementsCoordinator_DoesNotUseCoordinatorImplicitly()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var handler = new CapturingHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-1", "refresh-2"))));
        var store = new NonReentrantCoordinatedTokenStore();
        var manager = CreateManager(handler, store, clock, refreshToken: "refresh-1");

        var tokenSet = await manager.RefreshAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var stored = await store.LoadAsync();

        Assert.Equal("access-1", tokenSet.AccessToken);
        Assert.Equal("refresh-2", stored?.RefreshToken);
        Assert.Equal(0, store.ExecuteCallCount);
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
                    AuthResponseJson($"access-{responseNumber}", $"refresh-{responseNumber + 1}")));
            });

            handlers.Add(handler);

            return new HttpClient(handler);
        });

        var services = new ServiceCollection();
        services.AddBcsInvestApiClient(settings =>
        {
            settings.RefreshToken = "refresh-1";
            settings.ClientId = BcsAuthClientIds.TradeApiRead;
            settings.AuthUrl = new Uri("https://example.test/token");
            settings.TokenRefreshSkew = TimeSpan.FromMinutes(5);
            settings.AutoRefreshInterval = TimeSpan.FromMilliseconds(50);
        });

        services.AddSingleton<IHttpClientFactory>(httpClientFactory);

        await using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<BcsTokenManager>();

        Assert.Equal(0, httpClientFactory.CreateClientCallCount);

        var first = await manager.RefreshAsync();
        var second = await manager.RefreshAsync();

        Assert.Equal("access-1", first.AccessToken);
        Assert.Equal("refresh-2", first.RefreshToken);
        Assert.Equal("access-2", second.AccessToken);
        Assert.Equal("refresh-3", second.RefreshToken);
        Assert.Equal(2, httpClientFactory.CreateClientCallCount);
        Assert.Equal(2, handlers.Count);
        Assert.Equal(2, handlers.Sum(handler => handler.RequestCount));
        Assert.All(handlers, handler => Assert.Equal(1, handler.DisposeCount));
        Assert.Contains("refresh_token=refresh-2", handlers[^1].LastRequestContent);
    }

    [Fact]
    public async Task StartAutoRefresh_WhenFailureSubscriberThrows_KeepsRefreshLoopRunning()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var authException = new InvalidOperationException("Auth endpoint failed.");
        var handler = new CapturingHttpMessageHandler((_, _) => throw authException);
        var store = new BcsInMemoryTokenStore();
        var manager = CreateManager(handler, store, clock, refreshToken: "refresh-1");
        var failureNotifications = 0;
        var secondFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        manager.AutoRefreshFailed += (_, _) =>
        {
            if (Interlocked.Increment(ref failureNotifications) >= 2)
            {
                secondFailure.TrySetResult();
            }

            throw new InvalidOperationException("Subscriber failed.");
        };

        manager.StartAutoRefresh();

        await secondFailure.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(manager.IsAutoRefreshRunning);
        Assert.Same(authException, manager.LastAutoRefreshException);
        Assert.True(handler.RequestCount >= 2);

        await manager.StopAutoRefreshAsync();
    }

    [Fact]
    public void GetTokenManagerFromDi_WhenNoRefreshTokenAndNoSavedFile_ThrowsAtResolve()
    {
        var services = new ServiceCollection();
        services.AddBcsInvestApiClient(settings =>
        {
            settings.ClientId = BcsAuthClientIds.TradeApiRead;
            settings.AuthUrl = new Uri("https://example.test/token");
        });

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<BcsTokenManager>());

        Assert.Contains("refresh token is not configured", exception.Message);
        Assert.Contains("token storage does not contain saved tokens", exception.Message);
    }

    [Fact]
    public async Task GetTokenManagerFromDi_WhenSavedFileExistsWithoutRefreshToken_UsesStoredToken()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var filePath = Path.Combine(Path.GetTempPath(), "bcs-token-manager-tests", $"{Guid.NewGuid():N}.json");
        var store = new BcsFileTokenStore(filePath);
        await store.SaveAsync(new BcsTokenSet
        {
            AccessToken = "stored-access",
            RefreshToken = "stored-refresh",
            TokenType = "bearer",
            ExpiresIn = 86400,
            RefreshExpiresIn = 7776000,
            ReceivedAtUtc = clock.UtcNow,
            AccessTokenExpiresAtUtc = clock.UtcNow.AddHours(1),
            RefreshTokenExpiresAtUtc = clock.UtcNow.AddDays(30),
        });

        var services = new ServiceCollection();
        services.AddBcsInvestApiClient(settings =>
        {
            settings.ClientId = BcsAuthClientIds.TradeApiRead;
            settings.AuthUrl = new Uri("https://example.test/token");
            settings.TokenStoragePath = filePath;
        });
        services.AddSingleton<IBcsClock>(clock);

        await using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<BcsTokenManager>();

        var tokenSet = await manager.GetTokenSetAsync();

        Assert.Equal("stored-access", tokenSet.AccessToken);
        Assert.Equal("stored-refresh", tokenSet.RefreshToken);
    }

    [Fact]
    public void GetTokenManagerFromDi_WhenSavedFileContainsInvalidJson_ThrowsAtResolve()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "bcs-token-manager-tests", $"{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "{ not-json");

        var services = new ServiceCollection();
        services.AddBcsInvestApiClient(settings =>
        {
            settings.ClientId = BcsAuthClientIds.TradeApiRead;
            settings.AuthUrl = new Uri("https://example.test/token");
            settings.TokenStoragePath = filePath;
        });

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<BcsTokenManager>());

        Assert.Contains("saved token storage could not be loaded", exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public void GetTokenManagerFromDi_WhenSavedFileIsEmpty_ThrowsAtResolve()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "bcs-token-manager-tests", $"{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, string.Empty);

        var services = new ServiceCollection();
        services.AddBcsInvestApiClient(settings =>
        {
            settings.ClientId = BcsAuthClientIds.TradeApiRead;
            settings.AuthUrl = new Uri("https://example.test/token");
            settings.TokenStoragePath = filePath;
        });

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<BcsTokenManager>());

        Assert.Contains("refresh token is not configured", exception.Message);
        Assert.Contains("token storage does not contain saved tokens", exception.Message);
    }

    [Fact]
    public async Task GetTokenManagerFromDi_WhenSavedFileHasEmptyRefreshToken_ThrowsAtResolve()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var filePath = Path.Combine(Path.GetTempPath(), "bcs-token-manager-tests", $"{Guid.NewGuid():N}.json");
        var store = new BcsFileTokenStore(filePath);
        await store.SaveAsync(new BcsTokenSet
        {
            AccessToken = "stored-access",
            RefreshToken = "",
            TokenType = "bearer",
            ExpiresIn = 86400,
            RefreshExpiresIn = 7776000,
            ReceivedAtUtc = clock.UtcNow,
            AccessTokenExpiresAtUtc = clock.UtcNow.AddHours(1),
            RefreshTokenExpiresAtUtc = clock.UtcNow.AddDays(30),
        });

        var services = new ServiceCollection();
        services.AddBcsInvestApiClient(settings =>
        {
            settings.ClientId = BcsAuthClientIds.TradeApiRead;
            settings.AuthUrl = new Uri("https://example.test/token");
            settings.TokenStoragePath = filePath;
        });
        services.AddSingleton<IBcsClock>(clock);

        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<BcsTokenManager>());

        Assert.Contains("empty refresh token", exception.Message);
    }

    [Fact]
    public void CreateFactory_WhenNoRefreshTokenAndNoSavedFile_ThrowsAtCreate()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BcsInvestApiClientFactory.Create(new BcsInvestApiSettings
            {
                ClientId = BcsAuthClientIds.TradeApiRead,
                AuthUrl = new Uri("https://example.test/token"),
            }));

        Assert.Contains("refresh token is not configured", exception.Message);
        Assert.Contains("token storage does not contain saved tokens", exception.Message);
    }

    [Fact]
    public async Task CreateFactory_WhenSavedFileHasExpiredRefreshToken_ThrowsAtCreate()
    {
        var clock = new FakeBcsClock(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));
        var filePath = Path.Combine(Path.GetTempPath(), "bcs-token-manager-tests", $"{Guid.NewGuid():N}.json");
        var store = new BcsFileTokenStore(filePath);
        await store.SaveAsync(new BcsTokenSet
        {
            AccessToken = "stored-access",
            RefreshToken = "stored-refresh",
            TokenType = "bearer",
            ExpiresIn = 86400,
            RefreshExpiresIn = 7776000,
            ReceivedAtUtc = clock.UtcNow.AddDays(-31),
            AccessTokenExpiresAtUtc = clock.UtcNow.AddDays(-30),
            RefreshTokenExpiresAtUtc = clock.UtcNow.AddSeconds(-1),
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            BcsInvestApiClientFactory.Create(
                new BcsInvestApiSettings
                {
                    ClientId = BcsAuthClientIds.TradeApiRead,
                    AuthUrl = new Uri("https://example.test/token"),
                    TokenStoragePath = filePath,
                },
                clock: clock));

        Assert.Contains("saved refresh token is expired", exception.Message);
    }

    private static BcsTokenManager CreateManager(
        CapturingHttpMessageHandler handler,
        IBcsTokenStore store,
        FakeBcsClock clock,
        string refreshToken,
        TimeSpan? tokenPersistenceTimeout = null)
    {
        var settings = new BcsInvestApiSettings
        {
            RefreshToken = refreshToken,
            ClientId = BcsAuthClientIds.TradeApiRead,
            AuthUrl = new Uri("https://example.test/token"),
            TokenRefreshSkew = TimeSpan.FromMinutes(5),
            AutoRefreshInterval = TimeSpan.FromMilliseconds(50),
            TokenPersistenceTimeout = tokenPersistenceTimeout ?? TimeSpan.FromSeconds(30),
        };

        var auth = new BcsAuthService(new HttpClient(handler), settings);
        return new BcsTokenManager(auth, store, settings, clock);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private static string AuthResponseJson(string accessToken, string refreshToken) =>
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

    private sealed class PreflightFailingTokenStore : IBcsTokenStore, IBcsTokenStorePreflight
    {
        private readonly BcsTokenSet? _tokenSet;

        public PreflightFailingTokenStore(BcsTokenSet? tokenSet)
        {
            _tokenSet = tokenSet;
        }

        public ValueTask<BcsTokenSet?> LoadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_tokenSet);

        public ValueTask SaveAsync(BcsTokenSet tokenSet, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask EnsureCanPersistAsync(CancellationToken cancellationToken = default) =>
            throw new UnauthorizedAccessException("Token storage path is not writable.");
    }

    private sealed class SaveFailingTokenStore : IBcsTokenStore
    {
        public bool SaveAttempted { get; private set; }

        public ValueTask<BcsTokenSet?> LoadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<BcsTokenSet?>(null);

        public ValueTask SaveAsync(BcsTokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            SaveAttempted = true;
            throw new IOException("Disk is full.");
        }
    }

    private sealed class HangingSaveTokenStore : IBcsTokenStore
    {
        public bool SaveAttempted { get; private set; }

        public bool SaveCancellationObserved { get; private set; }

        public ValueTask<BcsTokenSet?> LoadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<BcsTokenSet?>(null);

        public async ValueTask SaveAsync(BcsTokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            SaveAttempted = true;

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SaveCancellationObserved = true;
                throw;
            }
        }
    }

    private sealed class NonReentrantCoordinatedTokenStore : IBcsTokenStore, IBcsTokenRefreshCoordinator
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private BcsTokenSet? _tokenSet;

        public int ExecuteCallCount { get; private set; }

        public async ValueTask<T> ExecuteAsync<T>(
            Func<CancellationToken, ValueTask<T>> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);

            ExecuteCallCount++;
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async ValueTask<BcsTokenSet?> LoadAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return _tokenSet;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async ValueTask SaveAsync(BcsTokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _tokenSet = tokenSet;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
