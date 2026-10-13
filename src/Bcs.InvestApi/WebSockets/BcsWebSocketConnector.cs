namespace Bcs.InvestApi.WebSockets;

using System.Net;
using System.Net.WebSockets;
using Bcs.InvestApi.Contracts.Exceptions;
using Bcs.InvestApi.Tokens;

/// <summary>
/// Opens an authenticated WebSocket connection to a BCS Trade API WebSocket endpoint.
/// The connector retrieves a fresh access token from <see cref="IBcsAccessTokenProvider"/>,
/// attaches it as a Bearer authorization header and opens the socket through its
/// <see cref="IBcsWebSocketTransport"/> (by default <see cref="BcsClientWebSocketTransport"/>, with keep-alive).
///
/// A handshake refused with 401 drops the cached access token, as an HTTP 401 does, so the next connect
/// obtains a fresh one. Reconnect, backoff and multiplexing remain the caller's responsibility.
/// </summary>
public sealed class BcsWebSocketConnector
{
    private readonly IBcsAccessTokenProvider _tokens;
    private readonly IBcsWebSocketTransport _transport;

    internal BcsWebSocketConnector(
        IBcsAccessTokenProvider tokens,
        IBcsWebSocketTransport transport)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <summary>
    /// Opens an authenticated WebSocket connection to the given URL.
    /// On success returns an open <see cref="WebSocket"/>; the caller owns disposal.
    /// A refused handshake — <see cref="BcsApiException"/> with the HTTP status of the answer.
    /// </summary>
    public async Task<WebSocket> ConnectAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        var accessToken = await _tokens
            .GetAccessTokenAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await _transport
                .ConnectAsync(url, $"Bearer {accessToken}", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (BcsApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            _tokens.InvalidateAccessToken(accessToken);
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
    public async Task<WebSocket> ConnectAndSubscribeAsync<TSubscribe>(
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
}
