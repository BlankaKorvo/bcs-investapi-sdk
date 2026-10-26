namespace Bcs.InvestApi.Infrastructure;

using System.Text.Json;
using System.Text.Json.Serialization;
using Bcs.InvestApi.Contracts.Orders;

/// <summary>
/// <c>data</c> of the order status and of the synchronous creation (<see cref="BcsOrderStatusResponse.Data"/>): one report
/// as an object — what BCS sends — or reports as an array — what the method pages document. Anything else, <c>null</c> and
/// a <c>null</c> report among them included, is an error: the answer does not describe the order.
/// </summary>
internal sealed class BcsOrderReportsConverter : JsonConverter<IReadOnlyList<BcsOrderStatusData>>
{
    public override bool HandleNull => true;

    public override IReadOnlyList<BcsOrderStatusData> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                return [JsonSerializer.Deserialize<BcsOrderStatusData>(ref reader, options)!];
            case JsonTokenType.StartArray:
                var reports = JsonSerializer.Deserialize<List<BcsOrderStatusData?>>(ref reader, options)!;
                if (reports.Contains(null))
                {
                    throw new JsonException("A report in 'data' is null.");
                }

                return reports!;
            default:
                throw new JsonException(
                    $"'data' is {reader.TokenType}: neither an object (one report) nor an array of reports.");
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyList<BcsOrderStatusData> value,
        JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.ToArray(), options);
}
