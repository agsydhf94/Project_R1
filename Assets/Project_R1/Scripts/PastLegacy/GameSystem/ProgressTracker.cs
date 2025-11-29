// 09_ProgressTracker.cs
// 진행도 스칼라 계산기 – 랩/체크포인트/거리 기반(간단 버전)
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Computes a monotonically increasing progress scalar <c>dp</c> along a looped track.
    /// Tracks the current segment index and interpolation value, projects world position
    /// to arc length, and applies a pivot-based wrap counter with hysteresis so that
    /// progress increases smoothly across Start/Finish.
    /// </summary>
    public class ProgressTracker : MonoBehaviour
    {
        [Header("Injects")]
        /// <summary>
        /// Ordered array of track checkpoints, where index 0 is Start/Finish.
        /// Must contain at least two transforms for a valid loop.
        /// </summary>
        public Transform[] checkpoints;

        /// <summary>
        /// Optional lap tracker reference; not strictly required for progress computation.
        /// </summary>
        public LapTracker lap;

        /// <summary>
        /// Optional ranking manager used for registration so external ranking can query progress.
        /// </summary>
        private RankManager rankManager;

        [Header("Ranking Pivot")]
        /// <summary>
        /// Pivot transform used to convert absolute arc length into a pivot-relative distance
        /// and to count wraps (crossings) for monotonic progress.
        /// </summary>
        public Transform pivot;

        [Header("Debug (읽기 전용)")]
        /// <summary>Current segment index on the piecewise linear track.</summary>
        public int segIndex;

        /// <summary>Interpolation (0..1) within the current segment.</summary>      
        public float segT;

        /// <summary>Total loop length of the track, computed from the checkpoints.</summary>       
        public float trackLength;

         /// <summary>
        /// Distance progress that increases monotonically: <c>dp = cycles * trackLength + fromPivot</c>.
        /// </summary>
        public float dp { get; private set; }  

        /// <summary>
        /// Cumulative arc-length table where <c>cum[i]</c> is the distance at checkpoint <c>i</c>.
        /// </summary>
        private float[] cum;

        /// <summary>Number of checkpoints.</summary>
        private int n;

        /// <summary>Pivot position in arc length (0..trackLength).</summary>
        private float pivotS;

        /// <summary>Previous frame's pivot-relative distance.</summary>           
        private float lastFromPivot = -1f;

        /// <summary>Accumulated number of forward wraps past the pivot.</summary>
        private int pivotCycles = 0;    

        // Wrap-detection hysteresis: count only high→low crossings in the forward direction
        private float wrapHiFrac = 0.7f;  // previous value above L*0.7
        private float wrapLoFrac = 0.3f;  // and current value below L*0.3 → wrap


        /// <summary>
        /// Registers this tracker with a <see cref="RankManager"/> so external systems
        /// can compare progress values between competitors.
        /// </summary>
        void OnEnable()
        {
            rankManager = FindObjectOfType<RankManager>();
            rankManager?.Register(this);
        }


        /// <summary>
        /// Validates checkpoint references, builds cumulative distances, selects the pivot,
        /// and initializes the first-frame pivot-relative distance to avoid false wrap detection.
        /// </summary>
        void Start()
        {
            if (checkpoints == null || checkpoints.Length < 2)
            {
                Debug.LogError("[ProgressTracker] Not enough checkpoints.");
                enabled = false;
                return;
            }

            n = checkpoints.Length;
            cum = new float[n + 1];

            float acc = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                acc += Vector3.Distance(checkpoints[i].position, checkpoints[j].position);
                cum[i + 1] = acc;
            }
            trackLength = acc;

            segIndex = 0;
            segT = 0f;

            // Set pivot (use S/F=0 if not provided)
            if (!pivot && checkpoints != null && checkpoints.Length > 0)
                pivot = checkpoints[0];
            pivotS = ProjectS(pivot ? pivot.position : checkpoints[0].position, out _);

            // Record fromPivot at race start to avoid misdetecting a wrap in the first frame
            float s0 = ProjectS(transform.position, out _);
            lastFromPivot = (s0 - pivotS + trackLength) % trackLength;
        }


        /// <summary>
        /// Updates progress each frame by tracking the current segment,
        /// projecting position to arc length, and applying pivot-based wrap counting.
        /// </summary>
        void Update()
        {
            UpdateProgress();
        }


        /// <summary>
        /// Unregisters this tracker from the <see cref="RankManager"/> on disable.
        /// </summary>
        void OnDisable()
        {
            rankManager?.Unregister(this);
        }


        /// <summary>
        /// Advances the segment index/parameter around the loop, computes absolute arc length,
        /// converts to pivot-relative distance, detects wraps with hysteresis, and updates <see cref="dp"/>.
        /// </summary>
        private void UpdateProgress()
        {
            if (n < 2) return;

            // Segment tracking (kept as in original code)
            int i = segIndex;
            int j = (i + 1) % n;

            Vector3 a = checkpoints[i].position;
            Vector3 b = checkpoints[j].position;
            Vector3 p = transform.position;

            Vector3 ab = b - a;
            float abLen2 = ab.sqrMagnitude;
            if (abLen2 < 1e-6f)
            {
                segIndex = (segIndex + 1) % n;
                segT = 0f;
            }
            else
            {
                float t = Vector3.Dot(p - a, ab) / abLen2;

                if (t > 1f)
                {
                    segIndex = (segIndex + 1) % n;
                    segT = 0f;
                }
                else if (t < 0f)
                {
                    segIndex = (segIndex - 1 + n) % n;
                    segT = 1f;
                }
                else
                {
                    segT = t;
                }
            }

            // Absolute arc length s (0..L)
            i = segIndex;
            j = (i + 1) % n;

            float segLen = Vector3.Distance(checkpoints[i].position, checkpoints[j].position);
            float s = cum[i] + Mathf.Clamp01(segT) * segLen;

            // Pivot-relative fromPivot (0..L)
            float fromPivot = (s - pivotS + trackLength) % trackLength;

            // Wrap detection → accumulate pivot cross count
            if (lastFromPivot >= 0f && trackLength > 1e-4f)
            {
                float hi = trackLength * wrapHiFrac;
                float lo = trackLength * wrapLoFrac;

                // Forward wrap: (large value) -> (small value)
                if (lastFromPivot > hi && fromPivot < lo)
                {
                    pivotCycles++;
                }
                // (Optional) Reverse wrap: (small value) -> (large value) -> decrease counter
                else if (lastFromPivot < lo && fromPivot > hi)
                {
                    // If not needed, keep this as-is or remove
                    pivotCycles = Mathf.Max(0, pivotCycles - 1);
                }
            }
            lastFromPivot = fromPivot;

            // Final monotonic progress
            dp = pivotCycles * trackLength + fromPivot;
        }

        /// <summary>
        /// Projects a world-space position onto the loop to obtain its arc-length coordinate.
        /// </summary>
        /// <param name="pos">World position to project.</param>
        /// <param name="bestSeg">Returns the index of the closest segment.</param>
        /// <returns>Arc length in [0..trackLength] corresponding to <paramref name="pos"/>.</returns>
        private float ProjectS(Vector3 pos, out int bestSeg)
        {
            bestSeg = 0;
            if (checkpoints == null || checkpoints.Length < 2) return 0f;

            int bestI = 0;
            float bestT = 0f;
            float bestDist = float.PositiveInfinity;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 A = checkpoints[i].position;
                Vector3 B = checkpoints[j].position;
                Vector3 AB = B - A;

                float len2 = AB.sqrMagnitude;
                float t = (len2 > 1e-6f) ? Mathf.Clamp01(Vector3.Dot(pos - A, AB) / len2) : 0f;
                Vector3 proj = A + AB * t;
                float d2 = (pos - proj).sqrMagnitude;

                if (d2 < bestDist)
                {
                    bestDist = d2;
                    bestI = i;
                    bestT = t;
                }
            }

            float segLen = Vector3.Distance(checkpoints[bestI].position, checkpoints[(bestI + 1) % n].position);
            bestSeg = bestI;
            return cum[bestI] + bestT * segLen;
        }
    }
}
