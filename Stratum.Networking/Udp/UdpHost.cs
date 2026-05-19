/*
 * (UdpHost.cs)
 *------------------------------------------------------------
 * Created - 5/18/2026 9:04:37 AM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using LiteNetLib;
using LiteNetLib.Utils;
using Stratum.Networking.Dispatch;
using Stratum.Shared.Networking;
using Stratum.SystemTools.Clock;
using Stratum.SystemTools.Logger;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Stratum.Networking.Udp;

public sealed class UdpHost : INetEventListener, ITickable
{
    private readonly int _bindPort;
    private readonly PacketDispatcher<UdpConnection> _dispatcher;
    private readonly Func<NetPeer, bool> _connectionAuthorizer;
    private readonly ConcurrentDictionary<int, UdpConnection> 
        _connectionsByPeerId = new();
    private readonly NetManager _net;

    private long _nextConnectionId;
    private bool _started;

    public string Name => "UdpHost";
    public int ConnectionCount => _connectionsByPeerId.Count;
    public int BindPort => _bindPort;

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpHost"/> class.
    /// </summary>
    /// <param name="bindPort">The port number to bind the UDP host to.</param>
    /// <param name="dispatcher">The packet dispatcher for handling 
    /// connections. Must be frozen before construction.</param>
    /// <param name="connectionAuthorizer">A function to authorize incoming 
    /// connections. If <c>null</c>, all connections are authorized by default.</param>
    /// <exception cref="ArgumentException">Thrown when 
    /// <paramref name="dispatcher"/> is not frozen.</exception>
    public UdpHost(
        int bindPort,
        PacketDispatcher<UdpConnection> dispatcher,
        Func<NetPeer, bool>? connectionAuthorizer = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        if (!dispatcher.IsFrozen)
        {
            throw new ArgumentException(
                $"Dispatcher must be frozen before host construction.",
                nameof(dispatcher));
        }

        _bindPort = bindPort;
        _dispatcher = dispatcher;
        _connectionAuthorizer = connectionAuthorizer ?? (_ => true);
        _net = new NetManager(this) { AutoRecycle = true };
    }

    /// <summary>
    /// Starts the UDP host and begins listening on the configured port.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the host has already been started, or if binding to the 
    /// UDP port fails.</exception>
    public void Start()
    {
        if (_started)
        {
            throw new InvalidOperationException(
                "UdpHost has already been started.");
        }

        if (!_net.Start(_bindPort))
        {
            throw new InvalidOperationException(
                $"UdpHost failed to bind UDP port {_bindPort}.");
        }

        _started = true;

        Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
            $"UdpHost listening on UDP Port {_bindPort}."));
    }

    /// <summary>
    /// Stops the network service and clears all active connections.
    /// </summary>
    /// <returns>A completed task.</returns>
    public Task StopAsync()
    {
        if (!_started)
            return Task.CompletedTask;

        _started = false;

        _net.Stop(sendDisconnectMessages: false);
        _connectionsByPeerId.Clear();
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes a single tick update, polling network events if the network has been started.
    /// </summary>
    /// <param name="context">The tick context.</param>
    public void Tick(in TickContext context)
    {
        if (_started)
            _net.PollEvents();
    }

    /// <summary>
    /// Sends a packet over the specified UDP connection using the given delivery method.
    /// </summary>
    /// <typeparam name="TPacket">The packet type that implements <see cref="IPacketWritable"/>.</typeparam>
    /// <param name="connection">The UDP connection to send the packet through.</param>
    /// <param name="packet">The packet to serialize and send.</param>
    /// <param name="deliveryMethod">The delivery method to use for sending the packet.</param>
    /// <returns><see langword="true"/> if the packet was sent successfully; otherwise, <see langword="false"/>.</returns>
    public bool Send<TPacket>(
        UdpConnection connection,
        TPacket packet,
        DeliveryMethod deliveryMethod) 
        where TPacket : struct, IPacketWritable
    {
        if (!_started) return false;

        if (connection.IsDisconnectRequested) return false;

        var writer = new NetDataWriter();
        writer.Put(packet.TypeId);
        packet.Serialize(writer);

        try
        {
            connection.Peer.Send(writer, deliveryMethod);
            return true;
        }
        catch(Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
                $"UDP send failed on connection {connection.Id} " +
                $"(0x{packet.TypeId:X8}).",
                ex));

            connection.RequestDisconnect(SecureDisconnectReason.SendFailure);

            return false;
        }
    }

    void INetEventListener.OnConnectionRequest(ConnectionRequest request)
    {
        if (_connectionAuthorizer(null!))
        {
            request.Accept();
        }
        else
        {
            request.Reject();
        }
    }

    void INetEventListener.OnPeerConnected(NetPeer peer)
    {
        var id = Interlocked.Increment(ref _nextConnectionId);
        var connection = new UdpConnection(id, peer);

        _connectionsByPeerId[peer.Id] = connection;

        Scribe.Pump(new ScribeMessage(
            ScribeSeverity.Info,
            $"UDP connection {id} from {connection.RemoteEndpoint}."));
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, 
        DisconnectInfo disconnectInfo)
    {
        if(_connectionsByPeerId.TryRemove(peer.Id, out var conn))
        {
            Scribe.Pump(new ScribeMessage(
                ScribeSeverity.Info,
                $"UDP connection {conn.Id} disconnected " +
                $"({disconnectInfo.Reason})."));
        }
    }

    void INetEventListener.OnNetworkReceive(NetPeer peer, 
        NetPacketReader reader, 
        byte channelNumber, 
        DeliveryMethod deliveryMethod)
    {
        if(!_connectionsByPeerId.TryGetValue(peer.Id, out var connection))
        {
            reader.Recycle();
            return;
        }

        if(reader.AvailableBytes < 4)
        {
            connection.RequestDisconnect(SecureDisconnectReason.InvalidFrame);
            reader.Recycle();
            return;
        }

        uint typeId = reader.GetUInt();
        connection.LastActivityUtc = DateTime.UtcNow;

        var result = _dispatcher.Dispatch(typeId, connection, reader);
        
        if (!result.IsOk)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
                $"UDP dispatch on connection {connection.Id} return " +
                $"{result.Outcome} for 0x{typeId:X8}.", 
                result.Exception));

            connection.RequestDisconnect(result.Outcome switch
            {
                DispatchOutcome.UnknownType =>
                SecureDisconnectReason.UnknownPacket,
                DispatchOutcome.InvalidPacket =>
                SecureDisconnectReason.InvalidFrame,
                _ => SecureDisconnectReason.HandlerError
            });
        }

        reader.Recycle();
    }

    void INetEventListener.OnNetworkError(IPEndPoint endPoint, 
        SocketError socketError)
    {
        Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
            $"UDP network error from {endPoint}: {socketError}."));

    }

    void INetEventListener.OnNetworkReceiveUnconnected(
        IPEndPoint remoteEndPoint,
        NetPacketReader reader,
        UnconnectedMessageType messageType)
    {
        reader.Recycle();
    }

    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) 
    { }
}



/*
 *------------------------------------------------------------
 * (UdpHost.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */