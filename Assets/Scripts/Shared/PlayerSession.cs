using UnityEngine;

namespace RTSokak.Shared
{
    /// <summary>Локальные сетевые настройки (без ника / аккаунта).</summary>
    public static class PlayerSession
    {
        const string PortKey = "rtsokak.server.port";
        const string LanInterfaceIpKey = "rtsokak.lan.interface.ip";
        const string UseTcpTransportKey = "rtsokak.transport.tcp";
        const string ServerNameKey = "rtsokak.server.name";

        public const ushort DefaultPort = 7777;

        public static ushort Port { get; private set; } = DefaultPort;
        public static string LanInterfaceIPv4 { get; private set; } = string.Empty;
        public static bool UseTcpTransport { get; private set; }
        public static string ServerDisplayName { get; private set; } = "RTS Server";

        public static void Load()
        {
            Port = (ushort)PlayerPrefs.GetInt(PortKey, DefaultPort);
            LanInterfaceIPv4 = PlayerPrefs.GetString(LanInterfaceIpKey, string.Empty);
            UseTcpTransport = PlayerPrefs.GetInt(UseTcpTransportKey, 0) != 0;
            ServerDisplayName = PlayerPrefs.GetString(ServerNameKey, "RTS Server");
            if (string.IsNullOrWhiteSpace(ServerDisplayName))
                ServerDisplayName = "RTS Server";
        }

        public static void SetPort(ushort port)
        {
            Port = port == 0 ? DefaultPort : port;
            PlayerPrefs.SetInt(PortKey, Port);
            PlayerPrefs.Save();
        }

        public static bool TrySetPort(string portText, out ushort port)
        {
            if (!ushort.TryParse(portText, out port) || port == 0)
            {
                port = DefaultPort;
                return false;
            }

            SetPort(port);
            return true;
        }

        public static void SetLanInterfaceIPv4(string ipv4)
        {
            LanInterfaceIPv4 = string.IsNullOrWhiteSpace(ipv4) ? string.Empty : ipv4.Trim();
            PlayerPrefs.SetString(LanInterfaceIpKey, LanInterfaceIPv4);
            PlayerPrefs.Save();
        }

        public static void SetUseTcpTransport(bool useTcp)
        {
            UseTcpTransport = useTcp;
            PlayerPrefs.SetInt(UseTcpTransportKey, useTcp ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetServerDisplayName(string name)
        {
            ServerDisplayName = string.IsNullOrWhiteSpace(name) ? "RTS Server" : name.Trim();
            if (ServerDisplayName.Length > 40)
                ServerDisplayName = ServerDisplayName.Substring(0, 40);
            PlayerPrefs.SetString(ServerNameKey, ServerDisplayName);
            PlayerPrefs.Save();
        }
    }
}
