namespace SocketChat.Server;

internal interface IMessageHandler
{
    Task HandleMessageAsync(SocketConnection socketConnection, string? message);
}
