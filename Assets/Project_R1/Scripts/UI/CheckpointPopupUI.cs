using TMPro;
using UnityEngine;
using DG.Tweening; // DOTween

namespace R1
{
    /// <summary>
    /// Displays a transient checkpoint popup showing the current lap time and a delta
    /// versus a reference (e.g., previous best/split). Uses DOTween to animate fade
    /// and scale in/out, then hides automatically.
    /// </summary>
    public class CheckpointPopupUI : MonoBehaviour
    {
        [Header("Refs")]
        /// <summary>
        /// CanvasGroup used to control popup opacity during fade animations.
        /// </summary>
        [SerializeField] private CanvasGroup group;

        /// <summary>
        /// Root RectTransform whose scale is animated for the punch-in/out effect.
        /// </summary>
        [SerializeField] private RectTransform root;

        /// <summary>
        /// Text element that displays the current lap/split time.
        /// </summary>
        [SerializeField] private TextMeshProUGUI currentText;

        /// <summary>
        /// Text element that displays the time delta (e.g., +0.123 / -0.045).
        /// </summary>
        [SerializeField] private TextMeshProUGUI deltaText;

        [Header("Style")]
        /// <summary>
        /// Duration of the fade/scale-in segment (seconds).
        /// </summary>
        [SerializeField] private float inDuration = 0.18f;

        /// <summary>
        /// Duration to hold the popup fully visible before fading out (seconds).
        /// </summary>
        [SerializeField] private float holdDuration = 1.5f;

        /// <summary>
        /// Duration of the fade/scale-out segment (seconds).
        /// </summary>
        [SerializeField] private float outDuration = 0.25f;

        /// <summary>
        /// Target scale for the punch-in; the popup animates from ~0.96 to this value.
        /// </summary>
        [SerializeField] private float scaleIn = 1.06f;


        /// <summary>
        /// Text color for an improved (negative) delta time.
        /// </summary>
        [SerializeField] private Color improvedColor = new(0.3f, 1f, 0.4f, 1f);

        /// <summary>
        /// Text color for a worse (positive) delta time.
        /// </summary>
        [SerializeField] private Color worseColor = new(1f, 0.4f, 0.4f, 1f);

        /// <summary>
        /// Text color for a neutral/zero delta time, or when delta is not shown.
        /// </summary>
        [SerializeField] private Color neutralColor = new(0.8f, 0.8f, 0.8f, 1f);


        /// <summary>
        /// The currently running DOTween sequence controlling the popup animation.
        /// Killed and recreated on each <see cref="Show(float, float)"/> call.
        /// </summary>
        Tween seq;


        /// <summary>
        /// Shows the popup with the given current lap time and delta, animating in,
        /// holding briefly, then animating out. Any in-flight animation is cancelled.
        /// </summary>
        /// <param name="currentLapTime">The current lap/split time in seconds.</param>
        /// <param name="deltaSeconds">
        /// The time delta in seconds (positive = slower, negative = faster).
        /// Pass <c>float.NaN</c> to hide the delta line.
        /// </param>
        public void Show(float currentLapTime, float deltaSeconds)
        {
            if (seq != null && seq.IsActive()) seq.Kill();

            currentText.text = Format(currentLapTime);

            if (float.IsNaN(deltaSeconds))
            {
                deltaText.text = "";
                deltaText.color = neutralColor;
            }
            else
            {
                string sign = deltaSeconds > 0f ? "+" : "";
                deltaText.text = $"{sign}{deltaSeconds:0.000}";
                deltaText.color = Mathf.Abs(deltaSeconds) < 1e-3f ? neutralColor :
                                  (deltaSeconds < 0f ? improvedColor : worseColor);
            }

            group.alpha = 0f;
            root.localScale = Vector3.one * 0.96f;

            seq = DOTween.Sequence()
                .Append(group.DOFade(1f, inDuration))
                .Join(root.DOScale(scaleIn, inDuration).SetEase(Ease.OutCubic))
                .AppendInterval(holdDuration)
                .Append(group.DOFade(0f, outDuration))
                .Join(root.DOScale(1f, outDuration).SetEase(Ease.InCubic));
        }


        /// <summary>
        /// Formats a time value in seconds into <c>m:ss.mmm</c>.
        /// </summary>
        /// <param name="t">Time in seconds.</param>
        /// <returns>Formatted string like <c>1:23.456</c>.</returns>
        string Format(float t)
        {
            int m = Mathf.FloorToInt(t / 60f);
            int s = Mathf.FloorToInt(t % 60f);
            int c = Mathf.FloorToInt((t - Mathf.Floor(t)) * 1000f);
            return $"{m:0}:{s:00}.{c:000}";
        }
    }
}
