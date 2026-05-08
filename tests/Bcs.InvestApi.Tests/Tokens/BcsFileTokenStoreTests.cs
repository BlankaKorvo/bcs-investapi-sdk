namespace Bcs.InvestApi.Tests.Tokens;

using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsFileTokenStoreTests
{
    [Fact]
    public async Task EnsureCanPersistAsync_WhenDirectoryDoesNotExist_CreatesDirectoryWithoutTokenFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bcs-token-store-tests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(directory, "tokens.json");
        var store = new BcsFileTokenStore(filePath);

        await store.EnsureCanPersistAsync();

        Assert.True(Directory.Exists(directory));
        Assert.False(File.Exists(filePath));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task EnsureCanPersistAsync_WhenTokenPathIsDirectory_ThrowsPersistenceException()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "bcs-token-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(filePath);
        var store = new BcsFileTokenStore(filePath);

        var exception = await Assert.ThrowsAsync<BcsTokenPersistenceException>(() => store.EnsureCanPersistAsync().AsTask());

        Assert.Contains("not writable", exception.Message);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsTokenPair()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "bcs-token-store-tests", $"{Guid.NewGuid():N}.json");
        var store = new BcsFileTokenStore(filePath);
        var tokenSet = new BcsTokenSet
        {
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            TokenType = "bearer",
            ExpiresIn = 86400,
            RefreshExpiresIn = 7776000,
            ReceivedAtUtc = new DateTimeOffset(2026, 05, 02, 12, 00, 00, TimeSpan.Zero),
            AccessTokenExpiresAtUtc = new DateTimeOffset(2026, 05, 03, 12, 00, 00, TimeSpan.Zero),
            RefreshTokenExpiresAtUtc = new DateTimeOffset(2026, 07, 31, 12, 00, 00, TimeSpan.Zero),
            NotBeforePolicy = 0,
            SessionState = "session-state-1",
            Scope = "trade-api-read",
        };

        await store.SaveAsync(tokenSet);
        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("access-1", loaded.AccessToken);
        Assert.Equal("refresh-1", loaded.RefreshToken);
        Assert.Equal(tokenSet.AccessTokenExpiresAtUtc, loaded.AccessTokenExpiresAtUtc);
        Assert.Equal(tokenSet.RefreshTokenExpiresAtUtc, loaded.RefreshTokenExpiresAtUtc);
    }

    [Fact]
    public void IsLockContention_WhenSharingOrLockViolation_ReturnsTrue()
    {
        Assert.True(BcsFileTokenRefreshCoordinator.IsLockContention(CreateWin32IOException(32)));
        Assert.True(BcsFileTokenRefreshCoordinator.IsLockContention(CreateWin32IOException(33)));
    }

    [Fact]
    public void IsLockContention_WhenPermanentIoError_ReturnsFalse()
    {
        Assert.False(BcsFileTokenRefreshCoordinator.IsLockContention(CreateWin32IOException(5)));
    }

    private static IOException CreateWin32IOException(int errorCode) =>
        new("Test IO exception.", unchecked((int)0x80070000) | errorCode);
}
