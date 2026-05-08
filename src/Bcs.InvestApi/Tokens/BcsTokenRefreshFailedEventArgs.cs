namespace Bcs.InvestApi.Tokens;

public sealed class BcsTokenRefreshFailedEventArgs : EventArgs
{
    public BcsTokenRefreshFailedEventArgs(Exception exception)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    public Exception Exception { get; }
}
