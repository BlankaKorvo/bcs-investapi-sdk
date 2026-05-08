namespace Bcs.InvestApi.Tokens;

using System.Text.Json;
using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Time;
using Microsoft.Extensions.Options;

public sealed class BcsTokenManager : IBcsAccessTokenProvider, IDisposable, IAsyncDisposable
{
    private const string AuthSucceededPersistenceFailureMessage =
        "BCS auth succeeded, but token persistence failed within token persistence timeout; refresh token may have rotated.";

    private readonly BcsAuthService _authService;
    private readonly IBcsTokenStore _tokenStore;
    private readonly CoordinatedTokenStoreOperations? _coordinatedTokenStoreOperations;
    private readonly BcsInvestApiSettings _settings;
    private readonly IBcsClock _clock;
    private readonly IBcsTokenRefreshCoordinator? _tokenRefreshCoordinator;
    private readonly bool _useCoordinatedTokenStoreOperations;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly SemaphoreSlim _startupPreflightGate = new(1, 1);
    private readonly object _autoRefreshGate = new();
    private CancellationTokenSource? _autoRefreshCts;
    private Task? _autoRefreshTask;
    private bool _startupPreflightCompleted;
    private bool _disposed;

    public BcsTokenManager(
        BcsAuthService authService,
        IBcsTokenStore tokenStore,
        IOptions<BcsInvestApiSettings> options,
        IBcsClock clock)
        : this(authService, tokenStore, options, clock, tokenRefreshCoordinator: null)
    {
    }

    public BcsTokenManager(
        BcsAuthService authService,
        IBcsTokenStore tokenStore,
        IOptions<BcsInvestApiSettings> options,
        IBcsClock clock,
        IBcsTokenRefreshCoordinator? tokenRefreshCoordinator)
        : this(authService, tokenStore, options.Value, clock, tokenRefreshCoordinator)
    {
    }

    public BcsTokenManager(
        BcsAuthService authService,
        IBcsTokenStore tokenStore,
        BcsInvestApiSettings settings,
        IBcsClock? clock = null)
        : this(authService, tokenStore, settings, clock, tokenRefreshCoordinator: null)
    {
    }

    public BcsTokenManager(
        BcsAuthService authService,
        IBcsTokenStore tokenStore,
        BcsInvestApiSettings settings,
        IBcsClock? clock,
        IBcsTokenRefreshCoordinator? tokenRefreshCoordinator)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clock = clock ?? new BcsSystemClock();
        _coordinatedTokenStoreOperations = GetBuiltInTokenStoreOperations(tokenStore);
        var implicitCoordinator = _coordinatedTokenStoreOperations?.RefreshCoordinator;
        _tokenRefreshCoordinator = tokenRefreshCoordinator ?? implicitCoordinator;
        _useCoordinatedTokenStoreOperations =
            _coordinatedTokenStoreOperations is not null &&
            ReferenceEquals(_tokenRefreshCoordinator, implicitCoordinator);

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

    /// <summary>
    /// Explicitly validates that startup token source is available without doing I/O in the constructor.
    /// </summary>
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (Volatile.Read(ref _startupPreflightCompleted))
        {
            return;
        }

        await _startupPreflightGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _startupPreflightCompleted))
            {
                return;
            }

            await BcsTokenSourcePreflight.EnsureStartupTokenSourceAsync(
                _settings,
                _tokenStore,
                _clock,
                cancellationToken).ConfigureAwait(false);

            Volatile.Write(ref _startupPreflightCompleted, true);
        }
        finally
        {
            _startupPreflightGate.Release();
        }
    }

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var tokenSet = await GetTokenSetAsync(cancellationToken).ConfigureAwait(false);
        return tokenSet.AccessToken;
    }

    public async ValueTask<BcsTokenSet> GetTokenSetAsync(CancellationToken cancellationToken = default) =>
        await RefreshIfRequiredAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);

    public ValueTask<BcsTokenSet?> GetStoredTokenSetAsync(CancellationToken cancellationToken = default) =>
        _tokenStore.LoadAsync(cancellationToken);

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
        _startupPreflightGate.Dispose();
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
        _startupPreflightGate.Dispose();
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

        if (_tokenRefreshCoordinator is not null)
        {
            return await _tokenRefreshCoordinator.ExecuteAsync(
                ct => RefreshIfRequiredCoreAsync(forceRefresh, ct),
                cancellationToken).ConfigureAwait(false);
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RefreshIfRequiredCoreAsync(forceRefresh, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async ValueTask<BcsTokenSet> RefreshIfRequiredCoreAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var stored = await LoadTokenSetForRefreshAsyncOrRecoverCorruptedStorageAsync(cancellationToken).ConfigureAwait(false);
        var nowUtc = _clock.UtcNow;
        var hasUsableStoredRefreshToken = stored?.HasUsableRefreshToken(nowUtc) == true;

        if (!forceRefresh &&
            stored is not null &&
            hasUsableStoredRefreshToken &&
            stored.HasUsableAccessToken(nowUtc, _settings.TokenRefreshSkew))
        {
            return stored;
        }

        var refreshToken = ResolveRefreshTokenForRefresh(stored, nowUtc);

        await EnsureTokenStoreCanPersistAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        // From this point the refresh exchange may rotate the refresh token.
        using var refreshOperationCts = new CancellationTokenSource(_settings.TokenRefreshOperationTimeout);

        var response = await _authService.GetAccessTokenAsync(
            new BcsAuthRequest
            {
                ClientId = _settings.ClientId,
                RefreshToken = refreshToken,
                GrantType = BcsGrantTypes.RefreshToken,
            },
            refreshOperationCts.Token).ConfigureAwait(false);

        var tokenSet = BcsTokenSet.FromAuthResponse(response, _clock.UtcNow);

        try
        {
            // After auth succeeds, cancellation must not interrupt persistence of a rotated refresh token.
            using var persistenceCts = new CancellationTokenSource(_settings.TokenPersistenceTimeout);
            await SaveTokenSetForRefreshAsync(tokenSet, persistenceCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new BcsTokenPersistenceException(AuthSucceededPersistenceFailureMessage, ex);
        }

        return tokenSet;
    }

    private string ResolveRefreshTokenForRefresh(BcsTokenSet? stored, DateTimeOffset nowUtc)
    {
        if (stored is null)
        {
            return _settings.GetRequiredRefreshToken();
        }

        if (stored.HasUsableRefreshToken(nowUtc))
        {
            return stored.RefreshToken;
        }

        if (!string.IsNullOrWhiteSpace(_settings.RefreshToken))
        {
            return _settings.RefreshToken;
        }

        if (string.IsNullOrWhiteSpace(stored.RefreshToken))
        {
            stored.ValidateStoredRefreshToken(nowUtc);
        }

        throw new BcsRefreshTokenExpiredException(stored.RefreshTokenExpiresAtUtc);
    }

    private async ValueTask EnsureTokenStoreCanPersistAsync(CancellationToken cancellationToken)
    {
        var preflight = GetTokenStorePreflight();
        if (preflight is null)
        {
            return;
        }

        try
        {
            await preflight(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BcsTokenPersistenceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BcsTokenPersistenceException(
                "BCS token persistence preflight failed. Auth refresh was not attempted.",
                ex);
        }
    }

    private ValueTask<BcsTokenSet?> LoadTokenSetForRefreshAsync(CancellationToken cancellationToken) =>
        _useCoordinatedTokenStoreOperations
            ? _coordinatedTokenStoreOperations!.LoadAsync(cancellationToken)
            : _tokenStore.LoadAsync(cancellationToken);

    private async ValueTask<BcsTokenSet?> LoadTokenSetForRefreshAsyncOrRecoverCorruptedStorageAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await LoadTokenSetForRefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException) when (
            !string.IsNullOrWhiteSpace(_settings.RefreshToken) &&
            _tokenStore is IBcsTokenStoreCorruptionRecovery recovery)
        {
            await recovery.BackupCorruptedTokenStorageAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private ValueTask SaveTokenSetForRefreshAsync(BcsTokenSet tokenSet, CancellationToken cancellationToken) =>
        _useCoordinatedTokenStoreOperations
            ? _coordinatedTokenStoreOperations!.SaveAsync(tokenSet, cancellationToken)
            : _tokenStore.SaveAsync(tokenSet, cancellationToken);

    private Func<CancellationToken, ValueTask>? GetTokenStorePreflight()
    {
        if (_useCoordinatedTokenStoreOperations)
        {
            return _coordinatedTokenStoreOperations!.EnsureCanPersistAsync;
        }

        return _tokenStore is IBcsTokenStorePreflight preflight
            ? preflight.EnsureCanPersistAsync
            : null;
    }

    private static CoordinatedTokenStoreOperations? GetBuiltInTokenStoreOperations(IBcsTokenStore tokenStore) =>
        tokenStore switch
        {
            BcsInMemoryTokenStore inMemoryStore => new CoordinatedTokenStoreOperations(
                inMemoryStore.RefreshCoordinator,
                inMemoryStore.LoadForRefreshAsync,
                inMemoryStore.EnsureCanPersistForRefreshAsync,
                inMemoryStore.SaveForRefreshAsync),
            BcsFileTokenStore fileStore => new CoordinatedTokenStoreOperations(
                fileStore.RefreshCoordinator,
                fileStore.LoadForRefreshAsync,
                fileStore.EnsureCanPersistForRefreshAsync,
                fileStore.SaveForRefreshAsync),
            _ => null,
        };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class CoordinatedTokenStoreOperations
    {
        public CoordinatedTokenStoreOperations(
            IBcsTokenRefreshCoordinator refreshCoordinator,
            Func<CancellationToken, ValueTask<BcsTokenSet?>> loadAsync,
            Func<CancellationToken, ValueTask> ensureCanPersistAsync,
            Func<BcsTokenSet, CancellationToken, ValueTask> saveAsync)
        {
            RefreshCoordinator = refreshCoordinator ?? throw new ArgumentNullException(nameof(refreshCoordinator));
            LoadAsync = loadAsync ?? throw new ArgumentNullException(nameof(loadAsync));
            EnsureCanPersistAsync = ensureCanPersistAsync ?? throw new ArgumentNullException(nameof(ensureCanPersistAsync));
            SaveAsync = saveAsync ?? throw new ArgumentNullException(nameof(saveAsync));
        }

        public IBcsTokenRefreshCoordinator RefreshCoordinator { get; }

        public Func<CancellationToken, ValueTask<BcsTokenSet?>> LoadAsync { get; }

        public Func<CancellationToken, ValueTask> EnsureCanPersistAsync { get; }

        public Func<BcsTokenSet, CancellationToken, ValueTask> SaveAsync { get; }
    }
}
