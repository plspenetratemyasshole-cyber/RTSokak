using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace RTSokak.Editor
{
    /// <summary>
    /// Автоматическая настройка UI для авторизации через Kaplao Games сайт.
    /// Добавляет элементы авторизации в Settings Panel (Account section).
    /// </summary>
    public static class AccountAuthUiSetup
    {
        [MenuItem("Tools/RTS Setup/Setup Account Auth UI")]
        public static void SetupAccountAuthUI()
        {
            // Найти MainMenuUI компонент
            var mainMenuUI = Object.FindFirstObjectByType<RTSokak.UI.MainMenuUI>();
            if (mainMenuUI == null)
            {
                EditorUtility.DisplayDialog(
                    "MainMenuUI not found",
                    "Open the MainMenu scene first and make sure MainMenuUI component exists.",
                    "OK");
                return;
            }

            // Найти Settings Panel через SerializedObject
            var so = new SerializedObject(mainMenuUI);
            var settingsPanelProp = so.FindProperty("settingsPanel");
            if (settingsPanelProp == null || settingsPanelProp.objectReferenceValue == null)
            {
                EditorUtility.DisplayDialog(
                    "Settings Panel not found",
                    "Settings Panel reference is missing in MainMenuUI.",
                    "OK");
                return;
            }

            GameObject settingsPanel = settingsPanelProp.objectReferenceValue as GameObject;
            Transform settingsContent = settingsPanel.transform;

            // Найти существующий Viewport/Content или работать напрямую с panel
            Transform content = settingsContent.Find("Viewport/Content");
            if (content == null)
                content = settingsContent.Find("Content");
            if (content == null)
                content = settingsContent;

            // === Account Section Header ===
            GameObject accountHeaderObj = new GameObject("Account Section Header");
            accountHeaderObj.transform.SetParent(content, false);
            
            RectTransform headerRect = accountHeaderObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 40);

            TextMeshProUGUI headerText = accountHeaderObj.AddComponent<TextMeshProUGUI>();
            headerText.text = "Account";
            headerText.fontSize = 20;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.Left;
            headerText.color = Color.white;

            // === Nickname Input Row ===
            GameObject nicknameRowObj = CreateSettingRow(content, "Nickname Row");
            AddLabel(nicknameRowObj, "Nickname:");
            TMP_InputField nicknameInput = AddInputField(nicknameRowObj, "Enter nickname...");

            // === Account Status Label ===
            GameObject statusRowObj = CreateSettingRow(content, "Account Status Row");
            TextMeshProUGUI statusLabel = AddFullWidthLabel(statusRowObj, "Not logged in");
            statusLabel.fontSize = 14;
            statusLabel.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            statusLabel.alignment = TextAlignmentOptions.Center;

            // === Sign In Button Row ===
            GameObject signInRowObj = CreateSettingRow(content, "Sign In Row");
            Button signInButton = AddButton(signInRowObj, "Sign In");

            // === Sign Out Button Row ===
            GameObject signOutRowObj = CreateSettingRow(content, "Sign Out Row");
            Button signOutButton = AddButton(signOutRowObj, "Sign Out");

            // Найти или создать RTSAccountClient и RTSAccountConfig
            GameObject accountSystemObj = GameObject.Find("Account System");
            if (accountSystemObj == null)
            {
                accountSystemObj = new GameObject("Account System");
                accountSystemObj.AddComponent<RTSokak.Shared.RTSAccountConfig>();
                accountSystemObj.AddComponent<RTSokak.Shared.RTSAccountClient>();
                UnityEngine.Debug.Log("[AccountAuthUiSetup] Created Account System GameObject with RTSAccountConfig and RTSAccountClient");
            }

            // Создать контроллер для UI
            GameObject controllerObj = new GameObject("Account UI Controller");
            controllerObj.transform.SetParent(settingsPanel.transform, false);
            var controller = controllerObj.AddComponent<RTSokak.UI.AccountUIController>();
            
            controller.nicknameInput = nicknameInput;
            controller.signInButton = signInButton;
            controller.signOutButton = signOutButton;
            controller.statusLabel = statusLabel;

            // Кнопки подключаются в AccountUIController.Start() через WireButtonEvents()

            EditorUtility.SetDirty(mainMenuUI.gameObject);
            Selection.activeGameObject = accountHeaderObj;

            EditorUtility.DisplayDialog(
                "Account Auth UI Setup Complete",
                "Added to Settings Panel:\n" +
                "• Account section header\n" +
                "• Nickname input field\n" +
                "• Sign In / Sign Out buttons\n" +
                "• Status label\n" +
                "• Account UI Controller\n\n" +
                "Make sure 'Account System' GameObject has RTSAccountConfig and RTSAccountClient components.",
                "OK");
        }

        private static GameObject CreateSettingRow(Transform parent, string name)
        {
            GameObject rowObj = new GameObject(name);
            rowObj.transform.SetParent(parent, false);
            
            RectTransform rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0, 1);
            rowRect.anchorMax = new Vector2(1, 1);
            rowRect.pivot = new Vector2(0.5f, 1);
            rowRect.sizeDelta = new Vector2(0, 40);

            HorizontalLayoutGroup layout = rowObj.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 5, 5);

            return rowObj;
        }

        private static void AddLabel(GameObject parent, string text)
        {
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(parent.transform, false);
            
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(120, 30);

            TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 16;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Left;
        }

        private static TMP_InputField AddInputField(GameObject parent, string placeholder)
        {
            GameObject inputObj = new GameObject("Input Field");
            inputObj.transform.SetParent(parent.transform, false);
            
            RectTransform inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(250, 30);

            Image inputImage = inputObj.AddComponent<Image>();
            inputImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();

            // Placeholder
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(inputObj.transform, false);
            RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10, 5);
            placeholderRect.offsetMax = new Vector2(-10, -5);
            
            TextMeshProUGUI placeholderText = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholderText.text = placeholder;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            placeholderText.fontSize = 14;
            placeholderText.alignment = TextAlignmentOptions.Left;

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);
            
            TextMeshProUGUI inputText = textObj.AddComponent<TextMeshProUGUI>();
            inputText.text = "";
            inputText.color = Color.white;
            inputText.fontSize = 14;
            inputText.alignment = TextAlignmentOptions.Left;

            inputField.textViewport = textRect;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholderText;
            inputField.characterLimit = 20;

            return inputField;
        }

        private static TextMeshProUGUI AddFullWidthLabel(GameObject parent, string text)
        {
            GameObject labelObj = new GameObject("Full Width Label");
            labelObj.transform.SetParent(parent.transform, false);
            
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 16;
            label.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            label.alignment = TextAlignmentOptions.Center;

            return label;
        }

        private static Button AddButton(GameObject parent, string labelText)
        {
            GameObject buttonObj = new GameObject("Button");
            buttonObj.transform.SetParent(parent.transform, false);
            
            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(200, 35);

            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 0.9f, 1f);

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = labelText;
            text.color = Color.white;
            text.fontSize = 16;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;

            return button;
        }
    }
}
