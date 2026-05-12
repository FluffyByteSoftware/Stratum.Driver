/*
 * (DisconnectReason.cs)
 *------------------------------------------------------------
 * Created - 5/10/2026 6:00:00 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

namespace Stratum.Shared.Networking
{

    /// <summary>
    /// The reason a connection is being terminated, sent in a <see cref="DisconnectPacket"/> immediately
    /// before the server closes the underlying transport.
    /// </summary>
    public enum DisconnectReason : byte
    {
        /// <summary>
        /// No reason specified or reason unknown. Avoid using directly; prefer a more specific value.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The server is shutting down gracefully.
        /// </summary>
        ServerShutdown,

        /// <summary>
        /// The connection idled past the configured timeout without traffic.
        /// </summary>
        Timeout,

        /// <summary>
        /// The underlying network transport failed (socket error, TLS error, etc.).
        /// </summary>
        NetworkFailure,

        /// <summary>
        /// Authentication was rejected (unknown username, signature did not verify, account locked).
        /// </summary>
        AuthFailed,

        /// <summary>
        /// The peer sent a malformed packet or violated protocol expectations.
        /// </summary>
        InvalidPacket,

        /// <summary>
        /// The peer's protocol version is incompatible with the server's.
        /// </summary>
        VersionMismatch,

        /// <summary>
        /// An administrator forcibly disconnected this peer.
        /// </summary>
        KickedByAdmin,

        /// <summary>
        /// The same account connected from another endpoint; this older connection is being closed.
        /// </summary>
        Duplicate,

        /// <summary>
        /// Indicates that the server has reached its maximum capacity and cannot accept additional connections or
        /// requests.
        /// </summary>
        ServerFull,
    }
}

/*
 *------------------------------------------------------------
 * (DisconnectReason.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */