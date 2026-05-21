namespace Bcs.InvestApi.WebSockets;

using System.Net.WebSockets;
using Bcs.InvestApi.Tokens;

/// <summary>
/// Opens an authenticated <see cref="ClientWebSocket"/> connection to a BCS Trade API WebSocket endpoint.
/// The connector retrieves a fresh access token from <see cref="IBcsAccessTokenProvider"/>,
/// attaches it as a Bearer authorization header, and configures keep-alive so the connection
/// participates in the ping/pong heartbeat and dead peers are detected.
///
/// .NET answers the server's ping frames with pong automatically while a receive is pending,
/// so the heartbeat works as long as the caller keeps reading. Reconnect, backoff and
/// multiplexing remain the caller's responsibility; on disconnect, request a new connection
/// and a fresh token will be fetched automatically.
/// </summary>
public sealed class BcsWebSocketConnector
{
    private readonly IBcsAccessTokenProvider _tokens;
    private readonly TimeSpan _keepAliveInterval;
    private readonly TimeSpan _keepAliveTimeout;

    internal BcsWebSocketConnector(
        IBcsAccessTokenProvider tokens,
        TimeSpan keepAliveInterval,
        TimeSpan keepAliveTimeout)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _keepAliveInterval = keepAliveInterval;
        _keepAliveTimeout = keepAliveTimeout;
    }

    /// <summary>
    /// Opens an authenticated WebSocket connection to the given URL.
    /// On success returns an open <see cref="ClientWebSocket"/>; the caller owns disposal.
    /// On failure, the partially-created socket is disposed before the exception propagates.
    /// </summary>
    public async Task<ClientWebSocket> ConnectAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        var accessToken = await _tokens
            .GetAccessTokenAsync(cancellationToken)
            .ConfigureAwait(false);

        var socket = new ClientWebSocket();
        try
        {
            socket.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            ConfigureKeepAlive(socket, _keepAliveInterval, _keepAliveTimeout);
            await socket.ConnectAsync(url, cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens an authenticated connection to <paramref name="url"/> and sends <paramref name="subscribeMessage"/>
    /// as the first (text) frame, using the SDK's JSON options. Convenience for streams that require an explicit
    /// subscribe request (e.g. <see cref="BcsWebSocketEndpoints.MarketData"/>); push-only streams should use
    /// <see cref="ConnectAsync"/> instead. The caller still owns the socket lifecycle: receiving, unsubscribing,
    /// reconnect and disposal. On failure the socket is disposed before the exception propagates.
    /// </summary>
    public async Task<ClientWebSocket> ConnectAndSubscribeAsync<TSubscribe>(
        Uri url,
        TSubscribe subscribeMessage,
        CancellationToken cancellationToken = default)
        where TSubscribe : notnull
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(subscribeMessage);

        var socket = await ConnectAsync(url, cancellationToken).ConfigureAwait(false);
        try
        {
            await socket.SendJsonMessageAsync(subscribeMessage, cancellationToken).ConfigureAwait(false);
            return socket;
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
}
