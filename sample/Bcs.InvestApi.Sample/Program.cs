using Bcs.InvestApi;
using Bcs.InvestApi.Auth;

var refreshToken = Environment.GetEnvironmentVariable("BCS_REFRESH_TOKEN");
if (string.IsNullOrWhiteSpace(refreshToken))
{
    Console.Error.WriteLine("Set BCS_REFRESH_TOKEN environment variable to your stable refresh/bootstrap secret.");
    return 1;
}

await using var client = BcsInvestApiClientFactory.Create(
    refreshToken: refreshToken,
    clientId: BcsAuthClientIds.TradeApiRead);

var accessToken = await client.Tokens.GetAccessTokenAsync();
client.Tokens.StartAutoRefresh();

Console.WriteLine($"AccessToken: {MaskToken(accessToken)}");
Console.WriteLine($"AutoRefreshRunning: {client.Tokens.IsAutoRefreshRunning}");

var limits = await client.GetLimitsAsync();
Console.WriteLine($"Depo limits: {limits.DepoLimit.Count}");
Console.WriteLine($"Future holdings: {limits.FutureHolding.Count}");
Console.WriteLine($"Money limits: {limits.MoneyLimits.Count}");
Console.WriteLine($"Futures limits: {limits.FuturesLimits.Count}");

var portfolio = await client.GetPortfolioAsync();
Console.WriteLine($"Portfolio positions: {portfolio.Count}");

await client.Tokens.StopAutoRefreshAsync();

return 0;

static string MaskToken(string token)
{
    if (string.IsNullOrEmpty(token))
    {
        return "<empty>";
    }

    if (token.Length <= 12)
    {
        return $"{token[0]}***{token[^1]}";
    }

    return $"{token[..6]}...{token[^6..]}";
}
