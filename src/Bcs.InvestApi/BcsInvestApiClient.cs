namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Tokens;

/// <summary>
/// Thin facade over BCS Trade API service clients.
/// Exposes in-memory token refresh management without exposing rotated runtime refresh tokens.
/// </summary>
public sealed class BcsInvestApiClient : IDisposable, IAsyncDisposable
{
    private readonly bool _ownsTokenManager;
    private readonly IDisposable? _ownedTransport;
    private bool _disposed;

    internal BcsInvestApiClient(BcsAuthService auth, BcsTokenManager tokens)
        : this(auth, tokens, ownsTokenManager: false, ownedTransport: null)
    {
    }

    internal BcsInvestApiClient(
        BcsAuthService auth,
        BcsTokenManager tokens,
        bool ownsTokenManager,
        IDisposable? ownedTransport)
    {
        Auth = auth ?? throw new ArgumentNullException(nameof(auth));
        Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _ownsTokenManager = ownsTokenManager;
        _ownedTransport = ownedTransport;
    }

    internal BcsAuthService Auth { get; }

    public BcsTokenManager Tokens { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsTokenManager)
        {
            Tokens.Dispose();
        }

        _ownedTransport?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsTokenManager)
        {
            await Tokens.DisposeAsync().ConfigureAwait(false);
        }

        _ownedTransport?.Dispose();
    }
}
