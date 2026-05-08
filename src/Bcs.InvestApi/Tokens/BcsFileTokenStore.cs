namespace Bcs.InvestApi.Tokens;

using System.Text.Json;
using Bcs.InvestApi.Infrastructure;

public sealed class BcsFileTokenStore : IBcsTokenStore, IBcsTokenRefreshCoordinator, IBcsTokenStorePreflight, IBcsTokenStoreCorruptionRecovery
{
    private static readonly byte[] PreflightProbeBytes = [0];

    private readonly string _filePath;
    private readonly BcsFileTokenRefreshCoordinator _coordinator;

    public BcsFileTokenStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("BCS token storage path is required.", nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
        _coordinator = new BcsFileTokenRefreshCoordinator(_filePath);
    }

    public ValueTask<T> ExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default) =>
        _coordinator.ExecuteAsync(operation, cancellationToken);

    internal IBcsTokenRefreshCoordinator RefreshCoordinator => this;

    public ValueTask<BcsTokenSet?> LoadAsync(CancellationToken cancellationToken = default) =>
        _coordinator.ExecuteAsync(LoadCoreAsync, cancellationToken);

    public async ValueTask EnsureCanPersistAsync(CancellationToken cancellationToken = default)
    {
        await _coordinator.ExecuteAsync(
            async ct =>
            {
                await EnsureCanPersistCoreAsync(ct).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    async ValueTask IBcsTokenStoreCorruptionRecovery.BackupCorruptedTokenStorageAsync(CancellationToken cancellationToken)
    {
        await _coordinator.ExecuteAsync(
            async ct =>
            {
                await EnsureCanPersistCoreAsync(ct).ConfigureAwait(false);
                BackupCorruptedTokenStorageCore();
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SaveAsync(BcsTokenSet tokenSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenSet);

        await _coordinator.ExecuteAsync(
            async ct =>
            {
                await SaveCoreAsync(tokenSet, ct).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    internal ValueTask<BcsTokenSet?> LoadForRefreshAsync(CancellationToken cancellationToken) =>
        LoadCoreAsync(cancellationToken);

    internal ValueTask EnsureCanPersistForRefreshAsync(CancellationToken cancellationToken) =>
        EnsureCanPersistCoreAsync(cancellationToken);

    internal ValueTask SaveForRefreshAsync(
        BcsTokenSet tokenSet,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenSet);

        return SaveCoreAsync(tokenSet, cancellationToken);
    }

    private async ValueTask EnsureCanPersistCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            EnsureDirectoryExists();
            if (Directory.Exists(_filePath))
            {
                throw new IOException("BCS token storage path points to a directory.");
            }

            var tempPath = GetTempPath();
            try
            {
                await WritePreflightProbeAsync(tempPath, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                DeleteIfExists(tempPath);
            }

            if (File.Exists(_filePath))
            {
                using var stream = new FileStream(
                    _filePath,
                    new FileStreamOptions
                    {
                        Mode = FileMode.Open,
                        Access = FileAccess.ReadWrite,
                        Share = FileShare.None,
                        BufferSize = 1,
                    });
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BcsTokenPersistenceException(
                $"BCS token storage is not writable. Auth refresh was not attempted. Path='{_filePath}'.",
                ex);
        }
    }

    private async ValueTask<BcsTokenSet?> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        await using var stream = new FileStream(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        if (stream.Length == 0)
        {
            return null;
        }

        return await JsonSerializer.DeserializeAsync<BcsTokenSet>(
            stream,
            BcsJson.SerializerOptions,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask SaveCoreAsync(BcsTokenSet tokenSet, CancellationToken cancellationToken)
    {
        EnsureDirectoryExists();

        var tempPath = GetTempPath();
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    BufferSize = 4096,
                    Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                }))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    tokenSet,
                    BcsJson.SerializerOptions,
                    cancellationToken).ConfigureAwait(false);

                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_filePath))
            {
                File.Replace(tempPath, _filePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, _filePath, overwrite: true);
            }
        }
        finally
        {
            DeleteIfExists(tempPath);
        }
    }

    private static async ValueTask WritePreflightProbeAsync(string tempPath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            tempPath,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 1,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            });

        await stream.WriteAsync(PreflightProbeBytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private string GetTempPath() => $"{_filePath}.{Guid.NewGuid():N}.tmp";

    private string GetCorruptBackupPath() => $"{_filePath}.corrupt.{Guid.NewGuid():N}.bak";

    private void BackupCorruptedTokenStorageCore()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            File.Move(_filePath, GetCorruptBackupPath());
        }
        catch (Exception ex)
        {
            throw new BcsTokenPersistenceException(
                $"BCS saved token storage is corrupted and could not be backed up. Auth refresh was not attempted. Path='{_filePath}'.",
                ex);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
