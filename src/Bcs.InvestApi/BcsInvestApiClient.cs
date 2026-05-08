namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Tokens;

/// <summary>
/// Thin facade over BCS Trade API service clients.
/// Iteration 2 exposes raw authorization plus token storage/refresh management.
/// </summary>
public sealed class BcsInvestApiClient : IDisposable, IAsyncDisposable
{
    private readonly bool _ownsTokenManager;
    private readonly IDisposable? _ownedTransport;
    private bool _disposed;

    public BcsInvestApiClient(BcsAuthService auth, BcsTokenManager tokens)
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

    public BcsAuthService Auth { get; }

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
