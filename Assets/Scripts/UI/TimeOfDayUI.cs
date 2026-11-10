using UnityEngine;
using TMPro;

namespace RTSokak.UI
{
    /// <summary>
    /// UI индикатор времени суток
    /// </summary>
    public class TimeOfDayUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private RTSokak.Environment.DayNightCycle dayNightCycle;
        
        [Header("Settings")]
        [SerializeField] private bool show24HourFormat = true;
        [SerializeField] private string timeFormat = "HH:mm";
        
        private void Start()
        {
            // Найти DayNightCycle если не указан
            if (dayNightCycle == null)
            {
                dayNightCycle = FindFirstObjectByType<RTSokak.Environment.DayNightCycle>();
            }
            
            if (dayNightCycle == null)
            {
                UnityEngine.Debug.LogWarning("[TimeOfDayUI] DayNightCycle not found in scene!");
                if (timeText != null)
                    timeText.text = "--:--";
            }
        }
        
        private void Update()
        {
            if (dayNightCycle != null && timeText != null)
            {
                float timeOfDay = dayNightCycle.GetTimeOfDay();
                int totalMinutes = Mathf.FloorToInt(timeOfDay * 24f * 60f);
                int hours = totalMinutes / 60;
                int minutes = totalMinutes % 60;
                
                if (show24HourFormat)
                {
                    timeText.text = $"{hours:D2}:{minutes:D2}";
                }
                else
                {
                    int displayHour = hours % 12;
                    if (displayHour == 0) displayHour = 12;
                    string ampm = hours >= 12 ? "PM" : "AM";
                    timeText.text = $"{displayHour}:{minutes:D2} {ampm}";
                }
            }
        }
    }
}
