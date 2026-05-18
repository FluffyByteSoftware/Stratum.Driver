/*
 * (SecureDisconnectReason.cs)
 *------------------------------------------------------------
 * Created - 5/17/2026 10:18:44 AM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

namespace Stratum.Shared.Networking
{
    public enum SecureDisconnectReason
    {
        /// <summary>
        /// Unknown reason or error occurred.
        /// </summary>
        Unspecified = 0,
        /// <summary>
        /// Server is shutting down.
        /// </summary>
        ServerShutdown,
        /// <summary>
        /// The connection has timed out.
        /// </summary>
        IdleTimeout,
        /// <summary>
        /// The peer has disconnected.
        /// </summary>
        PeerClosed,
        /// <summary>
        /// The frame received or sent was malformed.
        /// </summary>
        InvalidFrame,
        /// <summary>
        /// The packet type was indeterminable.
        /// </summary>
        UnknownPacket,
        /// <summary>
        /// Something was corrupt or unreadable with the packet.
        /// </summary>
        HandlerError,
        /// <summary>
        /// The system failed to send a packet.
        /// </summary>
        SendFailure,
        /// <summary>
        /// Authentication request rejected.
        /// </summary>
        AuthRejected,
        /// <summary>
        /// Authentication request timed out.
        /// </summary>
        AuthTimeout,
        /// <summary>
        /// The user has been intentionally kicked by the server.
        /// </summary>
        Kicked,
    }
}

/*
 *------------------------------------------------------------
 * (SecureDisconnectReason.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */