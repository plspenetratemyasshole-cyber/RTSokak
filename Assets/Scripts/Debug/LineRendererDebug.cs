using UnityEngine;

namespace RTSokak.Debug
{
    /// <summary>
    /// Отладочная утилита для проверки LineRenderer
    /// Добавь на любой GameObject и нажми Space для теста
    /// </summary>
    public class LineRendererDebug : MonoBehaviour
    {
        private LineRenderer testLine;

        private void Start()
        {
            // Создаём тестовую линию
            testLine = gameObject.AddComponent<LineRenderer>();
            testLine.startWidth = 0.2f;
            testLine.endWidth = 0.2f;
            
            // Пробуем разные шейдеры
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = Color.cyan;
            testLine.material = mat;
            
            testLine.startColor = Color.cyan;
            testLine.endColor = Color.magenta;
            
            // Рисуем простую линию
            testLine.positionCount = 2;
            testLine.SetPosition(0, transform.position);
            testLine.SetPosition(1, transform.position + Vector3.forward * 5f + Vector3.up);
            
            testLine.enabled = true;
            
            UnityEngine.Debug.Log($"Test LineRenderer created at {transform.position}");
            UnityEngine.Debug.Log($"LineRenderer material: {testLine.material.shader.name}");
            UnityEngine.Debug.Log($"LineRenderer enabled: {testLine.enabled}");
            UnityEngine.Debug.Log($"LineRenderer positions: {testLine.positionCount}");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                testLine.enabled = !testLine.enabled;
                UnityEngine.Debug.Log($"Test line toggled: {testLine.enabled}");
            }
            
            // Анимируем линию для проверки
            if (testLine.enabled)
            {
                Vector3 end = transform.position + Vector3.forward * 5f + Vector3.up * (1f + Mathf.Sin(Time.time));
                testLine.SetPosition(1, end);
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 300, 20), "Press SPACE to toggle test line");
            GUI.Label(new Rect(10, 30, 300, 20), $"Line enabled: {testLine.enabled}");
            GUI.Label(new Rect(10, 50, 300, 20), $"Shader: {testLine.material.shader.name}");
        }
    }
}
