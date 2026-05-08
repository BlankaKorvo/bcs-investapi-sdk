namespace Bcs.InvestApi.Tokens;

using System.Collections.Concurrent;

internal sealed class BcsFileTokenRefreshCoordinator : IBcsTokenRefreshCoordinator
{
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;
    private const int ErrorCodeMask = 0xFFFF;
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(PathComparer);

    private readonly string _lockFilePath;
    private readonly SemaphoreSlim _gate;
    private readonly AsyncLocal<int> _lockDepth = new();

    public BcsFileTokenRefreshCoordinator(string tokenFilePath)
    {
        if (string.IsNullOrWhiteSpace(tokenFilePath))
        {
            throw new ArgumentException("BCS token storage path is required.", nameof(tokenFilePath));
        }

        var fullTokenFilePath = Path.GetFullPath(tokenFilePath);
        _lockFilePath = fullTokenFilePath + ".lock";
        _gate = Gates.GetOrAdd(_lockFilePath, _ => new SemaphoreSlim(1, 1));
    }

    public async ValueTask<T> ExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_lockDepth.Value > 0)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var lockStream = await AcquireLockFileAsync(cancellationToken).ConfigureAwait(false);
            _lockDepth.Value++;
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _lockDepth.Value--;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<FileStream> AcquireLockFileAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_lockFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(
                    _lockFilePath,
                    new FileStreamOptions
                    {
                        Mode = FileMode.OpenOrCreate,
                        Access = FileAccess.ReadWrite,
                        Share = FileShare.None,
                        BufferSize = 1,
                        Options = FileOptions.Asynchronous,
                    });
            }
            catch (IOException ex) when (!cancellationToken.IsCancellationRequested && IsLockContention(ex))
            {
                await Task.Delay(LockRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal static bool IsLockContention(IOException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var errorCode = exception.HResult & ErrorCodeMask;
        return errorCode is ErrorSharingViolation or ErrorLockViolation;
    }
}
