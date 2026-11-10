using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RTSokak.UI
{
    /// <summary>
    /// Контроллер для UI авторизации в Settings Panel.
    /// Управляет состоянием кнопок и текста в зависимости от статуса авторизации.
    /// </summary>
    [DisallowMultipleComponent]
    public class AccountUIController : MonoBehaviour
    {
        [Header("UI References")]
        public TMP_InputField nicknameInput;
        public Button signInButton;
        public Button signOutButton;
        public TextMeshProUGUI statusLabel;

        private RTSokak.Shared.RTSAccountClient accountClient;
        private RTSokak.Shared.RTSAccountConfig accountConfig;

        private void Start()
        {
            EnsureAccountComponents();
            WireAccountEvents();
            WireButtonEvents();

            // Загрузить текущий nickname из PlayerPrefs
            RTSokak.Shared.RTSPlayerSession.Load();
            UpdateUI();
        }

        private void OnDestroy()
        {
            UnwireAccountEvents();
            UnwireButtonEvents();
        }

        private void WireButtonEvents()
        {
            if (signInButton != null)
            {
                signInButton.onClick.AddListener(OnSignInClicked);
                UnityEngine.Debug.Log("[AccountUIController] Sign In button listener added");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[AccountUIController] Sign In button is null!");
            }
            
            if (signOutButton != null)
            {
                signOutButton.onClick.AddListener(OnSignOutClicked);
                UnityEngine.Debug.Log("[AccountUIController] Sign Out button listener added");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[AccountUIController] Sign Out button is null!");
            }
            
            if (nicknameInput != null)
            {
                nicknameInput.onEndEdit.AddListener(OnNicknameChanged);
            }
        }

        private void UnwireButtonEvents()
        {
            if (signInButton != null)
                signInButton.onClick.RemoveListener(OnSignInClicked);
            
            if (signOutButton != null)
                signOutButton.onClick.RemoveListener(OnSignOutClicked);
            
            if (nicknameInput != null)
                nicknameInput.onEndEdit.RemoveListener(OnNicknameChanged);
        }

        private void EnsureAccountComponents()
        {
            if (accountClient == null)
                accountClient = RTSokak.Shared.RTSAccountClient.Instance;
            
            if (accountConfig == null)
            {
                var configObj = GameObject.Find("Account System");
                if (configObj != null)
                    accountConfig = configObj.GetComponent<RTSokak.Shared.RTSAccountConfig>();
            }

            if (accountClient == null)
            {
                UnityEngine.Debug.LogWarning("[AccountUIController] RTSAccountClient.Instance is null. Make sure Account System GameObject exists with RTSAccountClient component.");
            }
        }

        private void WireAccountEvents()
        {
            if (accountClient == null)
                return;

            // Отписаться (на случай повторного вызова)
            accountClient.SignedIn -= OnSignedIn;
            accountClient.SignedOut -= OnSignedOut;
            accountClient.SignInFailed -= OnSignInFailed;
            accountClient.StatusChanged -= OnStatusChanged;

            // Подписаться
            accountClient.SignedIn += OnSignedIn;
            accountClient.SignedOut += OnSignedOut;
            accountClient.SignInFailed += OnSignInFailed;
            accountClient.StatusChanged += OnStatusChanged;
        }

        private void UnwireAccountEvents()
        {
            if (accountClient == null)
                return;

            accountClient.SignedIn -= OnSignedIn;
            accountClient.SignedOut -= OnSignedOut;
            accountClient.SignInFailed -= OnSignInFailed;
            accountClient.StatusChanged -= OnStatusChanged;
        }

        private void UpdateUI()
        {
            bool isLoggedIn = RTSokak.Shared.RTSPlayerSession.IsLoggedIn;
            string nickname = RTSokak.Shared.RTSPlayerSession.Nickname;
            
            // Nickname input: редактируется только когда не залогинен
            if (nicknameInput != null)
            {
                nicknameInput.text = nickname;
                nicknameInput.interactable = !isLoggedIn;
            }

            // Sign In кнопка: видна только когда не залогинен
            if (signInButton != null)
                signInButton.gameObject.SetActive(!isLoggedIn);
            
            // Sign Out кнопка: видна только когда залогинен
            if (signOutButton != null)
                signOutButton.gameObject.SetActive(isLoggedIn);

            // Status label: меняет цвет и текст в зависимости от статуса
            if (statusLabel != null)
            {
                if (accountClient != null && accountClient.IsBusy)
                {
                    // Жёлтый во время процесса авторизации
                    statusLabel.text = accountClient.StatusMessage;
                    statusLabel.color = new Color(1f, 0.8f, 0.3f, 1f);
                }
                else if (isLoggedIn)
                {
                    // Зелёный когда залогинен
                    statusLabel.text = $"✓ Logged in as {nickname}\n{RTSokak.Shared.RTSPlayerSession.MaskedAccountEmail}";
                    statusLabel.color = new Color(0.3f, 1f, 0.3f, 1f);
                }
                else
                {
                    // Серый когда не залогинен
                    statusLabel.text = "Not logged in";
                    statusLabel.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                }
            }
        }

        /// <summary>
        /// Вызывается когда пользователь нажимает Sign In.
        /// Запускает процесс авторизации через браузер.
        /// </summary>
        public void OnSignInClicked()
        {
            UnityEngine.Debug.Log("[AccountUIController] Sign In button clicked");
            
            EnsureAccountComponents();
            WireAccountEvents();
            
            if (accountClient != null)
            {
                string url = accountConfig != null ? accountConfig.WebsiteUrl : null;
                UnityEngine.Debug.Log($"[AccountUIController] Calling BeginSignIn with URL: {url}");
                accountClient.BeginSignIn(url);
                UpdateUI();
            }
            else
            {
                UnityEngine.Debug.LogError("[AccountUIController] RTSAccountClient not found! Make sure Account System GameObject exists.");
            }
        }

        /// <summary>
        /// Вызывается когда пользователь нажимает Sign Out.
        /// Очищает сохранённые данные авторизации.
        /// </summary>
        public void OnSignOutClicked()
        {
            if (accountClient != null)
            {
                accountClient.SignOut();
                UpdateUI();
            }
        }

        /// <summary>
        /// Вызывается когда пользователь меняет nickname в поле ввода.
        /// Сохраняет nickname только если не залогинен.
        /// </summary>
        public void OnNicknameChanged(string newNickname)
        {
            if (!RTSokak.Shared.RTSPlayerSession.IsLoggedIn)
            {
                RTSokak.Shared.RTSPlayerSession.SetNickname(newNickname);
                UpdateUI();
            }
        }

        // === Event Handlers ===

        private void OnSignedIn()
        {
            UpdateUI();
        }

        private void OnSignedOut()
        {
            UpdateUI();
        }

        private void OnSignInFailed()
        {
            UpdateUI();
        }

        private void OnStatusChanged()
        {
            UpdateUI();
        }
    }
}
