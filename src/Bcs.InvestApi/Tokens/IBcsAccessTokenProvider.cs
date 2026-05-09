namespace Bcs.InvestApi.Tokens;

public interface IBcsAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
