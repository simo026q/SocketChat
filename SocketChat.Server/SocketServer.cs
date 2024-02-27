using SocketChat.Common;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace SocketChat.Server;

internal class SocketServer 
    : BackgroundService
{
    private readonly Socket _socket;

    private readonly ConcurrentDictionary<string, List<string>> _subscribedConnections = new();
    private readonly ConcurrentDictionary<string, SocketConnection> _connections = new();

    public SocketServer()
    {
        _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var ipAddress = await DnsTools.GetLocalIpAddressAsync();
        var endpoint = new IPEndPoint(ipAddress, 11000);
        _socket.Bind(endpoint);
        _socket.Listen(100);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Socket handler = await _socket.AcceptAsync(stoppingToken);
            SocketConnection connection = new(handler);
            _connections.TryAdd(connection.Id, connection);
            Task.Run(() => HandleConnection(connection, stoppingToken), stoppingToken);
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
                _subscribedConnections.AddOrUpdate(connection.Id, [roomId], (connectionId, roomIds) =>
                {
                    roomIds.Add(roomId);
                    return roomIds;
                });
            }
            else if (message.StartsWith(SocketConstants.Unsubscribe))
            {
                var roomId = message.Replace(SocketConstants.Unsubscribe, "").Trim();
                if (_subscribedConnections.TryGetValue(connection.Id, out List<string>? roomIds))
                {
                    roomIds.Remove(roomId);
                }
            }
            else if (message.StartsWith(SocketConstants.Message))
            {
                string messageBodyJson = message.Replace(SocketConstants.Message, "").Trim();

                Message? messageBody;
                try
                {
                    messageBody = JsonSerializer.Deserialize<Message>(messageBodyJson);
                }
                catch (JsonException)
                {
                    messageBody = null;
                }

                if (messageBody == null)
                    continue;

                foreach (var (connectionId, connection2) in _connections)
                {
                    if (_subscribedConnections.TryGetValue(connectionId, out List<string>? subscribedRoomIds)
                        && subscribedRoomIds.Contains(messageBody.RoomId))
                    {
                        await connection2.SendAsync(message, cancellationToken);
                    }
                }
            }
        }
    }
}
