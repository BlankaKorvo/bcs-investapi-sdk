namespace Bcs.InvestApi.WebSockets;

using System.Net.WebSockets;

/// <summary>
/// Opens the WebSocket connection itself: the transport seam of <see cref="BcsWebSocketConnector"/>, as
/// <see cref="HttpMessageHandler"/> is for HTTP. The connector fetches the access token and builds the
/// <c>Authorization</c> header; the transport only opens the socket. The default is
/// <see cref="BcsClientWebSocketTransport"/>; tests pass their own through
/// <see cref="BcsInvestApiClientFactory.Create(BcsInvestApiSettings, HttpMessageHandler?, TimeProvider?, IBcsWebSocketTransport?)"/>.
/// </summary>
public interface IBcsWebSocketTransport
{
    /// <summary>
    /// Opens a connection to <paramref name="url"/> with <paramref name="authorization"/> as the value of the
    /// <c>Authorization</c> header and returns the open socket; the caller owns disposal. A handshake answered
    /// with an HTTP status other than 101 — <see cref="Contracts.Exceptions.BcsApiException"/> with that status.
    /// </summary>
    Task<WebSocket> ConnectAsync(Uri url, string authorization, CancellationToken cancellationToken);
}
