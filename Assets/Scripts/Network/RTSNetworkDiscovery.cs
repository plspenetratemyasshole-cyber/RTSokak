using System;
using System.Net;
using System.Net.Sockets;
using Mirror;
using Mirror.Discovery;
using RTSokak.Shared;
using UnityEngine;

namespace RTSokak.Network
{
    [DisallowMultipleComponent]
    public class RTSNetworkDiscovery : NetworkDiscoveryBase<ServerRequest, RTSServerResponse>
    {
        string advertiseInterfaceIPv4 = string.Empty;

        public void AdvertiseServerOnInterface(string interfaceIPv4)
        {
            advertiseInterfaceIPv4 = string.IsNullOrWhiteSpace(interfaceIPv4)
                ? string.Empty
                : interfaceIPv4.Trim();

            if (!SupportedOnThisPlatform)
                throw new PlatformNotSupportedException("Network discovery not supported on this platform");

            StopDiscovery();

            serverUdpClient = new UdpClient(serverBroadcastListenPort)
            {
                EnableBroadcast = true,
                MulticastLoopback = false
            };

            _ = ServerListenAsync();
        }

        protected override RTSServerResponse ProcessRequest(ServerRequest request, IPEndPoint endpoint)
        {
            Uri uri = BuildAdvertisedUri();
            return new RTSServerResponse
            {
                serverId = ServerId,
                uri = uri,
                serverName = PlayerSession.ServerDisplayName
            };
        }

        Uri BuildAdvertisedUri()
        {
            Transport advertiseTransport = Transport.active != null ? Transport.active : transport;
            if (advertiseTransport == null)
                throw new InvalidOperationException("No active transport to advertise.");

            Uri uri = advertiseTransport.ServerUri();
            string hostIp = !string.IsNullOrWhiteSpace(advertiseInterfaceIPv4)
                ? advertiseInterfaceIPv4
                : PlayerSession.LanInterfaceIPv4;

            if (string.IsNullOrWhiteSpace(hostIp))
                return uri;

            return new UriBuilder(uri) { Host = hostIp }.Uri;
        }

        protected override ServerRequest GetRequest() => new ServerRequest();

        protected override void ProcessResponse(RTSServerResponse response, IPEndPoint endpoint)
        {
            response.EndPoint = endpoint;
            if (!TryGetUriHostIPv4(response.uri, out _))
            {
                response.uri = new UriBuilder(response.uri)
                {
                    Host = response.EndPoint.Address.ToString()
                }.Uri;
            }

            OnServerFound.Invoke(response);
        }

        static bool TryGetUriHostIPv4(Uri uri, out string ipv4)
        {
            ipv4 = null;
            if (uri == null || string.IsNullOrWhiteSpace(uri.Host))
                return false;
            if (!IPAddress.TryParse(uri.Host, out IPAddress ip))
                return false;
            if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return false;
            ipv4 = uri.Host;
            return true;
        }
    }
}
