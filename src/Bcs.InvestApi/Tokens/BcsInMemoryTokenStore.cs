namespace Bcs.InvestApi.Tokens;

public sealed class BcsInMemoryTokenStore : IBcsTokenStore, IBcsTokenRefreshCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AsyncLocal<int> _lockDepth = new();
    private BcsTokenSet? _tokenSet;

    public ValueTask<T> ExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default) =>
        ExecuteWithGateAsync(operation, cancellationToken);

    internal IBcsTokenRefreshCoordinator RefreshCoordinator => this;

    public ValueTask<BcsTokenSet?> LoadAsync(CancellationToken cancellationToken = default) =>
        ExecuteWithGateAsync(
            _ => ValueTask.FromResult(_tokenSet),
            cancellationToken);

    public async ValueTask SaveAsync(BcsTokenSet tokenSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenSet);

        await ExecuteWithGateAsync(
            _ =>
            {
                _tokenSet = tokenSet;
                return ValueTask.FromResult(true);
            },
            cancellationToken).ConfigureAwait(false);
    }

    internal ValueTask<BcsTokenSet?> LoadForRefreshAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_tokenSet);

    internal ValueTask EnsureCanPersistForRefreshAsync(CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    internal ValueTask SaveForRefreshAsync(
        BcsTokenSet tokenSet,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenSet);

        _tokenSet = tokenSet;
        return ValueTask.CompletedTask;
    }

    private async ValueTask<T> ExecuteWithGateAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_lockDepth.Value > 0)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _lockDepth.Value++;
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _lockDepth.Value--;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
