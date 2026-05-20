namespace Bcs.InvestApi.WebSockets;

using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using Bcs.InvestApi.Infrastructure;

/// <summary>
/// Helpers for receiving JSON payloads from a <see cref="WebSocket"/> using
/// the same <see cref="JsonSerializerOptions"/> as the HTTP transport.
/// The SDK provides receive plumbing only — reconnect, ping/pong,
/// and message dispatch belong to the application layer.
/// </summary>
public static class BcsWebSocketExtensions
{
    private const int InitialBufferSize = 8 * 1024;

    /// <summary>
    /// Reads a single logical message from the WebSocket (concatenating frames until <c>EndOfMessage</c>)
    /// and deserializes it as <typeparamref name="T"/> using the SDK's JSON options.
    /// Returns <c>default</c> if the server closes the connection cleanly before any data arrives.
    /// Throws <see cref="WebSocketException"/> if the connection closes in the middle of a message.
    /// </summary>
    public static async Task<T?> ReceiveJsonMessageAsync<T>(
        this WebSocket webSocket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webSocket);

        var buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        var written = 0;

        try
        {
            while (true)
            {
                if (written == buffer.Length)
                {
                    buffer = GrowBuffer(buffer, written);
                }

                var result = await webSocket
                    .ReceiveAsync(
                        new ArraySegment<byte>(buffer, written, buffer.Length - written),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (written == 0)
                    {
                        return default;
                    }

                    throw new WebSocketException(
                        WebSocketError.ConnectionClosedPrematurely,
                        "WebSocket closed in the middle of a message.");
                }

                written += result.Count;

                if (result.EndOfMessage)
                {
                    break;
                }
            }

            return JsonSerializer.Deserialize<T>(
                new ReadOnlySpan<byte>(buffer, 0, written),
                BcsJson.SerializerOptions);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static byte[] GrowBuffer(byte[] current, int written)
    {
        var bigger = ArrayPool<byte>.Shared.Rent(current.Length * 2);
        Buffer.BlockCopy(current, 0, bigger, 0, written);
        ArrayPool<byte>.Shared.Return(current);
        return bigger;
    }
}
