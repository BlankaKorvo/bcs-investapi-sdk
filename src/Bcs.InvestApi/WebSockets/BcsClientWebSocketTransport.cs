namespace Bcs.InvestApi.WebSockets;

using System.Net;
using System.Net.WebSockets;
using Bcs.InvestApi.Contracts.Exceptions;

/// <summary>
/// The default <see cref="IBcsWebSocketTransport"/>: a <see cref="ClientWebSocket"/> with keep-alive, so the
/// connection takes part in the ping/pong heartbeat and a dead peer is detected. .NET answers the server's
/// ping frames while a receive is pending, so the heartbeat works as long as the caller keeps reading.
/// </summary>
public sealed class BcsClientWebSocketTransport : IBcsWebSocketTransport
{
    private readonly TimeSpan _keepAliveInterval;
    private readonly TimeSpan _keepAliveTimeout;

    public BcsClientWebSocketTransport(TimeSpan keepAliveInterval, TimeSpan keepAliveTimeout)
    {
        _keepAliveInterval = keepAliveInterval;
        _keepAliveTimeout = keepAliveTimeout;
    }

    public async Task<WebSocket> ConnectAsync(Uri url, string authorization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorization);

        var socket = new ClientWebSocket();
        try
        {
            socket.Options.SetRequestHeader("Authorization", authorization);
            socket.Options.CollectHttpResponseDetails = true;
            ConfigureKeepAlive(socket, _keepAliveInterval, _keepAliveTimeout);
            await socket.ConnectAsync(url, cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch (WebSocketException ex) when (IsRefusedHandshake(socket.HttpStatusCode))
        {
            var status = socket.HttpStatusCode;
            socket.Dispose();
            throw new BcsApiException(status, ex.Message, url.ToString());
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    internal static void ConfigureKeepAlive(ClientWebSocket socket, TimeSpan interval, TimeSpan timeout)
    {
        socket.Options.KeepAliveInterval = interval;
        socket.Options.KeepAliveTimeout = timeout;
    }

    // The server answered the upgrade request with an HTTP status, not with 101: it refused the handshake.
    // 0 — no answer at all (network, TLS), left as the transport exception.
    internal static bool IsRefusedHandshake(HttpStatusCode status)
        => status != 0 && status != HttpStatusCode.SwitchingProtocols;
}
