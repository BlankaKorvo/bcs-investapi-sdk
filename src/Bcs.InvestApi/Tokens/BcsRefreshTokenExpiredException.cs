namespace Bcs.InvestApi.Tokens;

public sealed class BcsRefreshTokenExpiredException : Exception
{
    public BcsRefreshTokenExpiredException(DateTimeOffset refreshTokenExpiresAtUtc)
        : base($"BCS refresh token is expired or too close to expiration. RefreshTokenExpiresAtUtc={refreshTokenExpiresAtUtc:O}.")
    {
        RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc;
    }

    public DateTimeOffset RefreshTokenExpiresAtUtc { get; }
}
