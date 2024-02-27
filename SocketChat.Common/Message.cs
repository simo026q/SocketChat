namespace SocketChat.Common;

public class Message
{
    public string RoomId { get; set; }
    public string Name { get; set; }
    public string Messsage { get; set; }
    public int MessageId { get; set; }
    public DateTime CreatedAt { get; set; }
}
