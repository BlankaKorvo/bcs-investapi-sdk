using Bcs.InvestApi;
using Bcs.InvestApi.Auth;

var refreshToken = Environment.GetEnvironmentVariable("BCS_REFRESH_TOKEN");
if (string.IsNullOrWhiteSpace(refreshToken))
{
    Console.Error.WriteLine("Set BCS_REFRESH_TOKEN environment variable.");
    return 1;
}

await using var client = BcsInvestApiClientFactory.Create(new BcsInvestApiSettings
{
    RefreshToken = refreshToken,
    ClientId = BcsAuthClientIds.TradeApiRead,
    TokenStoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".bcs-investapi",
        "tokens.json"),
});

await client.Tokens.InitializeAsync();
var token = await client.Tokens.GetTokenSetAsync();
client.Tokens.StartAutoRefresh();

Console.WriteLine($"TokenType: {token.TokenType}");
Console.WriteLine($"ExpiresIn: {token.ExpiresIn}");
Console.WriteLine($"RefreshExpiresIn: {token.RefreshExpiresIn}");
Console.WriteLine($"Scope: {token.Scope}");
Console.WriteLine($"AccessToken length: {token.AccessToken.Length}");
Console.WriteLine($"AccessTokenExpiresAtUtc: {token.AccessTokenExpiresAtUtc:O}");
Console.WriteLine($"RefreshTokenExpiresAtUtc: {token.RefreshTokenExpiresAtUtc:O}");

return 0;
