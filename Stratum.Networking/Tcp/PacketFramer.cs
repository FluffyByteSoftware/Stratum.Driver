/*
 * (PacketFramer.cs)
 *------------------------------------------------------------
 * Created - 5/17/2026 9:42:12 AM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using System.Buffers.Binary;

namespace Stratum.Networking.Tcp;

/// <summary>
/// Provides utilities for framing packets with 8-byte headers consisting of a length prefix and type identifier in
/// big-endian format.
/// </summary>
public static class PacketFramer
{
    /// <summary>
    /// The number of bytes in the header.
    /// </summary>
    public const int HeaderBytes = 8;

    /// <summary>
    /// Writes a header consisting of the total length (payload length + 4) and type identifier to the destination
    /// span in big-endian format.
    /// </summary>
    /// <param name="destination">The destination span to write the header bytes to. Must be at least 8 bytes long.</param>
    /// <param name="payloadLength">The length of the payload in bytes.</param>
    /// <param name="typeId">The type identifier.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is less than 8 bytes long.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="payloadLength"/> is negative.</exception>
    public static void WriteHeader(Span<byte> destination,
        int payloadLength,
        uint typeId)
    {
        if(destination.Length < HeaderBytes)
            throw new ArgumentException($"Destination must be at least {HeaderBytes} bytes long.", nameof(destination));

        ArgumentOutOfRangeException.ThrowIfNegative(payloadLength);

        BinaryPrimitives.WriteInt32BigEndian(destination, payloadLength + 4);
        BinaryPrimitives.WriteUInt32BigEndian(destination[4..], typeId);
    }

    /// <summary>
    /// Reads a 4-byte integer length prefix from a byte span in big-endian format.
    /// </summary>
    /// <param name="source">The byte span to read from.</param>
    /// <returns>The 32-bit integer value read from the span.</returns>
    /// <exception cref="ArgumentException"><paramref name="source"/> has fewer than 4 bytes.</exception>
    public static int ReadLengthPrefix(ReadOnlySpan<byte> source)
    {
        if(source.Length < 4)
            throw new ArgumentException("Source must be at least 4 bytes.", nameof(source));

        return BinaryPrimitives.ReadInt32BigEndian(source);
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer type identifier from a byte span in big-endian format.
    /// </summary>
    /// <param name="source">The byte span to read from.</param>
    /// <returns>The 32-bit unsigned integer type identifier read from the source.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> contains fewer than 4 bytes.</exception>
    public static uint ReadTypeId(ReadOnlySpan<byte> source)
    {
        if (source.Length < 4)
        {
            throw new ArgumentException("Source must be at least 4 bytes.", nameof(source));
        }

        return BinaryPrimitives.ReadUInt32BigEndian(source);
    }
        
}

/*
*------------------------------------------------------------
* (PacketFramer.cs)
* See License.txt for licensing information.
*-----------------------------------------------------------
*/