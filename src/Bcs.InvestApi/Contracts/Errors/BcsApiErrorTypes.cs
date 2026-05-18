namespace Bcs.InvestApi.Contracts.Errors;

public static class BcsApiErrorTypes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string ResourceExhausted = "RESOURCE_EXHAUSTED";
    public const string UserBlocked = "USER_BLOCKED";
    public const string BadRequest = "BAD_REQUEST";
    public const string NotFound = "NOT_FOUND";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string Conflict = "CONFLICT";
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";
    public const string SessionNotFoundError = "SESSION_NOT_FOUND_ERROR";
    public const string SessionExpiredError = "SESSION_EXPIRED_ERROR";
    public const string SessionFailedError = "SESSION_FAILED_ERROR";
}
