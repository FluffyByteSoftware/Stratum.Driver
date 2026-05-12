/*
 * (PacketDispatcher.cs)
 *------------------------------------------------------------
 * Created - 5/11/2026 2:05:37 AM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using LiteNetLib;
using LiteNetLib.Utils;

namespace Stratum.Networking.Dispatch;

/// <summary>
/// Represents a method that can deserialize a packet of type T from a NetDataReader.
/// </summary>
/// <typeparam name="T">The type of the packet to deserialize.</typeparam>
/// <param name="reader">The NetDataReader containing the packet data.</param>
/// <returns>The deserialized packet of type T.</returns>
public delegate T PacketDeserializer<T>(NetDataReader reader) where T : struct;

/// <summary>
/// Represents a method that handles a received packet for a given connection.
/// </summary>
/// <remarks>Use this delegate to define custom logic for processing packets in network communication scenarios.
/// The packet parameter is passed by reference to avoid unnecessary copying of value types.</remarks>
/// <typeparam name="TConnection">The type representing the connection on which the packet was received.</typeparam>
/// <typeparam name="T">The value type of the packet to handle.</typeparam>
/// <param name="connection">The connection instance associated with the received packet.</param>
/// <param name="packet">The packet data to process. Passed by reference for efficiency.</param>
public delegate void PacketHandler<TConnection, T>(TConnection connection, in T packet) where T : struct;

/// <summary>
/// Represents a method that handles a raw network packet received from a connection.
/// </summary>
/// <remarks>Implement this delegate to process incoming raw packets for a specific connection type. The delegate
/// is typically invoked by the networking framework when a packet arrives.</remarks>
/// <typeparam name="TConnection">The type representing the connection from which the packet was received.</typeparam>
/// <param name="connection">The connection instance associated with the received packet.</param>
/// <param name="reader">A NetDataReader positioned at the start of the packet data to be processed.</param>
public delegate void RawPacketHandler<TConnection>(TConnection connection, NetDataReader reader);

/// <summary>
/// Provides a mechanism for registering and dispatching packet handlers based on packet type identifiers for a specific
/// connection type.
/// </summary>
/// <remarks>PacketDispatcher allows registration of handlers for different packet types, identified by a unique
/// type ID. Once frozen, no further handlers can be registered, and packets can be dispatched to the appropriate
/// handler. This class is not thread-safe; synchronization is required if accessed concurrently.</remarks>
/// <typeparam name="TConnection">The type representing a connection context for packet handling operations.</typeparam>
public sealed class PacketDispatcher<TConnection>
{
    private readonly Dictionary<uint, RawPacketHandler<TConnection>> _handlers = [];
    private bool _frozen;

    /// <summary>
    /// Gets a value indicating whether the dispatcher has been frozen, preventing further handler registrations.
    /// </summary>
    public bool IsFrozen => _frozen;
    /// <summary>
    /// Gets the number of registered handlers.
    /// </summary>
    public int HandlerCount => _handlers.Count;

    /// <summary>
    /// Registers a packet handler and deserializer for the specified packet type identifier.
    /// </summary>
    /// <remarks>Registering a handler for the same type ID more than once is not allowed. This method must be
    /// called before the dispatcher is frozen.</remarks>
    /// <typeparam name="T">The packet type to be handled. Must be a value type.</typeparam>
    /// <param name="typeId">The unique identifier for the packet type. Each type ID must be registered only once.</param>
    /// <param name="deserialize">A delegate that deserializes the packet data from the input reader into an instance of type T.</param>
    /// <param name="handler">A delegate that processes the deserialized packet for a given connection.</param>
    /// <exception cref="InvalidOperationException">Thrown if the dispatcher has been frozen and no further handlers can be registered.</exception>
    /// <exception cref="ArgumentException">Thrown if a handler is already registered for the specified type ID.</exception>
    public void Register<T>(
        uint typeId,
        PacketDeserializer<T> deserialize,
        PacketHandler<TConnection, T> handler) where T : struct
    {
        if (_frozen)
            throw new InvalidOperationException("Cannot register handlers after dispatcher has been frozen.");
            
        if (_handlers.ContainsKey(typeId))
            throw new ArgumentException($"Handler already registered for type ID 0x{typeId:X8}.", nameof(typeId));
            
        _handlers[typeId] = (conn, reader) =>
        {
            var packet = deserialize(reader);
            handler(conn, in packet);
        };
    }

    /// <summary>
    /// Registers a handler for processing packets of the specified type.
    /// </summary>
    /// <remarks>Registering a handler for a type ID that is already associated with another handler is not
    /// allowed. Once the dispatcher is frozen, no additional handlers can be registered.</remarks>
    /// <param name="typeId">The unique identifier for the packet type to associate with the handler.</param>
    /// <param name="handler">The delegate that processes packets of the specified type. Cannot be null.</param>
    /// <exception cref="InvalidOperationException">Thrown if the dispatcher has been frozen and no further handlers can be registered.</exception>
    /// <exception cref="ArgumentException">Thrown if a handler is already registered for the specified type ID.</exception>
    public void Register(uint typeId, RawPacketHandler<TConnection> handler)
    {
        if(_frozen)
            throw new InvalidOperationException("Cannot register handlers after dispatcher has been frozen.");

        if(_handlers.ContainsKey(typeId))
            throw new ArgumentException($"Handler already registered for type ID 0x{typeId:X8}.", nameof(typeId));
            
        _handlers[typeId] = handler;
    }

    /// <summary>
    /// Prevents further modifications to the current object.
    /// </summary>
    /// <remarks>After calling this method, attempts to change the object's state may be ignored or result in
    /// exceptions, depending on the implementation. Use this method to make the object immutable for the remainder of
    /// its lifetime.</remarks>
    public void Freeze() => _frozen = true;

    /// <summary>
    /// Dispatches an incoming packet to the registered handler for the specified type identifier.
    /// </summary>
    /// <remarks>If no handler is registered for the specified type identifier, the method returns a result
    /// with an outcome of UnknownType. If the handler throws an InvalidPacketException or any other exception, the
    /// result will indicate the corresponding error.</remarks>
    /// <param name="connection">The connection instance associated with the incoming packet. Used to provide context to the handler.</param>
    /// <param name="typeId">The type identifier of the packet to dispatch. Determines which handler will be invoked.</param>
    /// <param name="reader">A reader positioned at the start of the packet payload. Used by the handler to read packet data.</param>
    /// <returns>A DispatchResult indicating the outcome of the dispatch operation, including whether the packet was handled, the
    /// type was unknown, or an error occurred.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the dispatcher has not been frozen prior to dispatching.</exception>
    public DispatchResult Dispatch(TConnection connection, uint typeId, NetDataReader reader)
    {
        if(!_frozen)
            throw new InvalidOperationException("Dispatcher must be frozen before dispatch.");

        if (!_handlers.TryGetValue(typeId, out var handler))
            return new DispatchResult(DispatchOutcome.UnknownType, typeId);

        try
        {
            handler(connection, reader);
            return DispatchResult.Handled(typeId);
        }
        catch(InvalidPacketException ex)
        {
            return new DispatchResult(DispatchOutcome.InvalidPacket, typeId, ex);
        }
        catch(Exception ex)
        {
            return new DispatchResult(DispatchOutcome.HandlerException, typeId, ex);
        }
    }
}



/*
 *------------------------------------------------------------
 * (PacketDispatcher.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */