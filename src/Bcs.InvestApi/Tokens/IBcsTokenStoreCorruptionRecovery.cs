namespace Bcs.InvestApi.Tokens;

internal interface IBcsTokenStoreCorruptionRecovery
{
    ValueTask BackupCorruptedTokenStorageAsync(CancellationToken cancellationToken = default);
}
