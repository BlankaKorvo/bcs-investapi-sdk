namespace Bcs.InvestApi.Tokens;

using System.Text.Json;
using Bcs.InvestApi.Time;

internal static class BcsTokenSourcePreflight
{
    private const string MissingStartupTokenSourceMessage =
        "BCS refresh token is not configured and token storage does not contain saved tokens.";

    public static async ValueTask EnsureStartupTokenSourceAsync(
        BcsInvestApiSettings settings,
        IBcsTokenStore tokenStore,
        IBcsClock clock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tokenStore);
        ArgumentNullException.ThrowIfNull(clock);

        var settingsHasRefreshToken = !string.IsNullOrWhiteSpace(settings.RefreshToken);
        BcsTokenSet? storedTokenSet;
        try
        {
            using var timeoutCts = new CancellationTokenSource(settings.TokenStoreLockTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            storedTokenSet = await tokenStore.LoadAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new InvalidOperationException(
                $"BCS saved token storage could not be loaded within token store lock timeout ({settings.TokenStoreLockTimeout}). Ensure no other process holds the token storage lock.",
                ex);
        }
        catch (JsonException) when (settingsHasRefreshToken && tokenStore is IBcsTokenStoreCorruptionRecovery recovery)
        {
            await BackupCorruptedTokenStorageAsync(
                settings,
                recovery,
                cancellationToken).ConfigureAwait(false);

            return;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "BCS saved token storage could not be loaded. Ensure the token file contains a valid BcsTokenSet JSON payload.",
                ex);
        }

        if (storedTokenSet is null)
        {
            if (settingsHasRefreshToken)
            {
                return;
            }

            throw new InvalidOperationException(MissingStartupTokenSourceMessage);
        }

        var nowUtc = clock.UtcNow;
        if (storedTokenSet.HasUsableRefreshToken(nowUtc) || settingsHasRefreshToken)
        {
            return;
        }

        storedTokenSet.ValidateStoredRefreshToken(nowUtc);
    }

    private static async ValueTask BackupCorruptedTokenStorageAsync(
        BcsInvestApiSettings settings,
        IBcsTokenStoreCorruptionRecovery recovery,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(settings.TokenStoreLockTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            await recovery.BackupCorruptedTokenStorageAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new InvalidOperationException(
                $"BCS corrupted token storage could not be backed up within token store lock timeout ({settings.TokenStoreLockTimeout}). Ensure no other process holds the token storage lock.",
                ex);
        }
    }
}
