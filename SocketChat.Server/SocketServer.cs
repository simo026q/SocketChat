using SocketChat.Common;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace SocketChat.Server;

internal class SocketServer 
    : IHostedService
{
    private CancellationTokenSource? _cts;

    private readonly Socket _socket;

    private readonly ConcurrentDictionary<string, List<string>> _subscribedConnections = new();
    private readonly ConcurrentDictionary<string, SocketConnection> _connections = new();

    public SocketServer()
    {
        _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var ipAddress = await DnsTools.GetLocalIpAddressAsync();
        var endpoint = new IPEndPoint(ipAddress, 11000);
        _socket.Bind(endpoint);

        _socket.Listen(100);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task.Run(() => Run(cancellationToken), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        _socket.Close();
        return Task.CompletedTask;
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

                if (_subscribedConnections.TryGetValue(connection.Id, out List<string>? roomIds))
                {
                    foreach (var roomId in roomIds)
                    {
                        foreach (var (connectionId, connection2) in _connections)
                        {
                            if (_subscribedConnections.TryGetValue(connectionId, out List<string>? subscribedRoomIds) 
                                && subscribedRoomIds.Contains(roomId))
                            {
                                await connection2.SendAsync(message, cancellationToken);
                            }
                        }
                    }
                }
            }
        }
    }
}
