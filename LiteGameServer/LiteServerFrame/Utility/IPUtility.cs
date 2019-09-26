using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LiteServerFrame.Utility
{
    public class IPUtility
    {
        public static bool IPv6First = true;
        public static IPEndPoint IPEPIPv4Any = new IPEndPoint(IPAddress.Any, 0);
        public static IPEndPoint IPEPIPv6Any = new IPEndPoint(IPAddress.IPv6Any, 0);
        
        public static IPEndPoint GetIPEndPointAny(AddressFamily family, int port)
        {
            if (family == AddressFamily.InterNetwork)
            {
                if (port == 0)
                {
                    return IPEPIPv4Any;
                }

                return new IPEndPoint(IPAddress.Any, port);
            }
            else if (family == AddressFamily.InterNetworkV6)
            {
                if (port == 0)
                {
                    return IPEPIPv6Any;
                }

                return new IPEndPoint(IPAddress.IPv6Any, port);
            }
            return null;
        }

        public static IPEndPoint GetHostEndPoint(string host, int port)
        {
            IPAddress address = null;
            if (IPAddress.TryParse(host, out address))
            {
                return new IPEndPoint(address, port);
            }
            IPAddress[] ips = Dns.GetHostAddresses(host);
            List<IPAddress> listIPv4 = new List<IPAddress>();
            List<IPAddress> listIPv6 = new List<IPAddress>();
            foreach (IPAddress ip in ips)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    listIPv4.Add(ip);
                }
                else
                {
                    listIPv6.Add(ip);
                }
            }
        
            if (IPv6First)
            {
                if (listIPv6.Count > 0)
                {
                    return new IPEndPoint(listIPv6[0], port);
                }

                if (listIPv4.Count > 0)
                {
                    return new IPEndPoint(listIPv4[0], port);
                }
            }
            else
            {
                if (listIPv4.Count > 0)
                {
                    return new IPEndPoint(listIPv4[0], port);
                }

                if (listIPv6.Count > 0)
                {
                    return new IPEndPoint(listIPv6[0], port);
                }
            }
            return null;
        }
    }
}