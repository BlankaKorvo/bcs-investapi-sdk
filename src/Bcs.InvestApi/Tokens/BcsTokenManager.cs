namespace Bcs.InvestApi.Tokens;

using Bcs.InvestApi.Contracts.Auth;
using Bcs.InvestApi.Contracts.Exceptions;
using Bcs.InvestApi.Services;

internal sealed class BcsTokenManager : IBcsAccessTokenProvider, IDisposable, IAsyncDisposable
{
    private readonly BcsAuthService _authService;
    private readonly BcsInvestApiSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private BcsTokenSet? _currentTokenSet;
    private bool _disposed;

    internal BcsTokenManager(
        BcsAuthService authService,
        BcsInvestApiSettings settings,
        TimeProvider? timeProvider = null)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _timeProvider = timeProvider ?? TimeProvider.System;

        _settings.ValidateRefreshToken();
        _settings.ValidateTokenTimings();
    }

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var tokenSet = await RefreshIfRequiredAsync(cancellationToken).ConfigureAwait(false);
        return tokenSet.AccessToken;
    }

    public void InvalidateAccessToken(string rejectedAccessToken)
    {
        if (string.IsNullOrEmpty(rejectedAccessToken))
        {
            return;
        }

        while (true)
        {
            var current = GetCurrentTokenSetOrNull();
            if (current is null ||
                !string.Equals(current.AccessToken, rejectedAccessToken, StringComparison.Ordinal))
            {
                return;
            }

            var invalidated = current with
            {
                AccessTokenExpiresAtUtc = DateTimeOffset.MinValue,
            };

            if (ReferenceEquals(
                Interlocked.CompareExchange(ref _currentTokenSet, invalidated, current),
                current))
            {
                return;
            }
        }
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

    private async ValueTask<BcsTokenSet> RefreshIfRequiredAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var current = GetCurrentTokenSetOrNull();
        var nowUtc = _timeProvider.GetUtcNow();
        if (current?.HasUsableAccessToken(nowUtc, _settings.TokenRefreshSkew) == true)
        {
            return current;
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            current = GetCurrentTokenSetOrNull();
            nowUtc = _timeProvider.GetUtcNow();
            if (current?.HasUsableAccessToken(nowUtc, _settings.TokenRefreshSkew) == true)
            {
                return current;
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
        var resolvedRefreshToken = ResolveRefreshTokenForRefresh(_timeProvider.GetUtcNow());
        var source = ToPublicSource(resolvedRefreshToken.Source);

        try
        {
            var tokenSet = await ExchangeRefreshTokenAsync(resolvedRefreshToken.RefreshToken, cancellationToken).ConfigureAwait(false);
            RaiseTokenRefreshed(tokenSet, source);
            return tokenSet;
        }
        catch (Exception ex)
        {
            if (ex is BcsAuthException authException &&
                resolvedRefreshToken.Source == RefreshTokenSource.CurrentTokenSet &&
                IsInvalidGrant(authException))
            {
                Volatile.Write(ref _currentTokenSet, null);
            }

            RaiseTokenRefreshFailed(ex, source);
            throw;
        }
    }

    private void RaiseTokenRefreshed(BcsTokenSet tokenSet, BcsRefreshTokenSource source)
    {
        var callback = _settings.OnTokenRefreshed;
        if (callback is null)
        {
            return;
        }

        try
        {
            callback(new BcsTokenRefreshInfo
            {
                Source = source,
                ReceivedAtUtc = tokenSet.ReceivedAtUtc,
                AccessTokenExpiresAtUtc = tokenSet.AccessTokenExpiresAtUtc,
                RefreshTokenExpiresAtUtc = tokenSet.RefreshTokenExpiresAtUtc,
                Scope = tokenSet.Scope,
            });
        }
        catch
        {
            // Diagnostics callbacks must never break the auth flow.
        }
    }

    private void RaiseTokenRefreshFailed(Exception exception, BcsRefreshTokenSource source)
    {
        var callback = _settings.OnTokenRefreshFailed;
        if (callback is null)
        {
            return;
        }

        try
        {
            callback(new BcsTokenRefreshFailure
            {
                Source = source,
                Exception = exception,
            });
        }
        catch
        {
            // Diagnostics callbacks must never break the auth flow.
        }
    }

    private static BcsRefreshTokenSource ToPublicSource(RefreshTokenSource source) =>
        source == RefreshTokenSource.CurrentTokenSet
            ? BcsRefreshTokenSource.Rotated
            : BcsRefreshTokenSource.Bootstrap;

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

        var tokenSet = BcsTokenSet.FromAuthResponse(response, _timeProvider.GetUtcNow());
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
