/*
 * (PacketFramer.cs)
 *------------------------------------------------------------
 * Created - 5/11/2026 1:14:38 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using System.Buffers.Binary;

namespace Stratum.Networking.Tcp;

/// <summary>
/// Provides static methods and constants for framing and parsing length-prefixed packets with protocol headers.
/// </summary>
/// <remarks>This class defines constants for packet and header sizes, and provides methods for reading and
/// writing frames to streams using a length-prefixed protocol. All members are static and thread-safe. Use this class
/// to ensure consistent framing when sending or receiving packets over a stream-based transport.</remarks>
public static class PacketFramer
{
    /// <summary>
    /// Specifies the maximum allowed size, in bytes, for a packet.
    /// </summary>
    /// <remarks>Use this constant to validate or limit packet sizes when sending or receiving data. Packets
    /// larger than this value may be rejected or truncated, depending on implementation.</remarks>
    public const int MaxPacketBytes = 4096;
    /// <summary>
    /// Specifies the number of bytes in the protocol header.
    /// </summary>
    public const int HeaderBytes = 8;

    /// <summary>
    /// Asynchronously reads a length-prefixed frame from the specified stream into the provided buffer.
    /// </summary>
    /// <remarks>The method expects the frame to be prefixed with a 4-byte big-endian integer indicating the
    /// total frame length, including the prefix itself. The buffer must be at least as large as the frame
    /// length.</remarks>
    /// <param name="stream">The stream from which to read the frame. Must be readable.</param>
    /// <param name="buffer">The buffer to receive the frame data. Must be large enough to hold the entire frame.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous read operation. The result contains the total number of bytes read for
    /// the frame.</returns>
    /// <exception cref="InvalidDataException">Thrown if the frame length read from the stream is less than 4 or greater than the maximum allowed packet size.</exception>
    public static async Task<int> ReadFrameAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken ct)
    {
        await ReadExactAsync(stream, buffer, 0, 4, ct).ConfigureAwait(false);

        int length = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(0, 4));

        if(length < 4 || length > MaxPacketBytes)
            throw new InvalidDataException($"Invalid frame length: {length}");

        await ReadExactAsync(stream, buffer, 0, length, ct).ConfigureAwait(false);

        return length;
    }

    /// <summary>
    /// Writes a frame to the specified stream using the provided type identifier and payload.
    /// </summary>
    /// <remarks>The frame consists of an 8-byte header followed by the payload. The header includes the total
    /// frame length and the type identifier, both written in big-endian format. The method does not close or flush the
    /// stream.</remarks>
    /// <param name="stream">The stream to which the frame will be written. Must be writable.</param>
    /// <param name="typeId">The type identifier to include in the frame header.</param>
    /// <param name="payload">The payload data to include in the frame. May be empty.</param>
    public static void WriteFrame(Stream stream, uint typeId, ReadOnlySpan<byte> payload)
    {
        int length = 4 + payload.Length;

        Span<byte> header = stackalloc byte[8];

        BinaryPrimitives.WriteInt32BigEndian(header[..4], length);
        BinaryPrimitives.WriteUInt32BigEndian(header.Slice(4, 4), typeId);

        stream.Write(header);

        if (payload.Length > 0)
            stream.Write(payload);
    }

    /// <summary>
    /// Asynchronously reads the exact number of bytes specified from the stream into the buffer, starting at the given
    /// offset.
    /// </summary>
    /// <remarks>The method continues reading until the specified number of bytes has been read or the end of
    /// the stream is reached. If the operation is canceled via the cancellation token, the returned task is
    /// canceled.</remarks>
    /// <param name="stream">The stream from which bytes are read. Must be readable and support asynchronous operations.</param>
    /// <param name="buffer">The buffer to store the read bytes. Must have sufficient space to accommodate the requested number of bytes
    /// starting at the specified offset.</param>
    /// <param name="offset">The zero-based byte offset in the buffer at which to begin storing the data read from the stream. Must be
    /// non-negative and less than the length of the buffer.</param>
    /// <param name="count">The exact number of bytes to read from the stream. Must be non-negative and less than or equal to the number of
    /// bytes available in the buffer from the specified offset.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the asynchronous read operation.</param>
    /// <returns>A task that represents the asynchronous read operation.</returns>
    /// <exception cref="EndOfStreamException">Thrown if the end of the stream is reached before the requested number of bytes could be read.</exception>
    private static async Task ReadExactAsync(
        Stream stream,
        byte[] buffer,
        int offset,
        int count,
        CancellationToken ct)
    {
        int read = 0;

        while(read < count)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(offset + read, count - read), ct)
                .ConfigureAwait(false);

            if (n == 0)
                throw new EndOfStreamException($"Peer closed before frame completed.");

            read += n;
        }
    }
}



/*
 *------------------------------------------------------------
 * (PacketFramer.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */