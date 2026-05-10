namespace Bcs.InvestApi.Tests;

using Bcs.InvestApi.Contracts.Enums;
using Xunit;

public sealed class BcsInvestApiSettingsTests
{
    [Fact]
    public void ValidateTransportSettings_WithUnsupportedClientId_ThrowsArgumentOutOfRangeException()
    {
        var settings = CreateSettings();
        settings.ClientId = (BcsAuthClientIds)999;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(settings.ValidateTransportSettings);

        Assert.Equal("clientId", exception.ParamName);
    }

    [Fact]
    public void CreateFactory_WithUnsupportedClientId_ThrowsArgumentOutOfRangeException()
    {
        var settings = new BcsInvestApiSettings
        {
            RefreshToken = "settings-refresh-1",
            ClientId = (BcsAuthClientIds)999,
            AuthUrl = new Uri("https://example.test/token"),
            BaseUrl = new Uri("https://example.test"),
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => BcsInvestApiClientFactory.Create(settings));

        Assert.Equal("clientId", exception.ParamName);
    }

    [Fact]
    public void KnownClientIds_MapToWireValues()
    {
        Assert.Equal("trade-api-read", BcsAuthClientIds.TradeApiRead.ToApiValue());
        Assert.Equal("trade-api-write", BcsAuthClientIds.TradeApiWrite.ToApiValue());
    }

    private static BcsInvestApiSettings CreateSettings() =>
        new()
        {
            AuthUrl = new Uri("https://example.test/token"),
            BaseUrl = new Uri("https://example.test"),
            ClientId = BcsAuthClientIds.TradeApiRead,
        };
}
