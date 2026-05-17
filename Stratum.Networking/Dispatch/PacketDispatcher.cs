/*
 * (PacketDispatcher.cs)
 *------------------------------------------------------------
 * Created - 5/16/2026 11:18:09 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using LiteNetLib.Utils;
using Stratum.Shared.Networking;

namespace Stratum.Networking.Dispatch
{
    /// <summary>
    /// Dispatches network packets to registered type-specific handlers based on packet type identifiers.
    /// </summary>
    /// <remarks>Must be frozen after registration and before dispatching packets.</remarks>
    /// <typeparam name="TConnection">The connection type passed to packet handlers.</typeparam>
    public sealed class PacketDispatcher<TConnection> where TConnection : class
    {
        private readonly Dictionary<uint, Func<TConnection, NetDataReader, DispatchResult>> _handlers = [];
        private bool _frozen;

        /// <summary>
        /// Gets a value indicating whether the PacketDispatcher is frozen.
        /// </summary>
        public bool IsFrozen => _frozen;
        /// <summary>
        /// Gets the number of registered handlers.
        /// </summary>
        public int RegistrationCount => _handlers.Count;

        /// <summary>
        /// Registers a packet handler with a deserialization function for the specified packet type.
        /// </summary>
        /// <typeparam name="TPacket">The packet type to register. Must implement <see cref="IPacketWritable"/> and contain a TypeId property.</typeparam>
        /// <param name="deserialize">Function that deserializes a packet from a <see cref="NetDataReader"/>.</param>
        /// <param name="handler">Action that handles the deserialized packet for a connection.</param>
        /// <exception cref="InvalidOperationException">Thrown when registration is attempted after <c>Freeze()</c> has been called, or when a handler is already
        /// registered for the packet's TypeId.</exception>
        public void Register<TPacket>(
            Func<NetDataReader, TPacket> deserialize,
            Action<TConnection, TPacket> handler)
            where TPacket : struct, IPacketWritable
        {
            ArgumentNullException.ThrowIfNull(deserialize);
            ArgumentNullException.ThrowIfNull(handler);

            if (_frozen)
            {
                throw new InvalidOperationException($"Cannot register handlers after Freeze().");
            }

            // Probe the typeId from a default instance. The interface property is required.
            uint typeId = default(TPacket).TypeId;

            if(_handlers.ContainsKey(typeId))
                throw new InvalidOperationException($"Handler already registered for typeId {typeId:X8}.");

            _handlers[typeId] = (connection, reader) =>
            {
                TPacket packet;
                try
                {
                    packet = deserialize(reader);
                }
                catch (Exception ex)
                {
                    return DispatchResult.Invalid(typeId, ex);
                }

                try
                {
                    handler(connection, packet);
                    return DispatchResult.Ok(typeId);
                }
                catch (Exception ex)
                {
                    return DispatchResult.Handler(typeId, ex);
                }
            };
        }

        /// <summary>
        /// Freezes this instance, preventing further modifications to PacketDispatcher.
        /// </summary>
        public void Freeze() => _frozen = true;

        /// <summary>
        /// Dispatches a packet to the appropriate handler based on the packet type identifier.
        /// </summary>
        /// <param name="typeId">The type identifier of the packet.</param>
        /// <param name="connection">The connection associated with the packet.</param>
        /// <param name="reader">The reader containing the packet data.</param>
        /// <returns>A <see cref="DispatchResult"/> indicating the outcome of the dispatch operation.</returns>
        public DispatchResult Dispatch(uint typeId, 
            TConnection connection, 
            NetDataReader reader) 
        {
            if(!_frozen)
                throw new InvalidOperationException($"Dispatcher must be frozen before Dispatch().");

            if (!_handlers.TryGetValue(typeId, out var handler))
            {
                return DispatchResult.Unknown(typeId);
            }

            return handler(connection, reader);
        }
    }
}

/*
 *------------------------------------------------------------
 * (PacketDispatcher.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */