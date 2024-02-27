using System.Net;
using System.Net.Sockets;

namespace SocketChat.Common;

public static class DnsTools
{
    public static async Task<IPAddress> GetLocalIpAddressAsync()
    {
        var ipHostInfo = await Dns.GetHostEntryAsync(Dns.GetHostName(), AddressFamily.InterNetwork);
        var ipAddress = ipHostInfo.AddressList[0];
        return ipAddress;
    }
}
