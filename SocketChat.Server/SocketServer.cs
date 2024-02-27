using SocketChat.Common;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace SocketChat.Server;

internal class SocketServer
{
    private CancellationTokenSource? _cts;

    private readonly Socket _socket;

    private readonly ConcurrentDictionary<string, List<string>> _subscripedConnections = new();
    private readonly ConcurrentDictionary<string, SocketConnection> _connections = new();

    public SocketServer(IPEndPoint endPoint)
    {
        _socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _socket.Bind(endPoint);
    }

    public SocketServer(IPAddress address, int port)
        : this(new IPEndPoint(address, port))
    {
    }

    public static async Task<SocketServer> CreateAsync(int port)
    {
        var ipAddress = await DnsTools.GetLocalIpAddressAsync();
        return new(ipAddress, port);
    }

    public void Start()
    {
        _socket.Listen(100);

        _cts = new CancellationTokenSource();
        var cancellationToken = _cts.Token;
        Task.Run(() => Run(cancellationToken), cancellationToken);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _socket.Close();
    }

    private async Task Run(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket handler = await _socket.AcceptAsync(cancellationToken);
            SocketConnection connection = new(handler);
            _connections.TryAdd(connection.Id, connection);
            Task.Run(() => HandleConnection(connection, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleConnection(SocketConnection connection, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? message = await connection.ReceiveAsync(cancellationToken);
            if (message == null)
                break;

            if (message.StartsWith(SocketConstants.Subscribe))
            {
                var roomId = message.Replace(SocketConstants.Subscribe, "").Trim();
                _subscripedConnections.AddOrUpdate(connection.Id, [roomId], (connectionId, roomIds) =>
                {
                    roomIds.Add(roomId);
                    return roomIds;
                });
            }
            else if (message.StartsWith(SocketConstants.Unsubscribe))
            {
                var roomId = message.Replace(SocketConstants.Unsubscribe, "").Trim();
                if (_subscripedConnections.TryGetValue(connection.Id, out List<string>? roomIds))
                {
                    roomIds.Remove(roomId);
                }
            }
            else if (message.StartsWith(SocketConstants.Message))
            {
                // TODO: Send message to all subscribers
            }
        }
    }
}
