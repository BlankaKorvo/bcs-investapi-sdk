namespace Bcs.InvestApi.Infrastructure;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// A BCS <c>date-time</c> as the documentation declares it — RFC 3339, where the offset is required. A string without an
/// offset (<c>2026-10-19T07:00:00</c>, or a date alone) is not one: <see cref="JsonException"/>, never the machine's own
/// time zone, which is what <see cref="JsonSerializer"/> would silently take for it.
/// </summary>
internal sealed class BcsDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"A BCS date-time is a string, not {reader.TokenType}.");

        // The parser of System.Text.Json tells the offset by the kind: Utc for 'Z', Local for an offset, Unspecified for none.
        if (!reader.TryGetDateTime(out var parsed) || !reader.TryGetDateTimeOffset(out var value))
            throw new JsonException($"'{reader.GetString()}' is not a BCS date-time (RFC 3339).");
        if (parsed.Kind == DateTimeKind.Unspecified)
            throw new JsonException(
                $"'{reader.GetString()}' is a BCS date-time without an offset: RFC 3339 requires one, and the time zone is not guessed.");
        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
