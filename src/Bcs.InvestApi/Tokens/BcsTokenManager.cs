namespace Bcs.InvestApi.Tokens;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Time;
using Microsoft.Extensions.Options;

internal sealed class BcsTokenManager : IBcsAccessTokenProvider, IDisposable, IAsyncDisposable
{
    private readonly BcsAuthService _authService;
    private readonly BcsInvestApiSettings _settings;
    private readonly IBcsClock _clock;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private BcsTokenSet? _currentTokenSet;
    private bool _disposed;

    internal BcsTokenManager(
        BcsAuthService authService,
        IOptions<BcsInvestApiSettings> options,
        IBcsClock clock)
        : this(authService, GetSettings(options), clock)
    {
    }

    internal BcsTokenManager(
        BcsAuthService authService,
        BcsInvestApiSettings settings,
        IBcsClock? clock = null)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clock = clock ?? new BcsSystemClock();

        _settings.ValidateTokenSettings();
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

    public async ValueTask<BcsAccessTokenInfo> GetAccessTokenInfoAsync(CancellationToken cancellationToken = default)
    {
        var tokenSet = await RefreshIfRequiredAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        return tokenSet.ToAccessTokenInfo();
    }

    public ValueTask<BcsAccessTokenInfo?> GetCurrentAccessTokenInfoAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(GetCurrentTokenSetOrNull()?.ToAccessTokenInfo());
    }

    public async ValueTask<BcsAccessTokenInfo> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var tokenSet = await RefreshIfRequiredAsync(forceRefresh: true, cancellationToken).ConfigureAwait(false);
        return tokenSet.ToAccessTokenInfo();
    }

    internal ValueTask<BcsTokenSet?> GetCurrentTokenSetAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(GetCurrentTokenSetOrNull());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refreshGate.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _refreshGate.Dispose();
        return ValueTask.CompletedTask;
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
        var resolvedRefreshToken = ResolveRefreshTokenForRefresh(_clock.UtcNow);

        try
        {
            return await ExchangeRefreshTokenAsync(resolvedRefreshToken.RefreshToken, cancellationToken).ConfigureAwait(false);
        }
        catch (BcsAuthException ex) when (
            resolvedRefreshToken.Source == RefreshTokenSource.CurrentTokenSet &&
            IsInvalidGrant(ex))
        {
            Volatile.Write(ref _currentTokenSet, null);

            var settingsRefreshToken = _settings.GetRequiredRefreshToken();
            if (string.Equals(settingsRefreshToken, resolvedRefreshToken.RefreshToken, StringComparison.Ordinal))
            {
                throw;
            }

            return await ExchangeRefreshTokenAsync(settingsRefreshToken, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<BcsTokenSet> ExchangeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
        Volatile.Write(ref _currentTokenSet, tokenSet);

        return tokenSet;
    }

    private ResolvedRefreshToken ResolveRefreshTokenForRefresh(DateTimeOffset nowUtc)
    {
        var current = GetCurrentTokenSetOrNull();
        if (current?.HasUsableRefreshToken(nowUtc, _settings.TokenRefreshSkew) == true)
        {
            return new ResolvedRefreshToken(current.RefreshToken, RefreshTokenSource.CurrentTokenSet);
        }

        return new ResolvedRefreshToken(_settings.GetRequiredRefreshToken(), RefreshTokenSource.Settings);
    }

    private BcsTokenSet? GetCurrentTokenSetOrNull() =>
        Volatile.Read(ref _currentTokenSet);

    private static bool IsInvalidGrant(BcsAuthException exception) =>
        string.Equals(exception.Error, "invalid_grant", StringComparison.Ordinal);

    private static BcsInvestApiSettings GetSettings(IOptions<BcsInvestApiSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Value;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private readonly record struct ResolvedRefreshToken(
        string RefreshToken,
        RefreshTokenSource Source);

    private enum RefreshTokenSource
    {
        Settings,
        CurrentTokenSet,
    }
}
