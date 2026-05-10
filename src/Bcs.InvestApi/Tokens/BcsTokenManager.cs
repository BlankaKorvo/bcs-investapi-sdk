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

        _settings.ValidateTokenSettings();
    }

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var tokenSet = await RefreshIfRequiredAsync(cancellationToken).ConfigureAwait(false);
        return tokenSet.AccessToken;
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

        try
        {
            return await ExchangeRefreshTokenAsync(resolvedRefreshToken.RefreshToken, cancellationToken).ConfigureAwait(false);
        }
        catch (BcsAuthException ex) when (
            resolvedRefreshToken.Source == RefreshTokenSource.CurrentTokenSet &&
            IsInvalidGrant(ex))
        {
            Volatile.Write(ref _currentTokenSet, null);
            throw;
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
