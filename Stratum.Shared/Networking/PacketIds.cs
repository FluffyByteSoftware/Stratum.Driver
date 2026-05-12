/*
 * (PacketIds.cs)
 *------------------------------------------------------------
 * Created - 5/10/2026 6:00:00 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

namespace Stratum.Shared.Networking
{
    /// <summary>
    /// The canonical packet type identifiers shared between client and server. Each ID is a packed
    /// <see cref="uint"/> where the high byte is the <see cref="Channel"/> and the low three bytes are
    /// the packet's position within that channel.
    /// </summary>
    /// <remarks>The dispatch table on each receiver maps these IDs to concrete handlers. IDs are stable
    /// once published; new packet types append, never reuse existing IDs.</remarks>
    public static class PacketIds
    {
        // ---- System (0x00) ----
        public const uint Ping = 0x00_00_00_01;
        public const uint Disconnect = 0x00_00_00_02;

        // ---- Auth (0x01) ----
        public const uint AuthRequest = 0x01_00_00_01;
        public const uint AuthResponse = 0x01_00_00_02;

        /// <summary>
        /// Extracts the <see cref="Channel"/> from a packet type identifier.
        /// </summary>
        public static Channel GetChannel(uint typeId) => (Channel)((typeId >> 24) & 0xFF);
    }
}

/*
 *------------------------------------------------------------
 * (PacketIds.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */