/*
 * (DisconnectPacket.cs)
 *------------------------------------------------------------
 * Created - 5/10/2026 6:00:00 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using LiteNetLib.Utils;
using Stratum.Shared.Networking;

namespace Stratum.Shared.Networking.Packets
{

    /// <summary>
    /// Sent by the server immediately before closing a connection, giving the client a structured
    /// reason and an optional human-readable detail string for display.
    /// </summary>
    /// <remarks>The server sends this packet, flushes the transport, then closes the underlying
    /// socket. Clients should not rely on receiving it (a network failure means the client may simply
    /// observe a closed socket with no preceding packet), but should respect it when present.</remarks>
    public readonly struct DisconnectPacket
    {
        public const uint TypeId = PacketIds.Disconnect;

        /// <summary>
        /// The structured reason for disconnection.
        /// </summary>
        public DisconnectReason Reason { get; }

        /// <summary>
        /// An optional human-readable detail to surface to the user (e.g. "Server restarting in 30s",
        /// "Account suspended pending review"). May be empty.
        /// </summary>
        public string Detail { get; }

        public DisconnectPacket(DisconnectReason reason, string detail = "")
        {
            Reason = reason;
            Detail = detail;
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((byte)Reason);
            writer.Put(Detail);
        }

        public static DisconnectPacket Deserialize(NetDataReader reader)
        {
            var reason = (DisconnectReason)reader.GetByte();
            var detail = reader.GetString();
            return new DisconnectPacket(reason, detail);
        }
    }
}

/*
 *------------------------------------------------------------
 * (DisconnectPacket.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */