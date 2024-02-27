using SocketChat.Common;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace SocketChat.Server;

internal class SocketServer 
    : BackgroundService, IMessageHandler
{
    private readonly ILogger<SocketServer> _logger;
    private readonly Socket _socket;

    private readonly ConcurrentDictionary<string, List<string>> _subscribedConnections = new();
    private readonly ConcurrentDictionary<string, SocketConnection> _connections = new();

    public SocketServer(ILogger<SocketServer> logger)
    {
        _logger = logger;
        _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var ipAddress = await DnsTools.GetLocalIpAddressAsync();
        var endpoint = new IPEndPoint(ipAddress, 11000);

        _logger.LogInformation("Starting server on {endpoint}", endpoint.ToString());
        _socket.Bind(endpoint);
        _socket.Listen(100);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Ready for client connection...");
            Socket handler = await _socket.AcceptAsync(stoppingToken);
            _logger.LogInformation("Client connected from {endpoint}", handler.RemoteEndPoint?.ToString());

            SocketConnection connection = new(handler, this, stoppingToken);
            _connections.TryAdd(connection.Id, connection);
            connection.StartReceiveLoop();

            //ThreadPool.QueueUserWorkItem(async _ => await HandleConnection(connection, stoppingToken));
        }
    }

    public async Task HandleMessageAsync(SocketConnection connection, string? message)
    {
        if (message == null)
        {
            connection.Dispose();
            _connections.TryRemove(connection.Id, out _);
            _subscribedConnections.TryRemove(connection.Id, out _);
            _logger.LogInformation("Client {endpoint} disconnected", connection.Id);
            return;
        }

        _logger.LogInformation("Received message from {endpoint}: {message}", connection.Id, message);

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
                if (messageBody == null)
                    return;
            }
            catch (JsonException)
            {
                return;
            }

            foreach (var (connectionId, connection2) in _connections)
            {
                if (connection != connection2
                    && _subscribedConnections.TryGetValue(connectionId, out List<string>? subscribedRoomIds)
                    && subscribedRoomIds.Contains(messageBody.RoomId))
                {
                    bool succeeded = await connection2.SendAsync(message);
                    if (!succeeded)
                    {
                        _logger.LogWarning("Failed to send message to {endpoint}", connection2.Id);
                    }
                    else
                    {
                        _logger.LogInformation("Sent message to {endpoint}", connection2.Id);
                    }
                }
            }
        }
    }
}
