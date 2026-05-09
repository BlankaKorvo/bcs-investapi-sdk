namespace Bcs.InvestApi.Tokens;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Time;
using Microsoft.Extensions.Options;

public sealed class BcsTokenManager : IBcsAccessTokenProvider, IDisposable, IAsyncDisposable
{
    private readonly BcsAuthService _authService;
    private readonly BcsInvestApiSettings _settings;
    private readonly IBcsClock _clock;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _autoRefreshGate = new();
    private BcsTokenSet? _currentTokenSet;
    private CancellationTokenSource? _autoRefreshCts;
    private Task? _autoRefreshTask;
    private bool _disposed;

    public BcsTokenManager(
        BcsAuthService authService,
        IOptions<BcsInvestApiSettings> options,
        IBcsClock clock)
        : this(authService, GetSettings(options), clock)
    {
    }

    public BcsTokenManager(
        BcsAuthService authService,
        BcsInvestApiSettings settings,
        IBcsClock? clock = null)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clock = clock ?? new BcsSystemClock();

        _settings.ValidateTokenSettings();
    }

    public event EventHandler<BcsTokenRefreshFailedEventArgs>? AutoRefreshFailed;

    public Exception? LastAutoRefreshException { get; private set; }

    public bool IsAutoRefreshRunning
    {
        get
        {
            lock (_autoRefreshGate)
            {
                return _autoRefreshTask is not null && !_autoRefreshTask.IsCompleted;
            }
        }
    }

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var current = GetCurrentTokenSetOrNull();
        var nowUtc = _clock.UtcNow;
        if (current?.HasUsableAccessToken(nowUtc, _settings.TokenRefreshSkew) == true)
        {
            return current.AccessToken;
        }

        var tokenSet = await RefreshIfRequiredAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        return tokenSet.AccessToken;
    }

    public async ValueTask<BcsTokenSet> GetTokenSetAsync(CancellationToken cancellationToken = default) =>
        await RefreshIfRequiredAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);

    public ValueTask<BcsTokenSet?> GetCurrentTokenSetAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(GetCurrentTokenSetOrNull());
    }

    public async ValueTask<BcsTokenSet> RefreshAsync(CancellationToken cancellationToken = default) =>
        await RefreshIfRequiredAsync(forceRefresh: true, cancellationToken).ConfigureAwait(false);

    public void StartAutoRefresh()
    {
        ThrowIfDisposed();

        lock (_autoRefreshGate)
        {
            if (_autoRefreshTask is not null && !_autoRefreshTask.IsCompleted)
            {
                return;
            }

            _autoRefreshCts?.Dispose();
            _autoRefreshCts = new CancellationTokenSource();
            _autoRefreshTask = RunAutoRefreshAsync(_autoRefreshCts.Token);
        }
    }

    public async ValueTask StopAutoRefreshAsync()
    {
        Task? task;
        CancellationTokenSource? cts;

        lock (_autoRefreshGate)
        {
            task = _autoRefreshTask;
            cts = _autoRefreshCts;
            _autoRefreshTask = null;
            _autoRefreshCts = null;
        }

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            cts.Dispose();
        }

        if (task is not null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal stop path.
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAutoRefreshAsync().AsTask().GetAwaiter().GetResult();

        _disposed = true;
        _refreshGate.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAutoRefreshAsync().ConfigureAwait(false);

        _disposed = true;
        _refreshGate.Dispose();
    }

    private async Task RunAutoRefreshAsync(CancellationToken cancellationToken)
    {
        if (!await TryAutoRefreshOnceAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        using var timer = new PeriodicTimer(_settings.AutoRefreshInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await TryAutoRefreshOnceAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private async Task<bool> TryAutoRefreshOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshIfRequiredAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
            LastAutoRefreshException = null;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BcsAuthException ex) when (string.Equals(ex.Error, "invalid_grant", StringComparison.Ordinal))
        {
            LastAutoRefreshException = ex;
            OnAutoRefreshFailed(ex);
            return false;
        }
        catch (Exception ex)
        {
            LastAutoRefreshException = ex;
            OnAutoRefreshFailed(ex);
            return true;
        }
    }

    private void OnAutoRefreshFailed(Exception exception)
    {
        try
        {
            AutoRefreshFailed?.Invoke(this, new BcsTokenRefreshFailedEventArgs(exception));
        }
        catch
        {
            // Event subscribers must not stop the token refresh loop.
        }
    }

    private async ValueTask<BcsTokenSet> RefreshIfRequiredAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!forceRefresh)
        {
            var current = GetCurrentTokenSetOrNull();
            var nowUtc = _clock.UtcNow;
            if (current?.HasUsableAccessToken(nowUtc, _settings.TokenRefreshSkew) == true)
            {
                return current;
            }
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh)
            {
                var current = GetCurrentTokenSetOrNull();
                var nowUtc = _clock.UtcNow;
                if (current?.HasUsableAccessToken(nowUtc, _settings.TokenRefreshSkew) == true)
                {
                    return current;
                }
            }

            return await RefreshCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async ValueTask<BcsTokenSet> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var refreshToken = ResolveRefreshTokenForRefresh(_clock.UtcNow);

        cancellationToken.ThrowIfCancellationRequested();

        using var refreshOperationCts = new CancellationTokenSource(_settings.TokenRefreshOperationTimeout);
        using var linkedRefreshCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            refreshOperationCts.Token);

        var response = await _authService.GetAccessTokenAsync(
            new BcsAuthRequest
            {
                ClientId = _settings.ClientId,
                RefreshToken = refreshToken,
                GrantType = BcsGrantTypes.RefreshToken,
            },
            linkedRefreshCts.Token).ConfigureAwait(false);

        var tokenSet = BcsTokenSet.FromAuthResponse(response, _clock.UtcNow);
        Volatile.Write(ref _currentTokenSet, tokenSet);

        return tokenSet;
    }

    private string ResolveRefreshTokenForRefresh(DateTimeOffset nowUtc)
    {
        var current = GetCurrentTokenSetOrNull();
        if (current?.HasUsableRefreshToken(nowUtc) == true)
        {
            return current.RefreshToken;
        }

        return _settings.GetRequiredRefreshToken();
    }

    private BcsTokenSet? GetCurrentTokenSetOrNull() =>
        Volatile.Read(ref _currentTokenSet);

    private static BcsInvestApiSettings GetSettings(IOptions<BcsInvestApiSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Value;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
