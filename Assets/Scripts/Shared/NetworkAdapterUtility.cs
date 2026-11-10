using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RTSokak.Shared
{
    public readonly struct NetworkAdapterChoice
    {
        public readonly string DisplayName;
        public readonly string IPv4;

        public NetworkAdapterChoice(string displayName, string ipv4)
        {
            DisplayName = displayName;
            IPv4 = ipv4;
        }
    }

    public static class NetworkAdapterUtility
    {
        public static List<NetworkAdapterChoice> GetIPv4Choices()
        {
            var result = new List<NetworkAdapterChoice>();

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni == null || ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                IPInterfaceProperties properties;
                try { properties = ni.GetIPProperties(); }
                catch { continue; }
                if (properties == null)
                    continue;

                foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                {
                    IPAddress address = unicast?.Address;
                    if (address == null || address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    if (IPAddress.IsLoopback(address))
                        continue;

                    string ip = address.ToString();
                    if (string.IsNullOrWhiteSpace(ip))
                        continue;

                    result.Add(new NetworkAdapterChoice($"{ni.Name} {ip}", ip));
                }
            }

            return result;
        }
    }
}
