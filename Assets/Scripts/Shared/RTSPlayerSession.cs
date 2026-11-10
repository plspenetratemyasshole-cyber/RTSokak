using UnityEngine;

namespace RTSokak.Shared
{
    /// <summary>
    /// Локальная сессия игрока для меню и сети (nickname, account token).
    /// Аналог PlayerSession из KaplaoGames.
    /// </summary>
    public static class RTSPlayerSession
    {
        const string NicknameKey = "rts.player.nickname";
        const string AuthTokenKey = "rts.account.token";
        const string AuthEmailKey = "rts.account.email";
        const string AuthLoggedInKey = "rts.account.loggedIn";
        const string CameraPosXKey = "rts.camera.posX";
        const string CameraPosYKey = "rts.camera.posY";
        const string CameraPosZKey = "rts.camera.posZ";
        const string CameraZoomKey = "rts.camera.zoom";

        public const string DefaultNickname = "Player";

        public static string Nickname { get; private set; } = DefaultNickname;
        public static bool IsLoggedIn { get; private set; }
        public static string AccountEmail { get; private set; } = string.Empty;
        public static string AuthToken { get; private set; } = string.Empty;

        public static bool IsDefaultNickname =>
            string.Equals(Nickname, DefaultNickname, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Masked email для UI, например as*****@gmail.com
        /// </summary>
        public static string MaskedAccountEmail => MaskEmail(AccountEmail);

        public static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return string.Empty;

            email = email.Trim();
            int at = email.IndexOf('@');
            if (at <= 0 || at >= email.Length - 1)
                return "***";

            string local = email.Substring(0, at);
            string domain = email.Substring(at);
            string visible = local.Length <= 2 ? local : local.Substring(0, 2);
            int starCount = Mathf.Max(3, local.Length - visible.Length);
            return visible + new string('*', starCount) + domain;
        }

        public static void Load()
        {
            Nickname = PlayerPrefs.GetString(NicknameKey, DefaultNickname);
            IsLoggedIn = PlayerPrefs.GetInt(AuthLoggedInKey, 0) != 0;
            AccountEmail = PlayerPrefs.GetString(AuthEmailKey, string.Empty);
            AuthToken = PlayerPrefs.GetString(AuthTokenKey, string.Empty);

            if (IsLoggedIn && (string.IsNullOrWhiteSpace(AuthToken) || string.IsNullOrWhiteSpace(Nickname)))
                ClearAccount();
        }

        public static void SetNickname(string nickname)
        {
            if (IsLoggedIn)
                return;

            Nickname = SanitizeNickname(nickname);
            PlayerPrefs.SetString(NicknameKey, Nickname);
            PlayerPrefs.Save();
        }

        public static void SetAccount(string nickname, string email, string token)
        {
            Nickname = SanitizeNickname(nickname);
            AccountEmail = (email ?? string.Empty).Trim();
            AuthToken = token ?? string.Empty;
            IsLoggedIn = !string.IsNullOrWhiteSpace(AuthToken) && !string.IsNullOrWhiteSpace(Nickname);

            PlayerPrefs.SetString(NicknameKey, Nickname);
            PlayerPrefs.SetString(AuthEmailKey, AccountEmail);
            PlayerPrefs.SetString(AuthTokenKey, AuthToken);
            PlayerPrefs.SetInt(AuthLoggedInKey, IsLoggedIn ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ClearAccount()
        {
            IsLoggedIn = false;
            AccountEmail = string.Empty;
            AuthToken = string.Empty;
            PlayerPrefs.DeleteKey(AuthEmailKey);
            PlayerPrefs.DeleteKey(AuthTokenKey);
            PlayerPrefs.SetInt(AuthLoggedInKey, 0);
            PlayerPrefs.Save();
        }

        public static void SignOutKeepLocalNickname()
        {
            ClearAccount();
        }

        public static string SanitizeNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return DefaultNickname;

            nickname = nickname.Trim();
            return nickname.Length > 20 ? nickname.Substring(0, 20) : nickname;
        }

        /// <summary>
        /// Сохранить позицию камеры
        /// </summary>
        public static void SaveCameraPosition(Vector3 position, float orthographicSize)
        {
            PlayerPrefs.SetFloat(CameraPosXKey, position.x);
            PlayerPrefs.SetFloat(CameraPosYKey, position.y);
            PlayerPrefs.SetFloat(CameraPosZKey, position.z);
            PlayerPrefs.SetFloat(CameraZoomKey, orthographicSize);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Восстановить позицию камеры (возвращает false если нет сохранённой позиции)
        /// </summary>
        public static bool TryLoadCameraPosition(out Vector3 position, out float orthographicSize)
        {
            if (PlayerPrefs.HasKey(CameraPosXKey))
            {
                position = new Vector3(
                    PlayerPrefs.GetFloat(CameraPosXKey),
                    PlayerPrefs.GetFloat(CameraPosYKey),
                    PlayerPrefs.GetFloat(CameraPosZKey)
                );
                orthographicSize = PlayerPrefs.GetFloat(CameraZoomKey, 20f);
                return true;
            }

            position = Vector3.zero;
            orthographicSize = 20f;
            return false;
        }
    }
}
