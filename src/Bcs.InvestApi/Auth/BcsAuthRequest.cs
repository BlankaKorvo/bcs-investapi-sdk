namespace Bcs.InvestApi.Auth;

internal sealed record BcsAuthRequest
{
    public required string ClientId { get; init; }

    public required string RefreshToken { get; init; }

    public string GrantType { get; init; } = BcsGrantTypes.RefreshToken;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new ArgumentException("BCS auth client_id is required.", nameof(ClientId));
        }

        if (string.IsNullOrWhiteSpace(RefreshToken))
        {
            throw new ArgumentException("BCS refresh_token is required.", nameof(RefreshToken));
        }

        if (!string.Equals(GrantType, BcsGrantTypes.RefreshToken, StringComparison.Ordinal))
        {
            throw new ArgumentException($"BCS grant_type must be '{BcsGrantTypes.RefreshToken}'. Actual value: '{GrantType}'.", nameof(GrantType));
        }
    }
}
