using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace RTSokak.Network
{
    [Serializable]
    public class PublicServerEntry
    {
        public string name;
        public string address;
    }

    [Serializable]
    class PublicServerListDto
    {
        public PublicServerEntry[] servers;
    }

    public static class PublicServerList
    {
        public const string DefaultUrl =
            "https://gist.githubusercontent.com/plspenetratemyasshole-cyber/af969860a9658190f665ac2aee1fde7c/raw/servers.json";

        public static IEnumerator Fetch(string url, Action<List<PublicServerEntry>> onSuccess, Action<string> onError)
        {
            string requestUrl = string.IsNullOrWhiteSpace(url) ? DefaultUrl : url.Trim();
            string separator = requestUrl.Contains("?") ? "&" : "?";
            requestUrl = $"{requestUrl}{separator}t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            using UnityWebRequest request = UnityWebRequest.Get(requestUrl);
            request.timeout = 8;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error ?? "Request failed");
                yield break;
            }

            string json = request.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                onSuccess?.Invoke(new List<PublicServerEntry>());
                yield break;
            }

            PublicServerListDto dto;
            try
            {
                dto = JsonUtility.FromJson<PublicServerListDto>(json);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Invalid server list JSON: {ex.Message}");
                yield break;
            }

            var result = new List<PublicServerEntry>();
            if (dto?.servers != null)
            {
                for (int i = 0; i < dto.servers.Length; i++)
                {
                    PublicServerEntry entry = dto.servers[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.address))
                        continue;
                    entry.name = string.IsNullOrWhiteSpace(entry.name) ? "Public Server" : entry.name.Trim();
                    entry.address = entry.address.Trim();
                    result.Add(entry);
                }
            }

            onSuccess?.Invoke(result);
        }

        public static bool TryParseAddress(string address, out string host, out ushort port)
        {
            host = null;
            port = Shared.PlayerSession.DefaultPort;
            if (string.IsNullOrWhiteSpace(address))
                return false;

            address = address.Trim();
            int colon = address.LastIndexOf(':');
            if (colon > 0 && colon < address.Length - 1 &&
                ushort.TryParse(address.Substring(colon + 1), out ushort parsedPort))
            {
                host = address.Substring(0, colon);
                port = parsedPort;
                return !string.IsNullOrWhiteSpace(host);
            }

            host = address;
            return true;
        }
    }
}
