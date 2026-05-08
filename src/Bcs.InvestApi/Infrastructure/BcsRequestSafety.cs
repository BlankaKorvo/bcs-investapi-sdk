namespace Bcs.InvestApi.Infrastructure;

internal enum BcsRequestSafety
{
    IdempotentRead,
    IdempotentQueryPost,
    NonIdempotentCommand,
    TokenRefresh,
}
