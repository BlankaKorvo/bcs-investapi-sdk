namespace Bcs.InvestApi.Contracts.Errors;

using System.Text.Json;

public sealed record BcsApiErrorResponse
{
    public long Timestamp { get; init; }
    public string? TraceId { get; init; }
    public string? Type { get; init; }
    public IReadOnlyList<BcsApiErrorItem> Errors { get; init; } = Array.Empty<BcsApiErrorItem>();
    public JsonElement? DisplayOptions { get; init; }
}

public sealed record BcsApiErrorItem
{
    public string? Type { get; init; }
    public string? Field { get; init; }
    public JsonElement? Payload { get; init; }
}
