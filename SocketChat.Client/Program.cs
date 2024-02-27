using System.Net;

Console.WriteLine("Enter IP address for the server:");
string ipaddress = Console.ReadLine();

var client = new SocketClient(IPAddress.Parse(ipaddress), 11000); // Adjust port as necessary
await client.ConnectAsync();

Console.WriteLine("Enter commands (subscribe <RoomId>, unsubscribe <RoomId>, send <Message>, quit):");
Console.ForegroundColor = ConsoleColor.DarkYellow;
Console.BackgroundColor = ConsoleColor.Magenta;

while (true)
{
    var input = Console.ReadLine();

    if (input == null)
        continue;

    if (input.StartsWith("subscribe "))
    {
        var roomId = input.Substring("subscribe ".Length);
        await client.SubscribeToRoom(roomId);
    }
    else if (input.StartsWith("unsubscribe "))
    {
        var roomId = input.Substring("unsubscribe ".Length);
        await client.UnsubscribeFromRoom(roomId);
    }
    else if (input.StartsWith("send "))
    {
        var message = input.Substring("send ".Length);
        await client.SendMessageToRoom(message);
    }
    else if (input == "quit")
    {
        client.Close();
        break;
    }
}