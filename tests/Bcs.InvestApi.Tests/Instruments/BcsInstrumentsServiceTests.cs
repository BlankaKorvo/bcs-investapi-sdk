namespace Bcs.InvestApi.Tests.Instruments;

using System.Net;
using Bcs.InvestApi;
using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.Tests.Infrastructure;
using Bcs.InvestApi.Tokens;
using Xunit;

public sealed class BcsInstrumentsServiceTests
{
    [Fact]
    public async Task GetInstrumentsByIsinsAsync_PostsJsonBodyAndDeserializesResponse()
    {
        const string instrumentsJson = """
        [
          {
            "ticker": "GAZP",
            "boards": [
              {
                "classCode": "TQBR",
                "exchange": "MOEX"
              }
            ],
            "shortName": "Gazprom",
            "displayName": "Gazprom",
            "type": "Common shares",
            "isin": "RU0007661625",
            "registrationCode": "1-02-00028-A",
            "issuerName": "PAO Gazprom",
            "tradingCurrency": "RUB",
            "faceValue": 5,
            "scale": 2,
            "minimumStep": 0.01,
            "accruedInt": 0,
            "currencyStepPrice": "RUB",
            "settleCode": "T+1",
            "instrumentType": "STOCK",
            "settlementCurrency": "RUB",
            "settlementDate": "2026-02-04T00:00:00.000Z",
            "maturityDate": "",
            "lotSize": 10,
            "promoIdx": 0,
            "isQualifiedOnly": false,
            "isCanShort": true,
            "baseAsset": "",
            "qualifiedTestId": 0,
            "qualifiedTestIdTm": 0,
            "availableForUnqualified": true,
            "currencyNominal": "RUB",
            "stepPrice": 0,
            "isBcsProduct": false,
            "logoLink": "https://example.test/logo.png",
            "couponsPerYear": 0,
            "couponRate": 0,
            "nextCoupon": "1970-01-01T00:00:00.000Z",
            "complexProduct": -100,
            "baseAssetFuture": "",
            "subType": "AST_SEC_BASIC",
            "percentTargetCurrent": 19.77,
            "businessSector": "Oil and gas",
            "peNorm": 2.7,
            "priceTangible": 0.19,
            "epsGrowthRate": 0,
            "predictedDps": 0.0001,
            "dividendYield": 8.45,
            "priceChangeYear": -9.23,
            "targetPrice": 153.11,
            "mktcap": 3030209651200,
            "isBlocked": false,
            "businessSectorId": 2,
            "primaryBoard": "TQBR",
            "secondaryBoards": [
              "SMAL"
            ],
            "isCanMargin": true,
            "isReplacementBond": false,
            "subTitle": "GAZP",
            "couponTypeName": "",
            "emissionDate": "1970-01-01",
            "excludeTypeFlags": 4095,
            "creditRating": "",
            "liquidityRating": "",
            "bcsScore": 4,
            "bcsScoreColor": "yellow",
            "cfi": "",
            "nrdCode": "RU0007661625",
            "strike": 0,
            "baseAssetSecuritySecCode": "",
            "baseAssetSecurityClassCode": "",
            "businessCountry": "Russia",
            "businessCountryCode": "RU",
            "priceChangeHalfYear": 5.11,
            "priceChangeMonth": 1.98,
            "priceChangeEarlyYear": 3.33,
            "excludeTypes": [
              0,
              7
            ],
            "displayNameSecond": "GAZP",
            "firstCurrCode": "",
            "amortisedMty": false
          }
        ]
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, instrumentsJson)));
        var service = new BcsInstrumentsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var instruments = await service.GetInstrumentsByIsinsAsync(
            new[] { " RU0007661625 ", "ru0007661625", "ru0007661625" },
            page: 1,
            size: 25);

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal("application/json", handler.LastRequest?.Content?.Headers.ContentType?.MediaType);
        Assert.Equal("""{"isins":[" RU0007661625 ","ru0007661625","ru0007661625"]}""", handler.LastRequestContent);

        var instrument = Assert.Single(instruments);
        Assert.Equal("GAZP", instrument.Ticker);
        Assert.Equal("RU0007661625", instrument.Isin);
        Assert.Equal("STOCK", instrument.InstrumentType);
        Assert.Equal(10, instrument.LotSize);
        Assert.Equal("2026-02-04T00:00:00.000Z", instrument.SettlementDate);
        Assert.Equal("", instrument.MaturityDate);
        Assert.Equal("TQBR", instrument.PrimaryBoard);

        var board = Assert.Single(instrument.Boards);
        Assert.Equal("TQBR", board.ClassCode);
        Assert.Equal("MOEX", board.Exchange);

        Assert.Equal(new[] { "SMAL" }, instrument.SecondaryBoards);
    }

    [Fact]
    public async Task GetInstrumentsByTickersAsync_PostsJsonBodyAndDeserializesResponse()
    {
        const string instrumentsJson = """
        [
          {
            "ticker": "SBER",
            "boards": [
              {
                "classCode": "TQBR",
                "exchange": "MOEX"
              }
            ],
            "shortName": "Sber",
            "displayName": "Sber",
            "type": "STOCK",
            "isin": "RU0009029540",
            "instrumentType": "STOCK",
            "tradingCurrency": "RUB",
            "lotSize": 10,
            "primaryBoard": "TQBR"
          }
        ]
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, instrumentsJson)));
        var service = new BcsInstrumentsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var instruments = await service.GetInstrumentsByTickersAsync(
            new[] { " SBER ", "sber", "sber" },
            page: 1,
            size: 25);

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Equal("application/json", handler.LastRequest?.Content?.Headers.ContentType?.MediaType);
        Assert.Equal("""{"tickers":[" SBER ","sber","sber"]}""", handler.LastRequestContent);

        var instrument = Assert.Single(instruments);
        Assert.Equal("SBER", instrument.Ticker);
        Assert.Equal("RU0009029540", instrument.Isin);
        Assert.Equal("STOCK", instrument.InstrumentType);
        Assert.Equal("TQBR", instrument.PrimaryBoard);

        var board = Assert.Single(instrument.Boards);
        Assert.Equal("TQBR", board.ClassCode);
        Assert.Equal("MOEX", board.Exchange);
    }

    [Fact]
    public async Task GetInstrumentsByTypeAsync_SendsGetRequestAndDeserializesResponse()
    {
        const string instrumentsJson = """
        [
          {
            "ticker": "SBER",
            "boards": [
              {
                "classCode": "TQBR",
                "exchange": "MOEX"
              }
            ],
            "shortName": "Sber",
            "displayName": "Sber",
            "type": "STOCK",
            "isin": "RU0009029540",
            "instrumentType": "STOCK",
            "tradingCurrency": "RUB",
            "lotSize": 10,
            "primaryBoard": "TQBR"
          }
        ]
        """;

        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, instrumentsJson)));
        var service = new BcsInstrumentsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var instruments = await service.GetInstrumentsByTypeAsync(
            BcsInstrumentTypes.Stock,
            page: 1,
            size: 25);

        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal(
            new Uri("https://example.test/trade-api-information-service/api/v1/instruments/by-type?type=STOCK&size=25&page=1"),
            handler.LastRequest?.RequestUri);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token-1", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Null(handler.LastRequestContent);

        var instrument = Assert.Single(instruments);
        Assert.Equal("SBER", instrument.Ticker);
        Assert.Equal("RU0009029540", instrument.Isin);
        Assert.Equal("STOCK", instrument.InstrumentType);
        Assert.Equal("TQBR", instrument.PrimaryBoard);
    }

    [Fact]
    public async Task GetInstrumentsByTypeAsync_WithOptionsType_SendsBaseAssetTicker()
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var service = new BcsInstrumentsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        await service.GetInstrumentsByTypeAsync(
            BcsInstrumentTypes.Options,
            page: 1,
            size: 20,
            baseAssetTicker: " SBER ");

        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal(
            new Uri("https://example.test/trade-api-information-service/api/v1/instruments/by-type?type=OPTIONS&baseAssetTicker=%20SBER%20&size=20&page=1"),
            handler.LastRequest?.RequestUri);
        Assert.Null(handler.LastRequestContent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetInstrumentsByTypeAsync_WithOptionsTypeAndMissingBaseAssetTicker_Throws(
        string? baseAssetTicker)
    {
        var handler = new CapturingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")));
        var service = new BcsInstrumentsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetInstrumentsByTypeAsync(
                BcsInstrumentTypes.Options,
                page: 1,
                size: 20,
                baseAssetTicker: baseAssetTicker));

        Assert.Equal("baseAssetTicker", exception.ParamName);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetInstrumentsByIsinsAsync_RequestsOnlySpecifiedPageWhenPageIsFull()
    {
        var requestedUris = new List<Uri?>();
        var handler = new CapturingHttpMessageHandler((request, _) =>
        {
            requestedUris.Add(request.RequestUri);
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, """[{"ticker":"FIRST"},{"ticker":"SECOND"}]"""));
        });
        var service = new BcsInstrumentsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var instruments = await service.GetInstrumentsByIsinsAsync(
            new[] { "RU0007661625" },
            page: 3,
            size: 2);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(
            new[]
            {
                new Uri("https://example.test/trade-api-information-service/api/v1/instruments/by-isins?size=2&page=3"),
            },
            requestedUris);
        Assert.Equal(new[] { "FIRST", "SECOND" }, instruments.Select(instrument => instrument.Ticker));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public async Task GetInstrumentsByTickersAsync_WithInvalidSize_Throws(int size)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetInstrumentsByTickersAsync(new[] { "SBER" }, page: 0, size: size));
    }

    [Fact]
    public async Task GetInstrumentsByTickersAsync_RequestsOnlySpecifiedPageWhenPageIsFull()
    {
        var requestedUris = new List<Uri?>();
        var handler = new CapturingHttpMessageHandler((request, _) =>
        {
            requestedUris.Add(request.RequestUri);
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, """[{"ticker":"FIRST"},{"ticker":"SECOND"}]"""));
        });
        var service = new BcsInstrumentsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var instruments = await service.GetInstrumentsByTickersAsync(
            new[] { "SBER" },
            page: 3,
            size: 2);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(
            new[]
            {
                new Uri("https://example.test/trade-api-information-service/api/v1/instruments/by-tickers?size=2&page=3"),
            },
            requestedUris);
        Assert.Equal(new[] { "FIRST", "SECOND" }, instruments.Select(instrument => instrument.Ticker));
    }

    [Fact]
    public async Task GetInstrumentsByTypeAsync_RequestsOnlySpecifiedPageWhenPageIsFull()
    {
        var requestedUris = new List<Uri?>();
        var handler = new CapturingHttpMessageHandler((request, _) =>
        {
            requestedUris.Add(request.RequestUri);
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, """[{"ticker":"FIRST"},{"ticker":"SECOND"}]"""));
        });
        var service = new BcsInstrumentsService(
            CreateSettings(),
            new HttpClient(handler),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

        var instruments = await service.GetInstrumentsByTypeAsync(
            BcsInstrumentTypes.Stock,
            page: 3,
            size: 2);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(
            new[]
            {
                new Uri("https://example.test/trade-api-information-service/api/v1/instruments/by-type?type=STOCK&size=2&page=3"),
            },
            requestedUris);
        Assert.Equal(new[] { "FIRST", "SECOND" }, instruments.Select(instrument => instrument.Ticker));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public async Task GetInstrumentsByIsinsAsync_WithInvalidSize_Throws(int size)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetInstrumentsByIsinsAsync(new[] { "RU0007661625" }, page: 0, size: size));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public async Task GetInstrumentsByTypeAsync_WithInvalidSize_Throws(int size)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetInstrumentsByTypeAsync(BcsInstrumentTypes.Stock, page: 0, size: size));
    }

    [Fact]
    public async Task GetInstrumentsByIsinsAsync_WithNegativePage_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetInstrumentsByIsinsAsync(new[] { "RU0007661625" }, page: -1, size: 50));
    }

    [Fact]
    public async Task GetInstrumentsByTickersAsync_WithNegativePage_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetInstrumentsByTickersAsync(new[] { "SBER" }, page: -1, size: 50));
    }

    [Fact]
    public async Task GetInstrumentsByTypeAsync_WithNegativePage_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetInstrumentsByTypeAsync(BcsInstrumentTypes.Stock, page: -1, size: 50));
    }

    [Fact]
    public async Task GetInstrumentsByIsinsAsync_WithEmptyIsins_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetInstrumentsByIsinsAsync(Array.Empty<string>(), page: 0, size: 50));
    }

    [Fact]
    public async Task GetInstrumentsByIsinsAsync_WithNullIsins_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.GetInstrumentsByIsinsAsync(null!, page: 0, size: 50));
    }

    [Fact]
    public async Task GetInstrumentsByTickersAsync_WithEmptyTickers_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetInstrumentsByTickersAsync(Array.Empty<string>(), page: 0, size: 50));
    }

    [Fact]
    public async Task GetInstrumentsByTickersAsync_WithNullTickers_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.GetInstrumentsByTickersAsync(null!, page: 0, size: 50));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetInstrumentsByIsinsAsync_WithNullOrWhitespaceIsin_Throws(string? isin)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetInstrumentsByIsinsAsync(new[] { "RU0007661625", isin! }, page: 0, size: 50));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetInstrumentsByTickersAsync_WithNullOrWhitespaceTicker_Throws(string? ticker)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetInstrumentsByTickersAsync(new[] { "SBER", ticker! }, page: 0, size: 50));
    }

    [Fact]
    public async Task GetInstrumentsByTypeAsync_WithUnsupportedType_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetInstrumentsByTypeAsync((BcsInstrumentTypes)999, page: 0, size: 50));
    }

    private static BcsInstrumentsService CreateService() =>
        new(
            CreateSettings(),
            new HttpClient(new CapturingHttpMessageHandler((_, _) =>
                Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]")))),
            new StaticTokenProvider("access-token-1"),
            new BcsHttpRequestSender());

    private static BcsInvestApiSettings CreateSettings() =>
        new()
        {
            BaseUrl = new Uri("https://example.test"),
        };

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private sealed class StaticTokenProvider : IBcsAccessTokenProvider
    {
        private readonly string _accessToken;

        public StaticTokenProvider(string accessToken)
        {
            _accessToken = accessToken;
        }

        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_accessToken);

        public void InvalidateAccessToken(string rejectedAccessToken)
        {
        }
    }
}
