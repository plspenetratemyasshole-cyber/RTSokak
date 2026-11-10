using System;
using System.Net;
using Mirror;
using Mirror.Discovery;

namespace RTSokak.Network
{
    public struct RTSServerResponse : NetworkMessage
    {
        public IPEndPoint EndPoint { get; set; }
        public Uri uri;
        public long serverId;
        public string serverName;
    }
}
