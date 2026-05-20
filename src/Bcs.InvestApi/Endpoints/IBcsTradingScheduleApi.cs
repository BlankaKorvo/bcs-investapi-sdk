namespace Bcs.InvestApi;

using Bcs.InvestApi.Contracts.TradingSchedule;

/// <summary>Trading schedule endpoints of the BCS Trade API.</summary>
public interface IBcsTradingScheduleApi
{
    Task<BcsDailyTradingScheduleResponse> GetDailyTradingScheduleAsync(
        string classCode,
        string ticker,
        CancellationToken cancellationToken = default);

    Task<BcsTradingScheduleStatusResponse> GetTradingScheduleStatusAsync(
        string classCode,
        CancellationToken cancellationToken = default);
}
