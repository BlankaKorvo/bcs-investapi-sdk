namespace Bcs.InvestApi.Tokens;

public interface IBcsTokenStorePreflight
{
    ValueTask EnsureCanPersistAsync(CancellationToken cancellationToken = default);
}
