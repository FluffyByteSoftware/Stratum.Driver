/*
 * (InvalidPacketException.cs)
 *------------------------------------------------------------
 * Created - 5/10/2026 6:00:00 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using System;

namespace Stratum.Shared.Networking
{

    /// <summary>
    /// Thrown by packet <c>Deserialize</c> methods when incoming bytes violate the packet's expected
    /// shape (wrong field lengths, invalid enum values, structural issues that are not transport-level
    /// problems). Receivers catch this and disconnect the peer with
    /// <see cref="DisconnectReason.InvalidPacket"/>.
    /// </summary>
    public sealed class InvalidPacketException : Exception
    {
        public InvalidPacketException(string message) : base(message) { }
        public InvalidPacketException(string message, Exception inner) : base(message, inner) { }
    }
}

/*
 *------------------------------------------------------------
 * (InvalidPacketException.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */