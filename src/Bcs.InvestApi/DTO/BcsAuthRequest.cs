namespace Bcs.InvestApi.DTO;

using Bcs.InvestApi.DTO.Enums;

internal sealed record BcsAuthRequest
{
    public required BcsAuthClientIds ClientId { get; init; }

    public required string RefreshToken { get; init; }

    public string GrantType { get; init; } = BcsGrantTypes.RefreshToken;

    internal void Validate()
    {
        _ = ClientId.ToApiValue();

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
