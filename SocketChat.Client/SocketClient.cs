using SocketChat.Common;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;

class SocketClient
{
    private readonly Socket _socket;
    private readonly IPEndPoint _endPoint;
    private string _currentRoomId;

    public SocketClient(IPAddress ip, int port)
    {
        _endPoint = new IPEndPoint(ip, port);
        _socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
    }

    public async Task ConnectAsync()
    {
        await _socket.ConnectAsync(_endPoint);
        Console.WriteLine("Connected to the server.");
        Task.Run(ReceiveMessagesAsync);
    }

    private async Task ReceiveMessagesAsync()
    {
        try
        {
            while (_socket.Connected)
            {
                var buffer = new byte[1024];
                var length = await _socket.ReceiveAsync(buffer, SocketFlags.None);
                if (length > 0)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, length);

                    if (message != SocketConstants.Acknowledgment)
                    {
                        Console.WriteLine($"Message from server: {message}");
                        await SendRawAsync(SocketConstants.Acknowledgment);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public async Task SendAsync(string message)
    {
        await SendRawAsync($"{message}{SocketConstants.EndOfMessage}");
    }

    private async Task SendRawAsync(string message)
    {
        var encoded = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(encoded, SocketFlags.None);
        Console.WriteLine($"Message sent to the server: '{message}'");
    }

    public void Close()
    {
        _socket.Shutdown(SocketShutdown.Both);
        _socket.Close();
        Console.WriteLine("Disconnected from server.");
    }

    public Task SubscribeToRoom(string roomId)
    {
        _currentRoomId = roomId;
        var message = $"{SocketConstants.Subscribe}{roomId}";
        return SendAsync(message);
    }

    public Task UnsubscribeFromRoom(string roomId)
    {
        var message = $"{SocketConstants.Unsubscribe}{roomId}";
        return SendAsync(message);
    }

    public Task SendMessageToRoom(string messageText)
    {
        if (string.IsNullOrEmpty(_currentRoomId))
        {
            Console.WriteLine("Error: No room subscribed. Please subscribe to a room first.");
            return Task.CompletedTask;
        }

        var message = new Message
        {
            RoomId = _currentRoomId,
            Name = "Client", // This could be dynamic or fetched from user input
            Messsage = messageText,
            MessageId = new Random().Next(),
            CreatedAt = DateTime.UtcNow
        };

        var messageJson = JsonSerializer.Serialize(message);
        return SendAsync($"{SocketConstants.Message}{messageJson}");
    }
}
