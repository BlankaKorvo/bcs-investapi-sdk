namespace Bcs.InvestApi.Tokens;

public sealed class BcsTokenPersistenceException : Exception
{
    public BcsTokenPersistenceException(string message)
        : base(message)
    {
    }

    public BcsTokenPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
