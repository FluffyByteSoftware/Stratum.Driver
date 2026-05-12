/*
 * (AuthResponsePacket.cs)
 *------------------------------------------------------------
 * Created - 5/10/2026 6:00:00 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using LiteNetLib.Utils;

namespace Stratum.Shared.Networking.Packets
{

    /// <summary>
    /// A server-to-client response confirming a successful authentication. Carries a short-lived
    /// session token the client uses to identify itself to <see cref="System"/> ConnectionManager when
    /// it transitions to the in-game UDP connection.
    /// </summary>
    /// <remarks>This packet is only sent on success. Authentication failures manifest as a
    /// <see cref="DisconnectPacket"/> with <see cref="DisconnectReason.AuthFailed"/>, immediately
    /// followed by the server closing the connection.</remarks>
    public readonly struct AuthResponsePacket
    {
        public const uint TypeId = PacketIds.AuthResponse;

        /// <summary>
        /// The session token issued by the LoginServer. The client presents this token to the
        /// ConnectionManager when establishing the in-game UDP connection.
        /// </summary>
        public string Token { get; }

        public AuthResponsePacket(string token)
        {
            Token = token;
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Token);
        }

        public static AuthResponsePacket Deserialize(NetDataReader reader)
        {
            return new AuthResponsePacket(reader.GetString());
        }
    }
}

/*
 *------------------------------------------------------------
 * (AuthResponsePacket.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */