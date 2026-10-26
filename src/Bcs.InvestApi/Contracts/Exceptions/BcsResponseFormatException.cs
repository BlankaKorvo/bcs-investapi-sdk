namespace Bcs.InvestApi.Contracts.Exceptions;

using System.Text.Json;

/// <summary>
/// Thrown when a BCS answer — the body of a successful HTTP response or a WebSocket message — cannot be read into the
/// SDK's contract. The answer itself is in the message, up to <see cref="MaxShownLength"/> characters, so the first
/// unexpected form is seen at once rather than guessed from the position of the error.
/// </summary>
/// <remarks>
/// It is a <see cref="JsonException"/>: code that catches the serializer's error catches this one. The answer of the token
/// endpoint never goes into it — that answer holds the tokens; the request, with its <c>Authorization</c> header, never does
/// either.
/// </remarks>
public sealed class BcsResponseFormatException : JsonException
{
    /// <summary>The most characters of the answer the message shows; a longer one is cut, with its length.</summary>
    public const int MaxShownLength = 2000;

    public BcsResponseFormatException(string endpoint, string answer, JsonException error)
        : base(
            BuildMessage(endpoint, answer, error),
            error?.Path,
            error?.LineNumber,
            error?.BytePositionInLine,
            error)
    {
        Endpoint = endpoint;
    }

    /// <summary>The endpoint the answer came from, as the SDK names it (<c>order-status</c>, <c>websocket</c>, …).</summary>
    public string Endpoint { get; }

    private static string BuildMessage(string endpoint, string answer, JsonException error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(error);

        var shown = answer.Length <= MaxShownLength
            ? answer
            : $"{answer[..MaxShownLength]}… (the first {MaxShownLength} of {answer.Length} characters)";
        return $"BCS {endpoint}: the answer cannot be read — {error.Message} The answer: {shown}";
    }
}
