/*
 * (TcpHost.cs)
 *------------------------------------------------------------
 * Created - 5/11/2026 1:35:55 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using LiteNetLib.Utils;
using Stratum.Networking.Dispatch;
using Stratum.Shared.Networking;
using Stratum.Shared.Networking.Packets;
using Stratum.SystemTools.Logger;

namespace Stratum.Networking.Tcp;

/// <summary>
/// Provides a TCP server host that listens for incoming connections, authenticates clients using TLS, and dispatches
/// received packets to registered handlers.
/// </summary>
/// <remarks>TcpHost manages the lifecycle of TCP connections, including secure authentication, packet framing,
/// and dispatching. It enforces a maximum connection limit and handles connection teardown and error reporting. The
/// host requires a frozen PacketDispatcher for operation and is not thread-safe for concurrent Start or StopAsync
/// calls. Typical usage involves constructing the host, starting it, and sending packets to connected clients. TcpHost
/// is intended for use in server applications that require secure, packet-based communication over TCP.</remarks>
public sealed class TcpHost
{
    private const int MaxConnections = 32;
    private static readonly TimeSpan AuthTimeout = TimeSpan.FromSeconds(5);

    private readonly IPEndPoint _bindEndpoint;
    private readonly X509Certificate2 _cert;
    private readonly PacketDispatcher<TcpConnection> _dispatcher;
    private readonly ConcurrentDictionary<Guid, TcpConnection> _connections = new();
    private readonly CancellationTokenSource _cts = new();
    private TcpListener? _listener;
    private Task? _acceptTask;

    /// <summary>
    /// Initializes a new instance of the TcpHost class with the specified local endpoint, server certificate, and
    /// packet dispatcher.
    /// </summary>
    /// <param name="bind">The local network endpoint on which the host will listen for incoming TCP connections.</param>
    /// <param name="cert">The X.509 certificate used to authenticate the server and encrypt connections.</param>
    /// <param name="dispatcher">The packet dispatcher responsible for handling incoming packets for each TCP connection. Must be frozen before
    /// the host is started.</param>
    /// <exception cref="ArgumentException">Thrown if dispatcher is not frozen before the host is started.</exception>
    public TcpHost(IPEndPoint bind, X509Certificate2 cert, PacketDispatcher<TcpConnection> dispatcher)
    {
        if (!dispatcher.IsFrozen)
            throw new ArgumentException("Dispatcher must be frozen before host start.", nameof(dispatcher));
        _bindEndpoint = bind;
        _cert = cert;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Gets the number of active connections.
    /// </summary>
    public int ConnectionCount => _connections.Count;

    /// <summary>
    /// Starts the TCP host and begins listening for incoming connections.
    /// </summary>
    /// <remarks>This method initializes the underlying TCP listener and begins accepting connections
    /// asynchronously. Call this method before attempting to accept or process client connections. This method is not
    /// thread-safe; concurrent calls may result in exceptions.</remarks>
    /// <exception cref="InvalidOperationException">Thrown if the TCP host has already been started.</exception>
    public void Start()
    {
        if (_listener is not null)
            throw new InvalidOperationException("TcpHost already started.");
        _listener = new TcpListener(_bindEndpoint);
        _listener.Start();
        _acceptTask = Task.Run(() => AcceptLoop(_cts.Token));
        Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
            $"TcpHost listening on {_bindEndpoint}"));
    }

    /// <summary>
    /// Stops the server asynchronously, disconnecting all active connections and releasing resources.
    /// </summary>
    /// <remarks>This method cancels any pending accept operations, requests all active connections to
    /// disconnect, and waits briefly for connections to close before disposing resources. It is safe to call this
    /// method multiple times; subsequent calls have no effect if the server is already stopped.</remarks>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async Task StopAsync()
    {
        if (_listener is null) return;
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        foreach (var conn in _connections.Values)
            conn.RequestDisconnect(DisconnectReason.ServerShutdown);
        if (_acceptTask is not null)
        {
            try { await _acceptTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!_connections.IsEmpty && DateTime.UtcNow < deadline)
            await Task.Delay(50).ConfigureAwait(false);
        _cts.Dispose();
    }

    /// <summary>
    /// Sends a serialized packet of the specified value type to the given TCP connection.
    /// </summary>
    /// <remarks>The packet is serialized before being sent. This method does not wait for the send operation
    /// to complete.</remarks>
    /// <typeparam name="T">The value type of the packet to send. Must be a struct.</typeparam>
    /// <param name="conn">The TCP connection to which the packet will be sent.</param>
    /// <param name="packet">The packet data to serialize and send to the connection.</param>
    public void Send<T>(TcpConnection conn, in T packet) where T : struct
    {
        var typeId = GetTypeId<T>();
        var writer = new NetDataWriter();
        SerializeInto(writer, in packet);
        var payload = writer.CopyData();
        _ = SendAsyncCore(conn, typeId, payload);
    }

    private async Task SendAsyncCore(TcpConnection conn, uint typeId, byte[] payload)
    {
        try
        {
            await conn.SendLock.WaitAsync(_cts.Token).ConfigureAwait(false);
            try
            {
                PacketFramer.WriteFrame(conn.Stream, typeId, payload);
                await conn.Stream.FlushAsync(_cts.Token).ConfigureAwait(false);
            }
            finally { conn.SendLock.Release(); }
        }
        catch (Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
                $"Send failed for [conn-{conn.Id}]: {ex.Message}"));
            conn.RequestDisconnect(DisconnectReason.NetworkFailure);
        }
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                _ = Task.Run(() => HandleConnection(client, ct), ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                "Accept loop crashed", ex));
        }
    }

    private async Task HandleConnection(TcpClient client, CancellationToken ct)
    {
        var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
        var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        byte[]? buffer = null;
        TcpConnection? conn = null;

        try
        {
            await sslStream.AuthenticateAsServerAsync(
                _cert,
                clientCertificateRequired: false,
                enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13,
                checkCertificateRevocation: false).ConfigureAwait(false);

            buffer = ArrayPool<byte>.Shared.Rent(PacketFramer.MaxPacketBytes);
            conn = new TcpConnection(sslStream, endpoint, buffer);

            if (_connections.Count >= MaxConnections)
            {
                Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
                    $"Rejecting connection from {endpoint}: server full"));
                SendDisconnect(conn, DisconnectReason.ServerFull);
                return;
            }

            _connections[conn.Id] = conn;
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
                $"Peer connected from {endpoint} [conn-{conn.Id}]"));

            await ReadLoop(conn, ct).ConfigureAwait(false);
        }
        catch (AuthenticationException ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
                $"TLS handshake failed for {endpoint}: {ex.Message}"));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
                $"Connection handler failed for {endpoint}", ex));
        }
        finally
        {
            if (conn is not null)
                await Teardown(conn).ConfigureAwait(false);
            else
            {
                try { sslStream.Dispose(); } catch { }
                try { client.Dispose(); } catch { }
            }
            if (buffer is not null) ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task ReadLoop(TcpConnection conn, CancellationToken ct)
    {
        var authDeadline = DateTime.UtcNow + AuthTimeout;
        var reader = new NetDataReader();

        while (!ct.IsCancellationRequested)
        {
            if (!conn.IsAuthenticated && DateTime.UtcNow > authDeadline)
            {
                conn.RequestDisconnect(DisconnectReason.Timeout);
                break;
            }

            if (conn.TryGetPendingDisconnect(out _, out _)) break;

            int length;
            try
            {
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                readCts.CancelAfter(AuthTimeout);
                length = await PacketFramer.ReadFrameAsync(conn.Stream, conn.ReadBuffer, readCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (OperationCanceledException)
            {
                conn.RequestDisconnect(DisconnectReason.Timeout);
                break;
            }
            catch (InvalidDataException ex)
            {
                Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
                    $"Invalid frame from [conn-{conn.Id}]: {ex.Message}"));
                conn.RequestDisconnect(DisconnectReason.InvalidPacket);
                break;
            }
            catch (EndOfStreamException)
            {
                conn.RequestDisconnect(DisconnectReason.NetworkFailure);
                break;
            }

            uint typeId = BinaryPrimitives.ReadUInt32BigEndian(conn.ReadBuffer.AsSpan(0, 4));
            reader.SetSource(conn.ReadBuffer, 4, length);
            var result = _dispatcher.Dispatch(conn, typeId, reader);

            if (result.IsError)
            {
                Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
                    $"Dispatch error from [conn-{conn.Id}]: {result.Outcome} for 0x{result.TypeId:X8}: " +
                    $"{result.Exception?.Message ?? "no detail"}"));
                conn.RequestDisconnect(DisconnectReason.InvalidPacket);
                break;
            }
        }
    }

    private async Task Teardown(TcpConnection conn)
    {
        _connections.TryRemove(conn.Id, out _);

        DisconnectReason reason = DisconnectReason.Unknown;
        string detail = string.Empty;
        if (conn.TryGetPendingDisconnect(out var r, out var d))
        {
            reason = r;
            detail = d;
        }

        if (reason != DisconnectReason.Unknown)
            SendDisconnect(conn, reason, detail);

        try
        {
            using var flushCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await conn.Stream.FlushAsync(flushCts.Token).ConfigureAwait(false);
        }
        catch { }

        try { conn.Stream.Dispose(); } catch { }

        var who = conn.Username ?? "<unauthenticated>";
        Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
            $"Peer disconnected [conn-{conn.Id}] {who}@{conn.RemoteEndPoint} reason={reason}"));
    }

    private static void SendDisconnect(TcpConnection conn, DisconnectReason reason, string detail = "")
    {
        try
        {
            var packet = new DisconnectPacket(reason, detail);
            var writer = new NetDataWriter();
            packet.Serialize(writer);
            PacketFramer.WriteFrame(conn.Stream, DisconnectPacket.TypeId, writer.CopyData());
        }
        catch { }
    }

    private static uint GetTypeId<T>() where T : struct
    {
        // Each packet exposes a public const uint TypeId. Reflected here once;
        // dispatch-side does not need this because TypeId comes from the wire.
        return PacketTypeCache<T>.Value;
    }

    private static void SerializeInto<T>(NetDataWriter writer, in T packet) where T : struct
    {
        ((dynamic)packet).Serialize(writer);
    }

    private static class PacketTypeCache<T> where T : struct
    {
        public static readonly uint Value;
        static PacketTypeCache()
        {
            var field = typeof(T).GetField("TypeId",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static) ?? 
                throw new InvalidOperationException($"Packet type {typeof(T).Name} missing public const uint TypeId.");
            Value = (uint)field.GetValue(null)!;
        }
    }
}

/*
 *------------------------------------------------------------
 * (TcpHost.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */