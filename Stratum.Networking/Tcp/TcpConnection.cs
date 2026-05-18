/*
 * (TcpConnection.cs)
 *------------------------------------------------------------
 * Created - 5/17/2026 10:16:48 AM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using Stratum.Shared.Networking;
using System.Net;
using System.Net.Sockets;

namespace Stratum.Networking.Tcp;

public sealed class TcpConnection(
    long id,
    Socket socket,
    Stream stream,
    IPEndPoint remoteEndpoint)
{
    private int _disconnectRequested;
    private SecureDisconnectReason _disconnectReason;

    /// <summary>
    /// Id of our connection. Assigned by the server on accept and immutable for the connection's lifetime.
    /// </summary>
    public long Id { get; } = id;
    /// <summary>
    /// The underlying TCP Socket for our connection.
    /// </summary>
    public Socket Socket { get; } = socket;
    /// <summary>
    /// The underlying TCP stream to our connection. Used for reading and writing framed packets.
    /// </summary>
    public Stream Stream { get; } = stream;
    /// <summary>
    /// The endpoint of the remote peer we're connected to. Immutable for the connection's lifetime.
    /// </summary>
    public IPEndPoint RemoteEndpoint { get; } = remoteEndpoint;
    /// <summary>
    /// Semaphore used to synchronize send operations.
    /// </summary>
    public SemaphoreSlim SendLock { get; } = new(1, 1);
    /// <summary>
    /// Last detected activity on the connection, used for idle timeout detection. Updated on send and receive operations.
    /// </summary>
    public DateTime LastActivityUtc { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// Is the connection pending a disconnect or has been disconnected? The connection should be closed and cleaned up as soon as possible if this is true.
    /// </summary>
    public bool IsDisconnectRequested => Volatile.Read(ref _disconnectRequested) != 0;

    /// <summary>
    /// The reason this connection is currently requested for disconnect. Only meaningful if <see cref="IsDisconnectRequested"/> is true.
    /// </summary>
    public SecureDisconnectReason RequestedReason => _disconnectReason;



    /// <summary>
    /// Requests a secure disconnection with the specified reason.
    /// </summary>
    /// <remarks>This method is thread-safe. If called multiple times, only the first call will set the
    /// disconnect reason.</remarks>
    /// <param name="reason">The reason for the disconnection.</param>
    public void RequestDisconnect(SecureDisconnectReason reason)
    {
        if(Interlocked.Exchange(ref _disconnectRequested, 1) == 0)
            _disconnectReason = reason;
    }
}


/*
*------------------------------------------------------------
* (TcpConnection.cs)
* See License.txt for licensing information.
*-----------------------------------------------------------
*/