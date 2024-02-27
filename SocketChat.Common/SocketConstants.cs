namespace SocketChat.Common;

public static class SocketConstants
{
    public const string EndOfMessage = "<|EOM|>";
    public const string Acknowledgment = "<|ACK|>";
    public const string Subscribe = "<|SUB|>";
    public const string Unsubscribe = "<|UNSUB|>";
    public const string Message = "<|MSG|>";
    public const int BufferSize = 1_024;
}
