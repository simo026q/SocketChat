using SocketChat.Common;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace SocketChat.Server;

internal class SocketConnection : IDisposable
{
    private readonly Socket _socket;
    private readonly IMessageHandler _messageHandler;
    private readonly CancellationToken _cancellationToken;

    private bool _disposed = false;

    public string Id { get; }
    public bool IsConnected => _socket.Connected;

    public SocketConnection(Socket socket, IMessageHandler messageHandler, CancellationToken cancellationToken)
    {
        Id = socket.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
        _messageHandler = messageHandler;
        _socket = socket;
        _cancellationToken = cancellationToken;
    }

    private CancellationTokenSource? _receiveLoopCancellationTokenSource;

    public void StartReceiveLoop()
    {
        _receiveLoopCancellationTokenSource?.Cancel();
        _receiveLoopCancellationTokenSource = new CancellationTokenSource();
        Task.Run(() => WaitForReceiveAsync(_receiveLoopCancellationTokenSource.Token));
    }

    private async Task WaitForReceiveAsync(CancellationToken loopCancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(loopCancellationToken, _cancellationToken);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                string? message = await ReceiveAsync(stopLoop: false, cts.Token);
                await _messageHandler.HandleMessageAsync(this, message);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
    }

    public async Task<bool> SendAsync(string message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await SendWithoutAcknowledgmentAsync(message, cancellationToken);
            var response = await ReceiveWithoutAcknowledgmentAsync(stopLoop: true, cancellationToken);
            return response == SocketConstants.Acknowledgment;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await ReceiveAsync(stopLoop: true, cancellationToken);
    }

    private async Task<string?> ReceiveAsync(bool stopLoop, CancellationToken cancellationToken = default)
    {
        string? response = await ReceiveWithoutAcknowledgmentAsync(stopLoop, cancellationToken);

        if (response == null)
            return null;

        try
        {
            await SendRawWithoutAcknowledgmentAsync(SocketConstants.Acknowledgment, cancellationToken);
            return response;
        }
        catch(SocketException)
        {
            return null;
        }
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

    private async Task<string?> ReceiveWithoutAcknowledgmentAsync(bool stopLoop, CancellationToken cancellationToken = default)
    {
        if (stopLoop)
        {
            _receiveLoopCancellationTokenSource?.Cancel();
            _receiveLoopCancellationTokenSource = null;
        }

        string? response = null;

        var sb = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            var buffer = new byte[SocketConstants.BufferSize];

            string message;
            try
            {
                var received = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                if (received == 0)
                    break;

                message = Encoding.UTF8.GetString(buffer, 0, received);
            }
            catch (SocketException)
            {
                break;
            }

            if (message.IndexOf(SocketConstants.EndOfMessage) > -1)
            {
                message = message.Replace(SocketConstants.EndOfMessage, "");
                sb.Append(message);
                response = sb.ToString();
                sb.Clear();
                break;
            }
            else if (message == SocketConstants.Acknowledgment)
            {
                response = SocketConstants.Acknowledgment;
                break;
            }
            else
            {
                sb.Append(message);
            }
        }

        if (response != null && stopLoop)
        {
            StartReceiveLoop();
        }

        return response;
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

            _receiveLoopCancellationTokenSource?.Cancel();
            _receiveLoopCancellationTokenSource = null;
        }

        _disposed = true;
    }
}
