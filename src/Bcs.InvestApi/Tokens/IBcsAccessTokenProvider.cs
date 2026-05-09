namespace Bcs.InvestApi.Tokens;

internal interface IBcsAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
