/*
 * (TcpConnection.cs)
 *------------------------------------------------------------
 * Created - 5/11/2026 12:55:52 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using LiteNetLib;
using System.Net;
using System.Net.Security;

namespace Stratum.Networking.Tcp;

public sealed class TcpConnection(SslStream stream, IPEndPoint? remoteEndPoint, byte[] readBuffer)
{
    /// <summary>
    /// Gets the unique identifier for this instance.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>
    /// Gets the underlying SSL stream used for secure network communication.
    /// </summary>
    /// <remarks>The returned SslStream provides methods for reading and writing encrypted data over the
    /// network. Callers should use this property to access the secure stream for sending or receiving data. The caller
    /// is responsible for managing the lifetime and usage of the stream.</remarks>
    public SslStream Stream { get; } = stream;
    /// <summary>
    /// Gets the remote network endpoint associated with the connection.
    /// </summary>
    public IPEndPoint? RemoteEndPoint { get; } = remoteEndPoint ?? null;
    /// <summary>
    /// Gets the Coordinated Universal Time (UTC) when the connection was established.
    /// </summary>
    public DateTime ConnectedUtc { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the username associated with the current user or account.
    /// </summary>
    public string? Username { get; set; }
    /// <summary>
    /// Gets a value indicating whether the user is authenticated.
    /// </summary>
    public bool IsAuthenticated => Username is not null;

    /// <summary>
    /// Gets the internal buffer containing data read from the underlying stream.
    /// </summary>
    public byte[] ReadBuffer { get; } = readBuffer;

    /// <summary>
    /// Gets the semaphore used to synchronize send operations.
    /// </summary>
    /// <remarks>Use this semaphore to coordinate access to send-related resources in concurrent scenarios.
    /// The semaphore is initialized with a count of one, allowing only one sender at a time.</remarks>
    public SemaphoreSlim SendLock { get; } = new(1, 1);

    private int _disconnectRequested;
    private Stratum.Shared.Networking.DisconnectReason _pendingReason;
    private string _pendingDetail = string.Empty;

    /// <summary>
    /// Requests a disconnect from the remote endpoint with the specified reason and optional detail message.
    /// </summary>
    /// <remarks>If a disconnect has already been requested, subsequent calls to this method have no
    /// effect.</remarks>
    /// <param name="reason">The reason for the disconnect request. This value indicates why the disconnect is being initiated.</param>
    /// <param name="detail">An optional message providing additional details about the disconnect. May be an empty string if no additional
    /// information is needed.</param>
    public void RequestDisconnect(Stratum.Shared.Networking.DisconnectReason reason, string detail = "")
    {
        if (Interlocked.Exchange(ref _disconnectRequested, 1) != 0) return;

        _pendingReason = reason;
        _pendingDetail = detail;
    }

    /// <summary>
    /// Attempts to retrieve the reason and detail for a pending disconnect request, if one exists.
    /// </summary>
    /// <param name="reason">When this method returns, contains the reason for the pending disconnect if one exists; otherwise, the default
    /// value for the DisconnectReason type.</param>
    /// <param name="detail">When this method returns, contains additional detail about the pending disconnect if one exists; otherwise, an
    /// empty string.</param>
    /// <returns>true if a pending disconnect request exists and the reason and detail were retrieved; otherwise, false.</returns>
    public bool TryGetPendingDisconnect(out Stratum.Shared.Networking.DisconnectReason reason, out string detail)
    {
        if(Volatile.Read(ref _disconnectRequested) == 0)
        {
            reason = default;
            detail = string.Empty;
            return false;
        }

        reason = _pendingReason;
        detail = _pendingDetail;

        return true;
    }


}



/*
 *------------------------------------------------------------
 * (TcpConnection.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */