using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace R1
{
    public class LapEntry : MonoBehaviour
    {
        [Header("Texts")]
        /// <summary>Text field for the lap index label, e.g. "Lap 1".</summary>
        [SerializeField] private TextMeshProUGUI lapIndexText;

        /// <summary>Text field for the lap time, e.g. "01:23.45".</summary>
        [SerializeField] private TextMeshProUGUI lapTimeText;

        /// <summary>Text field for the lap delta, e.g. "+0.35" or "-0.27".</summary>
        [SerializeField] private TextMeshProUGUI deltaText;

        [Header("Optional")]
        /// <summary>Optional background image used to highlight the best lap.</summary>
        [SerializeField] private Image highlightBackground;    

        
        /// <summary>
        /// Assigns lap index, lap time, and optional highlight state.
        /// </summary>
        /// <param name="lapIndex">Zero-based lap index.</param>
        /// <param name="timeSeconds">Lap time in seconds.</param>
        /// <param name="highlight">Whether to enable best-lap highlight.</param>
        public void SetData(int lapIndex, float timeSeconds, bool highlight = false)
        {
            if (lapIndexText) lapIndexText.text = $"Lap {lapIndex + 1}";
            if (lapTimeText) lapTimeText.text = Format(timeSeconds);
            if (deltaText) deltaText.text = ""; 

            if (highlightBackground) highlightBackground.enabled = highlight;
        }

        
        /// <summary>
        /// Shows a placeholder row for an unfinished lap.
        /// </summary>
        /// <param name="lapIndex">Zero-based lap index.</param>
        public void SetPlaceholder(int lapIndex)
        {
            if (lapIndexText) lapIndexText.text = $"Lap {lapIndex + 1}";
            if (lapTimeText) lapTimeText.text = "--:--.---";
            if (deltaText) deltaText.text = "";

            if (highlightBackground) highlightBackground.enabled = false;
        }


        /// <summary>
        /// Sets the lap time delta text with formatting and color.
        /// </summary>
        /// <param name="deltaSeconds">Time difference to display in seconds.</param>
        /// <param name="color">Text color (e.g., green for improvement).</param>
        /// <param name="showSign">Whether to show '+' for positive deltas.</param>
        public void SetDelta(float deltaSeconds, Color color, bool showSign = true)
        {
            if (!deltaText) return;

            string sign = (deltaSeconds > 0f && showSign) ? "+" : "";
            deltaText.text = $"{sign}{deltaSeconds:0.000}";
            deltaText.color = color;
        }


        /// <summary>
        /// Formats a float time in seconds into "m:ss.mmm".
        /// Returns "--:--.---" for invalid values.
        /// </summary>
        /// <param name="t">Lap time in seconds.</param>
        /// <returns>Formatted string time.</returns>
        private string Format(float t)
        {
            if (t <= 0f || float.IsInfinity(t) || float.IsNaN(t)) return "--:--.---";
            int m = Mathf.FloorToInt(t / 60f);
            int s = Mathf.FloorToInt(t % 60f);
            int c = Mathf.FloorToInt((t - Mathf.Floor(t)) * 1000f);
            return $"{m:0}:{s:00}.{c:000}";
        }
    }
}
