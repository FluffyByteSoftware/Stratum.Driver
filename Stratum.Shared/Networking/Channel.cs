/*
 * (Channel.cs)
 *------------------------------------------------------------
 * Created - 5/10/2026 6:00:00 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

namespace Stratum.Shared.Networking
{

    /// <summary>
    /// Logical groupings of related packets. The channel occupies the high byte of every packet's 4-byte
    /// type identifier; the remaining 3 bytes are the packet ID within the channel.
    /// </summary>
    /// <remarks>Channels are used for organizing packet IDs, applying channel-wide policies (e.g. "all
    /// Combat packets require an authenticated session"), and mapping to LiteNetLib reliability channels
    /// on the UDP transport side.</remarks>
    public enum Channel : byte
    {
        /// <summary>
        /// Protocol-level packets: ping, disconnect, error reports.
        /// </summary>
        System = 0x00,

        /// <summary>
        /// Authentication and session establishment: login requests, token issuance.
        /// </summary>
        Auth = 0x01,

        /// <summary>
        /// Post-login session management: zone transfer, session heartbeats.
        /// </summary>
        Session = 0x02,

        /// <summary>
        /// In-game text communication.
        /// </summary>
        Chat = 0x03,

        /// <summary>
        /// Position, rotation, and input.
        /// </summary>
        Movement = 0x04,

        /// <summary>
        /// Attacks, damage, abilities, status effects.
        /// </summary>
        Combat = 0x05,

        /// <summary>
        /// Voxel deltas and world-level events.
        /// </summary>
        World = 0x06,

        /// <summary>
        /// Item moves, equip, drop, container interactions.
        /// </summary>
        Inventory = 0x07,

        /// <summary>
        /// Player-to-player trade workflow.
        /// </summary>
        Trade = 0x08,

        /// <summary>
        /// Friends, groups, guilds, and other social structures.
        /// </summary>
        Social = 0x09,
    }
}
/*
 *------------------------------------------------------------
 * (Channel.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */