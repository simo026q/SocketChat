using SocketChat.Common;
using System.Net.Sockets;
using System.Text;

namespace SocketChat.Server;

public class SocketConnection(Socket socket) 
    : IDisposable
{
    public string Id { get; } = socket.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();

    private readonly Socket _socket = socket;
    private bool _disposed = false;

    public async Task<bool> SendAsync(string message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await SendWithoutAcknowledgmentAsync(message, cancellationToken);
        var response = await ReceiveWithoutAcknowledgmentAsync(cancellationToken);
        return response == SocketConstants.Acknowledgment;
    }

    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string? response = await ReceiveWithoutAcknowledgmentAsync(cancellationToken);

        if (response == null)
            return null;

        await SendRawWithoutAcknowledgmentAsync(SocketConstants.Acknowledgment, cancellationToken);
        return response;
    }

    private async Task SendWithoutAcknowledgmentAsync(string message, CancellationToken cancellationToken = default)
    {
        await SendRawWithoutAcknowledgmentAsync(message + SocketConstants.EndOfMessage, cancellationToken);
    }

    private async Task SendRawWithoutAcknowledgmentAsync(string message, CancellationToken cancellationToken = default)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(messageBytes, SocketFlags.None, cancellationToken);
    }

    private async Task<string?> ReceiveWithoutAcknowledgmentAsync(CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            var buffer = new byte[SocketConstants.BufferSize];
            var received = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
            if (received == 0)
                break;

            string message = Encoding.UTF8.GetString(buffer, 0, received);

            if (message.IndexOf(SocketConstants.EndOfMessage) > -1)
            {
                message = message.Replace(SocketConstants.EndOfMessage, "");
                sb.Append(message);
                var value = sb.ToString();
                sb.Clear();
                return value;
            }
            else
            {
                sb.Append(message);
            }
        }

        return null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
            _socket.Dispose();
        }

        _disposed = true;
    }
}
