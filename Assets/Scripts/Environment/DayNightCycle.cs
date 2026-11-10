using Mirror;
using UnityEngine;

namespace RTSokak.Environment
{
    /// <summary>
    /// Система смены дня и ночи.
    /// Полный цикл = 20 минут реального времени.
    /// Сервер — источник истины; клиенты получают время через NetworkMessage.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Header("Time Settings")]
        [SerializeField] private float cycleDuration = 1200f; // 20 минут в секундах
        [SerializeField] private float startTime = 0.25f; // Начинать с утра (0.25 = 6:00)
        [SerializeField] private float networkSyncInterval = 0.5f;
        
        [Header("Lighting")]
        [SerializeField] private Light directionalLight;
        [SerializeField] private Gradient lightColorGradient;
        [SerializeField] private AnimationCurve lightIntensityCurve;
        
        [Header("Ambient")]
        [SerializeField] private Gradient ambientColorGradient;
        [SerializeField] private AnimationCurve ambientIntensityCurve;
        
        [Header("Fog")]
        [SerializeField] private bool enableFog = true;
        [SerializeField] private Gradient fogColorGradient;
        [SerializeField] private AnimationCurve fogDensityCurve;
        [SerializeField] private float maxFogDensity = 0.01f;

        [Header("Sky Visuals")]
        [SerializeField] private bool useSkyVisuals = true;
        [SerializeField] private SkyVisuals skyVisuals;
        
        private float currentTime; // 0.0 - 1.0 (0 = полночь, 0.5 = полдень)
        private float nextNetworkSyncTime;
        private bool handlersRegistered;
        private bool clientTimeLocked; // клиент ждёт первый пакет от сервера
        
        private void Awake()
        {
            Instance = this;
            currentTime = startTime;
            
            if (directionalLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var light in lights)
                {
                    if (light.type == LightType.Directional)
                    {
                        directionalLight = light;
                        break;
                    }
                }
            }
            
            if (lightColorGradient == null || lightColorGradient.colorKeys.Length == 0)
                CreateDefaultLightGradient();
            if (ambientColorGradient == null || ambientColorGradient.colorKeys.Length == 0)
                CreateDefaultAmbientGradient();
            if (fogColorGradient == null || fogColorGradient.colorKeys.Length == 0)
                CreateDefaultFogGradient();
            
            if (lightIntensityCurve == null || lightIntensityCurve.length == 0)
                CreateDefaultLightIntensityCurve();
            if (ambientIntensityCurve == null || ambientIntensityCurve.length == 0)
                CreateDefaultAmbientIntensityCurve();
            if (fogDensityCurve == null || fogDensityCurve.length == 0)
                CreateDefaultFogDensityCurve();
            
            if (enableFog)
                RenderSettings.fog = true;

            if (useSkyVisuals)
                EnsureSkyVisuals();
            
            UpdateLighting();
        }

        void EnsureSkyVisuals()
        {
            if (skyVisuals == null)
                skyVisuals = GetComponent<SkyVisuals>();
            if (skyVisuals == null)
                skyVisuals = FindFirstObjectByType<SkyVisuals>();
            if (skyVisuals == null)
                skyVisuals = gameObject.AddComponent<SkyVisuals>();
            skyVisuals.EnsureBuilt();
        }

        private void OnEnable()
        {
            RegisterNetworkHandlers();
        }

        private void OnDisable()
        {
            UnregisterNetworkHandlers();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            UnregisterNetworkHandlers();
        }

        private void RegisterNetworkHandlers()
        {
            if (handlersRegistered)
                return;

            NetworkClient.RegisterHandler<TimeOfDayMessage>(OnClientTimeOfDayMessage);
            handlersRegistered = true;
        }

        private void UnregisterNetworkHandlers()
        {
            if (!handlersRegistered)
                return;

            NetworkClient.UnregisterHandler<TimeOfDayMessage>();
            handlersRegistered = false;
        }
        
        private void Update()
        {
            RegisterNetworkHandlers();

            bool isServer = NetworkServer.active;
            bool isClientOnly = NetworkClient.active && !NetworkServer.active;

            if (isServer)
            {
                currentTime += Time.deltaTime / cycleDuration;
                if (currentTime >= 1.0f)
                    currentTime -= 1.0f;

                if (Time.time >= nextNetworkSyncTime)
                {
                    nextNetworkSyncTime = Time.time + Mathf.Max(0.1f, networkSyncInterval);
                    NetworkServer.SendToAll(new TimeOfDayMessage { timeOfDay = currentTime });
                }
            }
            else if (isClientOnly)
            {
                // Клиент локально интерполирует между пакетами сервера
                if (clientTimeLocked)
                {
                    currentTime += Time.deltaTime / cycleDuration;
                    if (currentTime >= 1.0f)
                        currentTime -= 1.0f;
                }
            }
            else
            {
                // Оффлайн / ещё нет сети
                currentTime += Time.deltaTime / cycleDuration;
                if (currentTime >= 1.0f)
                    currentTime -= 1.0f;
            }
            
            UpdateLighting();
        }

        private void OnClientTimeOfDayMessage(TimeOfDayMessage msg)
        {
            if (NetworkServer.active)
                return; // хост уже источник истины

            float incoming = Mathf.Repeat(msg.timeOfDay, 1f);
            if (!clientTimeLocked)
            {
                currentTime = incoming;
                clientTimeLocked = true;
            }
            else
            {
                // Мягкая коррекция, чтобы не дёргать свет
                float delta = Mathf.DeltaAngle(currentTime * 360f, incoming * 360f) / 360f;
                if (Mathf.Abs(delta) > 0.02f)
                    currentTime = incoming;
                else
                    currentTime = Mathf.Repeat(currentTime + delta * 0.35f, 1f);
            }

            UpdateLighting();
        }
        
        private void UpdateLighting()
        {
            if (directionalLight != null)
            {
                float sunAngle = currentTime * 360f - 90f;
                directionalLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0);
                
                directionalLight.color = lightColorGradient.Evaluate(currentTime);
                directionalLight.intensity = lightIntensityCurve.Evaluate(currentTime);
            }
            
            RenderSettings.ambientLight = ambientColorGradient.Evaluate(currentTime);
            RenderSettings.ambientIntensity = ambientIntensityCurve.Evaluate(currentTime);
            
            // Юнит-игрок сам форсит Linear fog в LateUpdate — не трогаем mode/start/end/density
            bool unitFog = RTSokak.Network.UnitPlayerController.Instance != null &&
                           RTSokak.Network.UnitPlayerController.Instance.IsActive;
            if (enableFog && !unitFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = fogColorGradient.Evaluate(currentTime);
                RenderSettings.fogDensity = fogDensityCurve.Evaluate(currentTime) * maxFogDensity;
            }
            else if (enableFog && unitFog)
            {
                // Только цвет (UnitPlayerController подхватит в LateUpdate)
                Color fc = fogColorGradient.Evaluate(currentTime);
                fc.a = 1f;
                RenderSettings.fogColor = fc;
            }

            if (useSkyVisuals)
            {
                if (skyVisuals == null)
                    EnsureSkyVisuals();
                skyVisuals?.ApplyTime(currentTime, directionalLight);
            }
        }
        
        #region Default Gradients and Curves
        
        private void CreateDefaultLightGradient()
        {
            lightColorGradient = new Gradient();
            var colorKeys = new GradientColorKey[8];
            var alphaKeys = new GradientAlphaKey[2];
            
            colorKeys[0] = new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 0.0f);
            colorKeys[1] = new GradientColorKey(new Color(1f, 0.7f, 0.5f), 0.23f);
            colorKeys[2] = new GradientColorKey(new Color(1f, 0.95f, 0.9f), 0.3f);
            colorKeys[3] = new GradientColorKey(new Color(1f, 1f, 1f), 0.5f);
            colorKeys[4] = new GradientColorKey(new Color(1f, 0.95f, 0.9f), 0.7f);
            colorKeys[5] = new GradientColorKey(new Color(1f, 0.5f, 0.3f), 0.77f);
            colorKeys[6] = new GradientColorKey(new Color(0.4f, 0.3f, 0.5f), 0.85f);
            colorKeys[7] = new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 1.0f);
            
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            
            lightColorGradient.SetKeys(colorKeys, alphaKeys);
        }
        
        private void CreateDefaultAmbientGradient()
        {
            ambientColorGradient = new Gradient();
            var colorKeys = new GradientColorKey[6];
            var alphaKeys = new GradientAlphaKey[2];
            
            colorKeys[0] = new GradientColorKey(new Color(0.1f, 0.1f, 0.15f), 0.0f);
            colorKeys[1] = new GradientColorKey(new Color(0.5f, 0.4f, 0.3f), 0.23f);
            colorKeys[2] = new GradientColorKey(new Color(0.7f, 0.7f, 0.7f), 0.5f);
            colorKeys[3] = new GradientColorKey(new Color(0.5f, 0.35f, 0.25f), 0.77f);
            colorKeys[4] = new GradientColorKey(new Color(0.2f, 0.15f, 0.25f), 0.85f);
            colorKeys[5] = new GradientColorKey(new Color(0.1f, 0.1f, 0.15f), 1.0f);
            
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            
            ambientColorGradient.SetKeys(colorKeys, alphaKeys);
        }
        
        private void CreateDefaultFogGradient()
        {
            fogColorGradient = new Gradient();
            var colorKeys = new GradientColorKey[6];
            var alphaKeys = new GradientAlphaKey[2];
            
            colorKeys[0] = new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 0.0f);
            colorKeys[1] = new GradientColorKey(new Color(0.8f, 0.6f, 0.5f), 0.23f);
            colorKeys[2] = new GradientColorKey(new Color(0.7f, 0.8f, 0.9f), 0.5f);
            colorKeys[3] = new GradientColorKey(new Color(0.9f, 0.6f, 0.4f), 0.77f);
            colorKeys[4] = new GradientColorKey(new Color(0.3f, 0.2f, 0.4f), 0.85f);
            colorKeys[5] = new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 1.0f);
            
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            
            fogColorGradient.SetKeys(colorKeys, alphaKeys);
        }
        
        private void CreateDefaultLightIntensityCurve()
        {
            lightIntensityCurve = new AnimationCurve();
            lightIntensityCurve.AddKey(0.0f, 0.1f);
            lightIntensityCurve.AddKey(0.23f, 0.6f);
            lightIntensityCurve.AddKey(0.5f, 1.0f);
            lightIntensityCurve.AddKey(0.77f, 0.6f);
            lightIntensityCurve.AddKey(1.0f, 0.1f);
        }
        
        private void CreateDefaultAmbientIntensityCurve()
        {
            ambientIntensityCurve = new AnimationCurve();
            ambientIntensityCurve.AddKey(0.0f, 0.3f);
            ambientIntensityCurve.AddKey(0.23f, 0.6f);
            ambientIntensityCurve.AddKey(0.5f, 1.0f);
            ambientIntensityCurve.AddKey(0.77f, 0.6f);
            ambientIntensityCurve.AddKey(1.0f, 0.3f);
        }
        
        private void CreateDefaultFogDensityCurve()
        {
            fogDensityCurve = new AnimationCurve();
            fogDensityCurve.AddKey(0.0f, 0.5f);
            fogDensityCurve.AddKey(0.23f, 1.0f);
            fogDensityCurve.AddKey(0.5f, 0.3f);
            fogDensityCurve.AddKey(0.77f, 1.0f);
            fogDensityCurve.AddKey(1.0f, 0.5f);
        }
        
        #endregion
        
        public float GetTimeOfDay() => currentTime;
        
        public void SetTimeOfDay(float time)
        {
            currentTime = Mathf.Clamp01(time);
            clientTimeLocked = NetworkServer.active || !NetworkClient.active;
            UpdateLighting();

            if (NetworkServer.active)
            {
                nextNetworkSyncTime = Time.time;
                NetworkServer.SendToAll(new TimeOfDayMessage { timeOfDay = currentTime });
            }
        }
        
        public int GetCurrentHour()
        {
            return Mathf.FloorToInt(currentTime * 24f);
        }
        
        public bool IsDay()
        {
            float hour = currentTime * 24f;
            return hour >= 6f && hour < 18f;
        }
        
        public bool IsNight() => !IsDay();
    }

    public struct TimeOfDayMessage : NetworkMessage
    {
        public float timeOfDay;
    }
}
