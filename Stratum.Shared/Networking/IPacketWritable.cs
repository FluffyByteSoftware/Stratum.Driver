/*
 * (IPacketWritable.cs)
 *------------------------------------------------------------
 * Created - 5/16/2026 9:27:30 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using LiteNetLib.Utils;

namespace Stratum.Shared.Networking
{

    public interface IPacketWritable
    {
        uint TypeId { get; }
        void Serialize(NetDataWriter writer);
    }

}

/*
 *------------------------------------------------------------
 * (IPacketWritable.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */