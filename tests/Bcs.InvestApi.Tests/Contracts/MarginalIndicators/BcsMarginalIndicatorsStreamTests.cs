namespace Bcs.InvestApi.Tests.Contracts.MarginalIndicators;

using System.Text.Json;
using Bcs.InvestApi.Contracts.MarginalIndicators;
using Xunit;

public sealed class BcsMarginalIndicatorsStreamTests
{
    // Mirrors the SDK's transport options (Bcs.InvestApi.Infrastructure.BcsJson).
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void MarginalIndicators_Deserializes_FullSnapshot()
    {
        const string payload = """
        {
          "agreementData": {
            "number": "1234567/25",
            "isIia": false,
            "displayName": "Основной",
            "subaccountId": "49909a5f-8cfe-441a-b3b7-86054eb97922",
            "color": "#2E88CB"
          },
          "portfolioCurrentValue": {
            "currentValueRub": 8086.59,
            "dailyPortfolioChangeRub": -6.38,
            "dailyPortfolioChangePerc": -1.07,
            "totalPortfolioChangeRub": -9.25,
            "totalPortfolioChangePerc": -0.24,
            "collateralAvailable": 5018.23,
            "varMargin": 0
          },
          "portfolioStability": {
            "isMarginOn": false,
            "borrowedFunds": 0,
            "stabilityStatusId": 5,
            "stabilityStatusName": "Максимальная устойчивость",
            "refillSum": 0
          },
          "fortsStability": {
            "ownFunds": 0,
            "freeCash": 0,
            "stabilityStatusId": 0,
            "stabilityStatusName": "",
            "refillSum": 0,
            "fortsMarginCall": 0,
            "fortsForcedClosure": 0,
            "riskLevel": 0,
            "optionsPremium": 0
          },
          "marginParameters": {
            "marginValue": 5136.59,
            "collateral": 0,
            "marginCall": 118.36,
            "forcedClosure": 59.18
          },
          "cashByCurrency": [
            { "currency": "RUB", "sum": 4544.79 }
          ],
          "moneyDebt": {
            "totalMoneyDebt": 0,
            "debtByCurrency": []
          },
          "marginSecurities": {
            "totalMarginSecurity": 0,
            "marginBySecurity": [
              {
                "ticker": "RUB",
                "board": "",
                "displayName": "",
                "logoLink": "https://mybroker.storage.bcs.ru/FinInstrumentLogo/RUB.png",
                "quantity": 4544.79,
                "currentValue": 4544.79,
                "currency": "RUB",
                "discount": 0
              },
              {
                "ticker": "SBER",
                "board": "TQBR",
                "displayName": "Сбербанк",
                "logoLink": "https://mybroker.storage.bcs.ru/FinInstrumentLogo/RU0009029540.png",
                "quantity": 2,
                "currentValue": 591.8,
                "currency": "RUB",
                "discount": 20
              }
            ]
          },
          "futuresParameters": {
            "futuresValue": 0,
            "futuresPortfolio": []
          }
        }
        """;

        var message = JsonSerializer.Deserialize<BcsMarginalIndicators>(payload, Options);

        Assert.NotNull(message);

        Assert.Equal("1234567/25", message!.AgreementData!.Number);
        Assert.False(message.AgreementData.IsIia);
        Assert.Equal("#2E88CB", message.AgreementData.Color);

        Assert.Equal(8086.59m, message.PortfolioCurrentValue!.CurrentValueRub);
        Assert.Equal(-6.38m, message.PortfolioCurrentValue.DailyPortfolioChangeRub);
        Assert.Equal(5018.23m, message.PortfolioCurrentValue.CollateralAvailable);

        Assert.False(message.PortfolioStability!.IsMarginOn);
        Assert.Equal(5, message.PortfolioStability.StabilityStatusId);
        Assert.Equal("Максимальная устойчивость", message.PortfolioStability.StabilityStatusName);

        Assert.Equal(0, message.FortsStability!.RiskLevel);
        Assert.Equal(0m, message.FortsStability.OptionsPremium);

        Assert.Equal(118.36m, message.MarginParameters!.MarginCall);
        Assert.Equal(59.18m, message.MarginParameters.ForcedClosure);

        var cash = Assert.Single(message.CashByCurrency);
        Assert.Equal("RUB", cash.Currency);
        Assert.Equal(4544.79m, cash.Sum);

        Assert.Equal(0m, message.MoneyDebt!.TotalMoneyDebt);
        Assert.Empty(message.MoneyDebt.DebtByCurrency);

        Assert.Equal(2, message.MarginSecurities!.MarginBySecurity.Count);
        var sber = message.MarginSecurities.MarginBySecurity[1];
        Assert.Equal("SBER", sber.Ticker);
        Assert.Equal("TQBR", sber.Board);
        Assert.Equal(2m, sber.Quantity);
        Assert.Equal(591.8m, sber.CurrentValue);
        Assert.Equal(20m, sber.Discount);

        Assert.Equal(0m, message.FuturesParameters!.FuturesValue);
        Assert.Empty(message.FuturesParameters.FuturesPortfolio);
    }
}
