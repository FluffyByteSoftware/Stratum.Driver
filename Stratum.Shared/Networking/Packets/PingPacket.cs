/*
 * (PingPacket.cs)
 *------------------------------------------------------------
 * Created - 5/10/2026 6:00:00 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using LiteNetLib.Utils;

namespace Stratum.Shared.Networking.Packets
{

    /// <summary>
    /// A keep-alive and round-trip-time probe. The sender stamps the current tick or timestamp; the
    /// receiver may echo it back to compute RTT or simply update an activity marker.
    /// </summary>
    public readonly struct PingPacket
    {
        public const uint TypeId = PacketIds.Ping;

        /// <summary>
        /// A sender-chosen timestamp, typically <see cref="System.DateTime.UtcNow"/> ticks. Opaque to
        /// the receiver; round-tripped if the receiver chooses to respond with its own ping.
        /// </summary>
        public long Timestamp { get; }

        public PingPacket(long timestamp)
        {
            Timestamp = timestamp;
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Timestamp);
        }

        public static PingPacket Deserialize(NetDataReader reader)
        {
            return new PingPacket(reader.GetLong());
        }
    }
}

/*
 *------------------------------------------------------------
 * (PingPacket.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */