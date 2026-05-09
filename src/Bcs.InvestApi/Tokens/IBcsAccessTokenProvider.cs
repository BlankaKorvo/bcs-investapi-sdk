namespace Bcs.InvestApi.Tokens;

public interface IBcsAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    ValueTask<BcsTokenSet> GetTokenSetAsync(CancellationToken cancellationToken = default);

    ValueTask<BcsTokenSet?> GetCurrentTokenSetAsync(CancellationToken cancellationToken = default);

    ValueTask<BcsTokenSet> RefreshAsync(CancellationToken cancellationToken = default);
}
