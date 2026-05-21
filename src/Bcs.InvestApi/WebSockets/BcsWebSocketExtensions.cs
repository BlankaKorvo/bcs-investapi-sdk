namespace Bcs.InvestApi.WebSockets;

using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
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
    /// Serializes <paramref name="message"/> with the SDK's JSON options and sends it as a single
    /// text message (<c>EndOfMessage = true</c>). Use it for subscribe/unsubscribe payloads so they
    /// match the field shape the SDK uses for receiving.
    /// </summary>
    public static Task SendJsonMessageAsync<T>(
        this WebSocket webSocket,
        T message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webSocket);

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, BcsJson.SerializerOptions);

        return webSocket.SendAsync(
            new ArraySegment<byte>(payload),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }

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

    /// <summary>
    /// Reads messages from the WebSocket as an <see cref="IAsyncEnumerable{T}"/>, deserializing each
    /// with <see cref="ReceiveJsonMessageAsync{T}"/>. The sequence ends when the server closes the
    /// connection cleanly between messages. Cancellation and a mid-message close surface as the same
    /// exceptions <see cref="ReceiveJsonMessageAsync{T}"/> throws.
    /// </summary>
    /// <remarks>
    /// This is receive plumbing only: there is no reconnect, backoff, or buffering. The caller still
    /// owns the socket and its disposal, and is responsible for sending any subscribe payload first.
    /// A payload that deserializes to <c>null</c> is treated as end-of-stream, matching
    /// <see cref="ReceiveJsonMessageAsync{T}"/>.
    /// </remarks>
    public static async IAsyncEnumerable<T> ReadAllAsync<T>(
        this WebSocket webSocket,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webSocket);

        while (true)
        {
            var message = await webSocket
                .ReceiveJsonMessageAsync<T>(cancellationToken)
                .ConfigureAwait(false);

            if (message is null)
            {
                yield break;
            }

            yield return message;
        }
    }

    /// <summary>
    /// Streams logical messages from the WebSocket as an <see cref="IAsyncEnumerable{T}"/>, deserializing each
    /// with the SDK's JSON options. Enumeration ends when the server closes the connection cleanly
    /// (<see cref="ReceiveJsonMessageAsync{T}"/> returns <c>null</c>) or when <paramref name="cancellationToken"/>
    /// is cancelled. The SDK provides receive plumbing only: reconnect, ack/error filtering and dispatch
    /// remain the caller's responsibility.
    /// </summary>
    public static async IAsyncEnumerable<T> ReceiveJsonMessagesAsync<T>(
        this WebSocket webSocket,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(webSocket);

        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await webSocket
                .ReceiveJsonMessageAsync<T>(cancellationToken)
                .ConfigureAwait(false);

            if (message is null)
            {
                yield break;
            }

            yield return message;
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
