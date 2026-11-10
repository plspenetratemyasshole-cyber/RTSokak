using UnityEngine;

namespace RTSokak.Shared
{
    /// <summary>
    /// Конфигурация URL веб-сайта для авторизации аккаунтов.
    /// Добавить на объект Main Menu в сцене.
    /// Использует ТОТ ЖЕ сайт что и KaplaoGames (kaplao.ru).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RTSokak/Account/RTS Account Config")]
    public class RTSAccountConfig : MonoBehaviour
    {
        public static RTSAccountConfig Instance { get; private set; }

        [Tooltip("Base URL веб-сайта Kaplao Games (без trailing slash). По умолчанию: https://kaplao.ru")]
        [SerializeField] string websiteUrl = "https://kaplao.ru";

        public string WebsiteUrl
        {
            get
            {
                string url = string.IsNullOrWhiteSpace(websiteUrl) ? "https://kaplao.ru" : websiteUrl.Trim();
                return url.TrimEnd('/');
            }
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
