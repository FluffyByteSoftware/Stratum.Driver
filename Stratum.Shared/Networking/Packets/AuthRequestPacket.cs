/*
 * (AuthRequestPacket.cs)
 *------------------------------------------------------------
 * Created - 5/10/2026 6:00:00 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using LiteNetLib;
using LiteNetLib.Utils;

namespace Stratum.Shared.Networking.Packets
{

    /// <summary>
    /// A client-to-server login request. The client names itself and provides an Ed25519 signature
    /// over a server-issued challenge, proving ownership of the private key paired with the public key
    /// stored in the server's allowlist.
    /// </summary>
    /// <remarks>Signatures are always exactly <see cref="Ed25519SignatureLength"/> bytes; deserialization
    /// rejects any other length as a malformed packet and the connection should be disconnected with
    /// <see cref="DisconnectReason.InvalidPacket"/>.</remarks>
    public readonly struct AuthRequestPacket
    {
        public const uint TypeId = PacketIds.AuthRequest;

        /// <summary>
        /// The expected length in bytes of a valid Ed25519 signature.
        /// </summary>
        public const int Ed25519SignatureLength = 64;

        /// <summary>
        /// The username (or account identifier) claiming this login.
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// The Ed25519 signature over the server's auth challenge. Always exactly
        /// <see cref="Ed25519SignatureLength"/> bytes.
        /// </summary>
        public byte[] Signature { get; }

        public AuthRequestPacket(string username, byte[] signature)
        {
            Username = username;
            Signature = signature;
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Username);
            writer.PutBytesWithLength(Signature);
        }

        public static AuthRequestPacket Deserialize(NetDataReader reader)
        {
            var username = reader.GetString();
            var signature = reader.GetBytesWithLength();

            if (signature.Length != Ed25519SignatureLength)
                throw new InvalidPacketException(
                    $"AuthRequest signature length is {signature.Length}, expected {Ed25519SignatureLength}.");

            return new AuthRequestPacket(username, signature);
        }
    }
}

/*
 *------------------------------------------------------------
 * (AuthRequestPacket.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */