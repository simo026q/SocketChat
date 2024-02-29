using SocketChat.Common;
using System.Net;
using System.Net.Sockets;
using System.Text;

List<Socket> connections = new();

var ipAddress = await DnsTools.GetLocalIpAddressAsync();
var endpoint = new IPEndPoint(ipAddress, 11000);

while (true)
{
    try
    {
        Socket socket = await ConnectNewSocket(endpoint);
        connections.Add(socket);
        Console.WriteLine($"Connected! #{connections.Count}");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Failed to connect: {ex}");
        Console.ResetColor();
        break;
    }

    foreach (var socket in connections)
    {
        try
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes("<|SUB|>1<|EOM|>"));
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to send (connection #{connections.IndexOf(socket) + 1}): {ex}");
            Console.ResetColor();
            break;
        }
    }

}

Console.WriteLine($"Opened {connections.Count} connections.");

foreach (var socket in connections)
{
    socket.Close();
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();

static async Task<Socket> ConnectNewSocket(EndPoint endPoint)
{
    var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
    await socket.ConnectAsync(endPoint);
    return socket;
}