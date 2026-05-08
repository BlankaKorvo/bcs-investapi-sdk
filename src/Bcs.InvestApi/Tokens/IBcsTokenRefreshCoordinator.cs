namespace Bcs.InvestApi.Tokens;

public interface IBcsTokenRefreshCoordinator
{
    ValueTask<T> ExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default);
}
