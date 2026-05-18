/*
 * (UdpConnection.cs)
 *------------------------------------------------------------
 * Created - 5/18/2026 7:54:47 AM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using LiteNetLib;
using Stratum.Shared.Networking;
using System.Net;

namespace Stratum.Networking.Udp;

public sealed class UdpConnection(long id, NetPeer peer)
{
    private int _disconnectRequested;
    private SecureDisconnectReason _disconnectReason;

    /// <summary>
    /// Id of our connection. Assigned by the server on accept and immutable
    /// for the connection's lifetime.
    /// </summary>
    public long Id { get; } = id;

    /// <summary>
    /// The underlying LiteNetLib peer for our connection.
    /// </summary>
    public NetPeer Peer { get; } = peer;

    /// <summary>
    /// The endpoint of the remote peer we're connected to. Immutable for the
    /// connection's lifetime, cached at construction.
    /// </summary>
    public IPEndPoint RemoteEndpoint { get; }
        = new IPEndPoint(peer.Address, peer.Port);

    /// <summary>
    /// Last detected activity on the connection, used for idle timeout
    /// detection. Updated on send and receive operations.
    /// </summary>
    public DateTime LastActivityUtc { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// Is the connection pending a disconnect or has been disconnected? The
    /// connection should be closed and cleaned up as soon as possible if this
    /// is true.
    /// </summary>
    public bool IsDisconnectRequested =>
        Volatile.Read(ref _disconnectRequested) != 0;

    /// <summary>
    /// The reason this connection is currently requested for disconnect. Only
    /// meaningful if <see cref="IsDisconnectRequested"/> is true.
    /// </summary>
    public SecureDisconnectReason RequestedReason => _disconnectReason;

    /// <summary>
    /// Requests a disconnect with the specified reason. Only the first request
    /// is honored; subsequent calls are no-ops.
    /// </summary>
    /// <param name="reason">The reason for disconnecting.</param>
    public void RequestDisconnect(SecureDisconnectReason reason)
    {
        if (Interlocked.Exchange(ref _disconnectRequested, 1) == 0)
            _disconnectReason = reason;
    }
}

/*
 *------------------------------------------------------------
 * (UdpConnection.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */