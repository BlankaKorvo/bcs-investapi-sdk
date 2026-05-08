namespace Bcs.InvestApi.Tokens;

public interface IBcsTokenStore
{
    ValueTask<BcsTokenSet?> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(BcsTokenSet tokenSet, CancellationToken cancellationToken = default);
}
