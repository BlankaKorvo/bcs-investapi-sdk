namespace Bcs.InvestApi.Tests.DependencyInjection;

using System.Net;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Contracts.Exceptions;
using Bcs.InvestApi.DependencyInjection;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

public sealed class BcsInvestApiClientLoggingTests
{
    [Fact]
    public async Task AddBcsInvestApiClient_WhenRefreshSucceeds_LogsDebugAndPreservesUserCallback()
    {
        var capture = new CaptureLoggerProvider();
        var userCallbackInvoked = false;
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-1", "current-refresh-2"))));

        await using var provider = BuildProvider(
            handler,
            capture,
            settings => settings.OnTokenRefreshed = _ => userCallbackInvoked = true);

        var manager = provider.GetRequiredService<BcsTokenManager>();
        await manager.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Single(capture.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("refreshed", StringComparison.Ordinal));
        Assert.Contains("Bootstrap", entry.Message, StringComparison.Ordinal);
        Assert.True(userCallbackInvoked);
    }

    [Fact]
    public async Task AddBcsInvestApiClient_WhenRefreshFails_LogsErrorWithExceptionAndPreservesUserCallback()
    {
        var capture = new CaptureLoggerProvider();
        BcsTokenRefreshFailure? userFailure = null;
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, InvalidGrantJson())));

        await using var provider = BuildProvider(
            handler,
            capture,
            settings => settings.OnTokenRefreshFailed = failure => userFailure = failure);

        var manager = provider.GetRequiredService<BcsTokenManager>();

        await Assert.ThrowsAsync<BcsAuthException>(() => manager.GetAccessTokenAsync(TestContext.Current.CancellationToken).AsTask());

        var entry = Assert.Single(capture.Entries, e => e.Level == LogLevel.Error);
        Assert.NotNull(entry.Exception);
        Assert.IsType<BcsAuthException>(entry.Exception);
        Assert.NotNull(userFailure);
        Assert.Equal(BcsRefreshTokenSource.Bootstrap, userFailure.Source);
    }

    [Fact]
    public async Task AddBcsInvestApiClient_WhenNoUserCallbackConfigured_StillLogs()
    {
        var capture = new CaptureLoggerProvider();
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, AuthResponseJson("access-1", "current-refresh-2"))));

        await using var provider = BuildProvider(handler, capture, configureExtra: null);

        var manager = provider.GetRequiredService<BcsTokenManager>();
        await manager.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Contains(capture.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("refreshed", StringComparison.Ordinal));
    }

    private static ServiceProvider BuildProvider(
        CapturingHttpMessageHandler handler,
        CaptureLoggerProvider capture,
        Action<BcsInvestApiSettings>? configureExtra)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero));

        var services = new ServiceCollection();
        services.AddBcsInvestApiClient(settings =>
        {
            settings.RefreshToken = "settings-refresh-1";
            settings.ClientId = BcsAuthClientIds.TradeApiRead;
            settings.AuthUrl = new Uri("https://example.test/token");
            settings.TokenRefreshSkew = TimeSpan.FromMinutes(5);
            configureExtra?.Invoke(settings);
        });
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IHttpClientFactory>(new SingleHandlerHttpClientFactory(handler));

        var loggerFactory = new CaptureLoggerFactory(capture);
        services.AddSingleton<ILoggerFactory>(loggerFactory);

        return services.BuildServiceProvider();
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

    private static string InvalidGrantJson() =>
        """
        {
          "error": "invalid_grant",
          "error_description": "Bootstrap refresh token was rejected."
        }
        """;

    private sealed class SingleHandlerHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public SingleHandlerHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed record CaptureEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class CaptureLoggerProvider
    {
        private readonly List<CaptureEntry> _entries = [];
        private readonly object _gate = new();

        public IReadOnlyList<CaptureEntry> Entries
        {
            get
            {
                lock (_gate)
                {
                    return _entries.ToArray();
                }
            }
        }

        public void Add(CaptureEntry entry)
        {
            lock (_gate)
            {
                _entries.Add(entry);
            }
        }
    }

    private sealed class CaptureLoggerFactory : ILoggerFactory
    {
        private readonly CaptureLoggerProvider _provider;

        public CaptureLoggerFactory(CaptureLoggerProvider provider)
        {
            _provider = provider;
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(_provider);

        public void Dispose()
        {
        }
    }

    private sealed class CaptureLogger : ILogger
    {
        private readonly CaptureLoggerProvider _provider;

        public CaptureLogger(CaptureLoggerProvider provider)
        {
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _provider.Add(new CaptureEntry(logLevel, formatter(state, exception), exception));
        }
    }
}
