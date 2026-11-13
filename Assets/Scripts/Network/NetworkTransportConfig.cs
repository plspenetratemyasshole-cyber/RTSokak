using kcp2k;
using Mirror;
using RTSokak.Shared;
using UnityEngine;

namespace RTSokak.Network
{
    [DisallowMultipleComponent]
    public class NetworkTransportConfig : MonoBehaviour
    {
        public static NetworkTransportConfig Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            PlayerSession.Load();
            ApplyFromSession();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void SetUseTcp(bool useTcp)
        {
            PlayerSession.SetUseTcpTransport(useTcp);
            if (Instance != null)
                Instance.ApplyFromSession();
            else
                RTSNetworkManager.Singleton?.ApplySessionPort();
        }

        public void ApplyFromSession()
        {
            var networkManager = GetComponent<RTSNetworkManager>() ?? RTSNetworkManager.Singleton;
            if (networkManager == null)
                return;

            if (NetworkServer.active || NetworkClient.active)
            {
                UnityEngine.Debug.LogWarning(
                    "[NetworkTransportConfig] Cannot switch transport while connected.");
                return;
            }

            if (PlayerSession.Port == 0)
                PlayerSession.SetPort(PlayerSession.DefaultPort);

            if (PlayerSession.UseTcpTransport)
                ApplyTelepathy(networkManager);
            else
                ApplyKcp(networkManager);

            networkManager.ApplySessionPort();
        }

        static void ApplyTelepathy(RTSNetworkManager networkManager)
        {
            KcpTransport kcp = networkManager.GetComponent<KcpTransport>();
            if (kcp != null)
                kcp.enabled = false;

            TelepathyTransport telepathy = networkManager.GetComponent<TelepathyTransport>();
            if (telepathy == null)
                telepathy = networkManager.gameObject.AddComponent<TelepathyTransport>();

            telepathy.enabled = true;
            telepathy.Port = PlayerSession.Port;
            networkManager.transport = telepathy;
            Transport.active = telepathy;

            var discovery = networkManager.GetComponent<RTSNetworkDiscovery>();
            if (discovery != null)
                discovery.transport = telepathy;
        }

        static void ApplyKcp(RTSNetworkManager networkManager)
        {
            TelepathyTransport telepathy = networkManager.GetComponent<TelepathyTransport>();
            if (telepathy != null)
                telepathy.enabled = false;

            KcpTransport kcp = networkManager.GetComponent<KcpTransport>();
            if (kcp == null)
                kcp = networkManager.gameObject.AddComponent<KcpTransport>();

            kcp.enabled = true;
            kcp.Port = PlayerSession.Port;
            kcp.NoDelay = true;
            kcp.Interval = 10;
            kcp.FastResend = 2;
            networkManager.sendRate = 30;
            networkManager.transport = kcp;
            Transport.active = kcp;

            var discovery = networkManager.GetComponent<RTSNetworkDiscovery>();
            if (discovery != null)
                discovery.transport = kcp;
        }
    }
}
