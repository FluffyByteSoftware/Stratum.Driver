/*
 * (TcpHost.cs)
 *------------------------------------------------------------
 * Created - 5/17/2026 2:17:21 PM
 * Created by - Seliris
 *-------------------------------------------------------------
 */

using LiteNetLib.Utils;
using Stratum.Networking.Dispatch;
using Stratum.Shared.Networking;
using Stratum.SystemTools.Logger;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Stratum.Networking.Tcp;

/// <summary>
/// Hosts a TCP server that manages client connections and dispatches packet-based messages.
/// </summary>
/// <remarks>Supports optional TLS encryption with server certificate authentication. Uses a frame-based protocol
/// with configurable maximum frame size. Manages connection lifecycle including idle timeout detection and graceful
/// shutdown. All connection operations are thread-safe.</remarks>
public sealed class TcpHost
{
    public const int DefaultMaxFrameBytes = 1024 * 1024; // 1 MiB

    private readonly IPEndPoint _bindEndpoint;
    private readonly PacketDispatcher<TcpConnection> _dispatcher;
    private readonly X509Certificate2? _serverCertificate;
    private readonly int _maxFrameBytes;
    private readonly TimeSpan _idleTimeout;
    private readonly ConcurrentDictionary<long, TcpConnection> _connections = [];
    private readonly CancellationTokenSource _cts = new();

    private Socket? _listener;
    private Task? _acceptTask;
    private long _nextConnectionId;
    private volatile bool _started;

    /// <summary>
    /// Gets the number of connections.
    /// </summary>
    public int ConnectionCount => _connections.Count;
    /// <summary>
    /// The local endpoint to which the connection is bound.
    /// </summary>
    public IPEndPoint BindEndpoint => _bindEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpHost"/> class.
    /// </summary>
    /// <param name="bindEndpoint">The endpoint to bind the TCP host to.</param>
    /// <param name="dispatcher">The packet dispatcher for handling connections. Must be frozen.</param>
    /// <param name="serverCertificate">The server certificate for SSL/TLS connections, or <see langword="null"/> for unencrypted connections.</param>
    /// <param name="maxFrameBytes">The maximum frame size in bytes. Must be at least <see cref="PacketFramer.HeaderBytes"/>.</param>
    /// <param name="idleTimeout">The idle timeout duration, or <see langword="null"/> to use the default of 30 seconds.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="dispatcher"/> is not frozen.</exception>
    public TcpHost(
        IPEndPoint bindEndpoint,
        PacketDispatcher<TcpConnection> dispatcher,
        X509Certificate2? serverCertificate = null,
        int maxFrameBytes = DefaultMaxFrameBytes,
        TimeSpan? idleTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(bindEndpoint);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (!dispatcher.IsFrozen)
            throw new ArgumentException(
                $"Dispatcher must be frozen before host construction.",
                nameof(dispatcher));

        ArgumentOutOfRangeException.ThrowIfLessThan(maxFrameBytes, PacketFramer.HeaderBytes);

        _bindEndpoint = bindEndpoint;
        _dispatcher = dispatcher;
        _serverCertificate = serverCertificate;
        _maxFrameBytes = maxFrameBytes;
        _idleTimeout = idleTimeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Starts the TCP host and begins listening for incoming connections.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the host has already been started.</exception>
    public void Start()
    {
        if (_started)
            throw new InvalidOperationException(
                "TcpHost has already been started.");
        
        _started = true;

        _listener = new Socket(
            _bindEndpoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        _listener.Bind(_bindEndpoint);
        _listener.Listen(backlog: 64);

        var tlsState = _serverCertificate is not null ? "on" : "off";

        Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
            $"TcpHost listening on {_bindEndpoint} (TLS: {tlsState})."));

        _acceptTask = Task.Run(AcceptLoop);
    }

    /// <summary>
    /// Stops the server asynchronously, cancelling pending operations and closing all active connections.
    /// </summary>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async Task StopAsync()
    {
        if (!_started) return;

        _cts.Cancel();

        try
        {
            _listener?.Close();
        }
        catch(OperationCanceledException)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Debug, 
                "Listener socket closed during shutdown."));
        }
        catch(Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Error, 
                $"StopAsync failed.", ex));
        }

        foreach(var conn in _connections.Values)
        {
            try
            {
                conn.Stream.Close();
            }
            catch(Exception ex)
            {
                Scribe.Pump(new ScribeMessage(ScribeSeverity.Error, 
                    $"Failed to close connection stream.", ex));
            }

            try
            {
                conn.Socket.Close();
            }
            catch(Exception ex)
            {
                Scribe.Pump(new ScribeMessage(ScribeSeverity.Error, 
                    $"Failed to close connection socket.", ex));
            }

            _connections.Clear();

            if (_acceptTask is not null)
            {
                try
                {
                    await _acceptTask.ConfigureAwait(false);

                }
                catch (OperationCanceledException)
                {
                    Scribe.Pump(new ScribeMessage(ScribeSeverity.Debug,
                        "Accept loop cancelled during shutdown."));
                }
                catch (Exception ex)
                {
                    Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                        $"Accept loop failed during shutdown.", ex));
                }
            }

            _cts.Dispose();
        }
    }

    /// <summary>
    /// Asynchronously sends a packet to the specified TCP connection.
    /// </summary>
    /// <remarks>Returns <see langword="false"/> if the connection is marked for disconnection or if an error
    /// occurs during transmission.</remarks>
    /// <typeparam name="TPacket">The packet type implementing IPacketWritable.</typeparam>
    /// <param name="connection">The target TCP connection.</param>
    /// <param name="packet">The packet to send.</param>
    /// <returns><see langword="true"/> if the packet was sent successfully; otherwise, <see langword="false"/>.</returns>
    public async ValueTask<bool> SendAsync<TPacket>(
        TcpConnection connection,
        TPacket packet) where TPacket : struct, IPacketWritable
    {
        if (connection.IsDisconnectRequested) return false;

        var writer = new NetDataWriter();
        
        packet.Serialize(writer);

        var payload = writer.Data.AsMemory(0, writer.Length);
        var typeId = packet.TypeId;

        var frameLength = PacketFramer.HeaderBytes + payload.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(frameLength);

        try
        {
            PacketFramer.WriteHeader(
                buffer.AsSpan(0, PacketFramer.HeaderBytes),
                payload.Length,
                typeId);
            payload.Span.CopyTo(buffer.AsSpan(PacketFramer.HeaderBytes));

            await connection.SendLock.WaitAsync(_cts.Token).ConfigureAwait(false);

            try
            {
                await connection.Stream
                    .WriteAsync(buffer.AsMemory(0, frameLength), _cts.Token)
                    .ConfigureAwait(false);
                await connection.Stream.FlushAsync(_cts.Token)
                    .ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                    $"Failed to send packet of type {typeId} to connection " +
                    $"{connection.Id}.", ex));
                return false;

            }
            finally
            {
                connection.SendLock.Release();
            }
        }
        catch(OperationCanceledException)
        {
            return false;
        }
        catch(Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                $"Failed to send packet of type {typeId} to connection " +
                $"{connection.Id:X8}.", ex));
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task AcceptLoop()
    {
        var token = _cts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                Socket socket;
                try
                {
                    socket = await _listener!
                        .AcceptAsync(token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch(ObjectDisposedException)
                {
                    break;
                }
                catch(Exception ex)
                {
                    Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                        $"Failed to accept incoming connection.", ex));

                    continue;
                }

                _ = HandleAcceptedAsync(socket);
            }
        }
        catch(Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                $"Accept loop failed.", ex));
        }
    }

    private async Task HandleAcceptedAsync(Socket socket)
    {
        var token = _cts.Token;
        var remote = (IPEndPoint)socket.RemoteEndPoint!;
        Stream stream = new NetworkStream(socket, ownsSocket: false);

        try
        {
            if(_serverCertificate is not null)
            {
                var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                using var handshakeCts = CancellationTokenSource
                                .CreateLinkedTokenSource(token);
                handshakeCts.CancelAfter(TimeSpan.FromSeconds(10));

                await ssl.AuthenticateAsServerAsync(
                    new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _serverCertificate,
                        ClientCertificateRequired = false,
                        EnabledSslProtocols =
                        System.Security.Authentication.SslProtocols.Tls12
                        | System.Security.Authentication.SslProtocols.Tls13,
                    },
                    handshakeCts.Token).ConfigureAwait(false);

                stream = ssl;
            }
        }
        catch(Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                $"Failed to complete TLS handshake with {remote}.", ex));
            try
            {
                stream.Dispose();
            }
            catch(Exception exe)
            {
                Scribe.Pump(
                    new ScribeMessage(ScribeSeverity.Error,
                    $"Failed to dispose stream after TLS handshake failure with {remote}.", exe));
            }

            try
            {
                socket.Close();
            }
            catch(Exception exe)
            {
                Scribe.Pump(
                    new ScribeMessage(ScribeSeverity.Error,
                    $"Failed to close socket after TLS handshake failure with {remote}.", exe));
            }

            return;
        }

        var id = Interlocked.Increment(ref _nextConnectionId);
        var connection = new TcpConnection(id, socket, stream, remote);
        _connections[id] = connection;

        Scribe.Pump(new ScribeMessage(ScribeSeverity.Info,
            $"TCP Connection {id} accepted from {remote}."));

        try
        {
            await ReadLoop(connection).ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
                $"Read loop terminated connection {id}.",
                ex));
        }
        finally
        {
            _connections.TryRemove(id, out _);
            try
            {
                connection.Stream.Close();
            }
            catch(Exception ex)
            {
                Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                    $"Failed to close stream for connection {id}.", ex));
            }

            try
            {
                connection.Socket.Close();
            }
            catch(Exception ex)
            {
                Scribe.Pump(new ScribeMessage(ScribeSeverity.Error,
                    $"Failed to close socket for connection {id}.", ex));
            }
        }
    }

    private async Task ReadLoop(TcpConnection connection)
    {
        var token = _cts.Token;
        var headerBuf = new byte[PacketFramer.HeaderBytes];

        while (!token.IsCancellationRequested)
        {
            if (connection.IsDisconnectRequested) return;

            using var readCts = CancellationTokenSource
                .CreateLinkedTokenSource(token);

            readCts.CancelAfter(_idleTimeout);

            try
            {
                await ReadExactAsync(
                    connection.Stream,
                    headerBuf,
                    readCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                connection.RequestDisconnect(SecureDisconnectReason.IdleTimeout);
                return;
            }
            catch (EndOfStreamException)
            {
                connection.RequestDisconnect(SecureDisconnectReason.PeerClosed);
                return;
            }

            var frameBodyLength =
                BinaryPrimitives.ReadInt32BigEndian(headerBuf);
            var typeId =
                BinaryPrimitives.ReadUInt32BigEndian(headerBuf.AsSpan(4));


            if(frameBodyLength < 4 || frameBodyLength > _maxFrameBytes)
            {
                Scribe.Pump(new ScribeMessage(ScribeSeverity.Warn,
                    $"Connection {connection.Id} sent invalid frame  +" +
                    $"length {frameBodyLength} from {connection.RemoteEndpoint}."));

                connection.RequestDisconnect(SecureDisconnectReason.InvalidFrame);

                return;
            }

            var payloadLength = frameBodyLength - 4;
            var payloadBuf = ArrayPool<byte>.Shared.Rent(payloadLength);

            try
            {
                if(payloadLength > 30)
                {
                    await ReadExactAsync(
                        connection.Stream,
                        payloadBuf.AsMemory(0, payloadLength),
                        readCts.Token).ConfigureAwait(false);

                    connection.LastActivityUtc = DateTime.UtcNow;

                    var reader = new NetDataReader();
                    reader.SetSource(payloadBuf, 0, payloadLength);

                    var result = _dispatcher.Dispatch(typeId, connection, reader);

                    if (!result.IsOk)
                    {
                        Scribe.Pump(new ScribeMessage(
                            ScribeSeverity.Warn,
                            $"Dispatch on connection {connection.Id} return " +
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
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(payloadBuf);
            }
        }
    }

    private static async Task ReadExactAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken ct)
    {
        int total = 0;

        while (total < buffer.Length)
        {
            int read = await stream
                .ReadAsync(buffer[total..], ct)
                .ConfigureAwait(false);

            if (read == 0) throw new EndOfStreamException();
            total += read;
        }
    }

    private static Task ReadExactAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken ct)
        => ReadExactAsync(stream, buffer.AsMemory(), ct);
}



/*
 *------------------------------------------------------------
 * (TcpHost.cs)
 * See License.txt for licensing information.
 *-----------------------------------------------------------
 */