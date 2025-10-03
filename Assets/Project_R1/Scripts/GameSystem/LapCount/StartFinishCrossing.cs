using UnityEngine;
using System.Collections.Generic;

namespace R1
{
    /// <summary>
    /// Detects start/finish line crossings and validates laps using gating rules.
    /// Supports arming by specific mid-checkpoints (any/all), direction/speed checks,
    /// cooldown, and optional notifications to LapTracker and RaceSessionManager.
    /// </summary>
    public class StartFinishCrossing : MonoBehaviour
    {
        [Header("Refs")]
        /// <summary>Reference to the active RaceSessionManager used for race state and callbacks.</summary>
        public RaceSessionManager rsm;

        /// <summary>Transform that defines the start/finish line orientation. Defaults to this GameObject if null.</summary>
        public Transform line;                

        /// <summary>When true, a car must exit the trigger before the next lap can be armed/validated.</summary> 
        public bool requireExitToArm = true;

        /// <summary>Enable verbose debug logging for crossing and gating checks.</summary>
        public bool verbose = true;

        [Header("Line Direction / Plane Normal")]
        /// <summary>Forward direction of the line used to compute gating dot product. Defaults to line.forward if zero.</summary>
        public Vector3 lineDir = Vector3.zero;

        /// <summary>Mode used to compute the crossing plane normal.</summary>
        public enum NormalMode { UseLineRight, CrossUpXLineDir }

        /// <summary>How to compute the crossing plane normal: line.right or cross(up, lineDir).</summary>
        public NormalMode normalMode = NormalMode.UseLineRight;


        [Header("Gating")]
        /// <summary>Minimum time between accepted laps (cooldown), in seconds.</summary>
        public float minLapSeconds = 1.0f;

        /// <summary>Minimum forwardness (dot with lineDir) to accept a crossing. Range [-1..1].</summary>     
        [Range(-1f, 1f)] public float forwardDotThreshold = -0.2f;      // Relaxed for testing; raise to ~0.2–0.4 later.

        /// <summary>Minimum speed (m/s) required to accept a crossing.</summary>
        public float minSpeed = 0.0f;          // Relaxed for testing; increase later (e.g., 1.0–2.0).

        [Header("Arming (by specific mid CPs)")]
        /// <summary>
        /// Mid-checkpoints that can arm the S/F crossing. Exclude index 0 (S/F).
        /// If empty, a fallback behavior may arm on any mid.
        /// </summary>
        [SerializeField] private CheckpointTrigger[] armFromCheckpoints;    // Drag mid-CPs in inspector.  

        /// <summary>Optional indices of mid-checkpoints that can arm (exclude 0).</summary>
        [SerializeField] private int[] armFromIndices;

        /// <summary>When true, visiting any listed mid-checkpoint arms; when false, require all.</summary>
        [SerializeField] private bool armWhenAnyListedPassed = true;

        [Header("Lap Reset")]
        /// <summary>Disarm after a lap is accepted so mids must be visited again to re-arm.</summary>
        [SerializeField] private bool disarmOnLap = true;        // Disarm after a valid lap.

        /// <summary>Clear the mid-checkpoint bit mask when a lap is accepted.</summary>
        [SerializeField] private bool clearMidMaskOnLap = true;  // Reset mid mask after lap.

        /// <summary>Clear the visited-arm set when a lap is accepted.</summary>
        [SerializeField] private bool clearArmVisitedOnLap = true; // Reset visited list-based arming set.

        // 내부 집합
        private readonly HashSet<int> _armIdxSet = new();
        private readonly HashSet<int> _armVisited = new();

        [Header("Callbacks")]
        /// <summary>If true, notify LapTracker (local car) when a lap is validated.</summary>
        [SerializeField] bool notifyLapTracker = true;

        /// <summary>If true, notify RaceSessionManager when a lap is validated.</summary>
        [SerializeField] bool notifyRaceSession = true;

        // 내부 상태
        private Vector3 planePoint, planeNormal;
        private float prevSide = 0f;
        private bool armed = false;
        private float lastLapAt = -999f;
        private Rigidbody carRb;
        private Transform carRoot;

        // Added: bitmask for mid-CP visits (simple 32-bit cap; expand to arrays if more are needed).
        private int midCPMask = 0;


        /// <summary>
        /// Initialize line direction/plane normal, attach to the local car (RSM), build arming set,
        /// and set initial armed state based on <see cref="requireExitToArm"/>.
        /// Call this at race start.
        /// </summary>
        public void OnRaceStart()
        {
            if (!line) line = transform;
            if (lineDir == Vector3.zero) lineDir = line.forward.normalized;

            planePoint = line.position;
            planeNormal = (normalMode == NormalMode.UseLineRight)
                ? line.right.normalized
                : Vector3.Cross(Vector3.up, lineDir).normalized;

            carRoot = rsm && rsm.localCar ? rsm.localCar.transform : null;
            carRb = rsm && rsm.localCar ? rsm.localCar.GetComponent<Rigidbody>() : null;

            prevSide = carRoot ? SideOfLine(carRoot.position) : 0f;
            lastLapAt = Time.time - minLapSeconds;

            // Arm/mid-CP state initialization.
            armed = !requireExitToArm;
            midCPMask = 0;

            // Build set from list-based arming configuration.
            BuildArmSet();
            _armVisited.Clear();

            if (verbose)
                Debug.Log($"[S/F] OnRaceStart armIdxSet=[{string.Join(",", _armIdxSet)}] any={armWhenAnyListedPassed} armed={armed}");
        }


        /// <summary>
        /// Build the arming set from configured mid-checkpoint references and/or index list.
        /// Excludes index 0 (S/F).
        /// </summary>
        private void BuildArmSet()
        {
            _armIdxSet.Clear();

            if (armFromCheckpoints != null)
            {
                foreach (var cp in armFromCheckpoints)
                {
                    if (cp != null && cp.index > 0) _armIdxSet.Add(cp.index);
                }
            }
            if (armFromIndices != null)
            {
                foreach (var i in armFromIndices)
                {
                    if (i > 0) _armIdxSet.Add(i);
                }
            }
        }


        /// <summary>
        /// Arms the start/finish gate when the local car exits the trigger (if <see cref="requireExitToArm"/> is true).
        /// </summary>
        /// <param name="other">The collider that exited the trigger volume.</param>
        private void OnTriggerExit(Collider other)
        {
            if (!IsLocalCar(other)) return;
            if (rsm == null || rsm.localState != RaceState.Racing) return;
            if (requireExitToArm)
            {
                armed = true;
                if (verbose) Debug.Log("[S/F] EXIT -> armed=true");
            }
        }


        /// <summary>
        /// Detects a sign change against the crossing plane to determine a line crossing,
        /// then validates gating requirements (speed, direction/dot, cooldown), and if valid,
        /// accepts the lap and optionally notifies LapTracker and RaceSessionManager.
        /// </summary>
        private void FixedUpdate()
        {
            if (rsm == null || rsm.localState != RaceState.Racing || carRoot == null) return;

            float curSide = SideOfLine(carRoot.position);
            bool crossed = (Mathf.Abs(prevSide) > 1e-4f) && (Mathf.Sign(prevSide) != Mathf.Sign(curSide));
            if (verbose) Debug.Log($"[S/F] side prev={prevSide:0.###} cur={curSide:0.###} crossed={crossed} armed={armed}");
            prevSide = curSide;

            if (!crossed) return;
            if (!armed) { if (verbose) Debug.Log("[S/F] crossed but not armed"); return; }

            // Check travel direction/speed/cooldown.
            Vector3 v = carRb ? carRb.velocity : Vector3.zero;
            float speed = v.magnitude;
            float dot = (speed > 0.01f) ? Vector3.Dot(v.normalized, lineDir) : 1f;

            if (verbose) Debug.Log($"[S/F] gate speed={speed:0.00} dot={dot:0.00} elapsed={Time.time - lastLapAt:0.00}");

            if (speed < minSpeed) return;
            if (dot < forwardDotThreshold) return;
            if (Time.time - lastLapAt < minLapSeconds) return;


            lastLapAt = Time.time;

            // Prepare for next lap (optional resets).
            if (disarmOnLap) armed = false;          
            if (clearMidMaskOnLap) midCPMask = 0;          
            if (clearArmVisitedOnLap) _armVisited.Clear();    

            if (verbose) Debug.Log("[S/F] LAP COMPLETED!");

            // Notify LapTracker (local car).
            if (notifyLapTracker)
            {
                var lap = rsm && rsm.localCar ? rsm.localCar.GetComponent<LapTracker>() : null;
                lap?.OnCrossStartFinish();
            }

            // Notify RaceSessionManager (local only) — passing 'who' is recommended.
            if (notifyRaceSession)
            {
                // Safer path (recommended): if RSM exposes OnLapCrossed(GameObject who).
                // rsm?.OnLapCrossed(carRoot.gameObject);

                // Legacy path:
                rsm?.OnLapCompleted();
            }
        }


        /// <summary>
        /// Signed distance from point to crossing plane (positive/negative sides).
        /// </summary>
        /// <param name="p">World-space point to test.</param>
        /// <returns>Signed distance to plane defined by planePoint and planeNormal.</returns>
        private float SideOfLine(Vector3 p) => Vector3.Dot(p - planePoint, planeNormal);


        /// <summary>
        /// True if the collider belongs to the local player's car.
        /// Uses attached rigidbody and parent lookups.
        /// </summary>
        /// <param name="col">Collider to test.</param>
        /// <returns>True if local car, otherwise false.</returns>
        private bool IsLocalCar(Collider col)
        {
            var rb = col.attachedRigidbody;
            if (!rb) return false;
            return IsMyCarGO(rb.gameObject);
        }


        /// <summary>
        /// Draws debug rays for direction (green), plane normal (cyan),
        /// and a small wire cube for visual aid.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            var src = line ? line : transform;
            var p = src.position;
            var f = (lineDir == Vector3.zero) ? src.forward : lineDir.normalized;
            var n = (normalMode == NormalMode.UseLineRight) ? src.right.normalized : Vector3.Cross(Vector3.up, f).normalized;

            Gizmos.color = Color.green; Gizmos.DrawRay(p, f * 3f); // Travel direction
            Gizmos.color = Color.cyan; Gizmos.DrawRay(p, n * 3f); // Plane normal (left/right split)
            Gizmos.color = Color.yellow; Gizmos.DrawWireCube(p, new Vector3(0.2f, 3f, 6f)); // Visual aid
        }


        /// <summary>
        /// Returns true if the provided GameObject is the local player's car.
        /// Checks PhotonView ownership (if present), PlayerIdentity, and finally CarController equality with rsm.localCar.
        /// </summary>
        /// <param name="go">GameObject to test.</param>
        /// <returns>True if it's the local car.</returns>
        private bool IsMyCarGO(GameObject go)
        {
            if (!go || rsm == null) return false;

#if PHOTON_UNITY_NETWORKING
            // Check PhotonView ownership for local control.
            var pv = go.GetComponent<Photon.Pun.PhotonView>();
            if (pv && pv.IsMine) return true;
#endif

            // If PlayerIdentity is on the root, fetching InParent is safer.
            var pid = go.GetComponent<PlayerIdentity>();
            if (pid && pid.IsLocal) return true;

            // Final fallback: compare CarController against rsm.localCar.
            var car = go.GetComponent<CarController>();
            return car && car == rsm.localCar;
        }

        /// <summary>
        /// Called when a mid-checkpoint is passed by a vehicle. Arms the S/F crossing
        /// according to the configured ANY/ALL rule, but only for the local player's car.
        /// </summary>
        /// <param name="who">The GameObject that passed the checkpoint.</param>
        /// <param name="cpIndex">Checkpoint index (must be &gt; 0; 0 is S/F).</param>
        public void OnMidCheckpointPassed(GameObject who, int cpIndex)
        {
            if (cpIndex <= 0) return;                  
            if (!IsMyCarGO(who)) return;               

            // Existing bitmask update.
            int bit = 1 << ((cpIndex - 1) & 31);
            midCPMask |= bit;
            
            // If a list is configured, arm according to the list (ANY/ALL).
            if (_armIdxSet.Count > 0)
            {
                if (_armIdxSet.Contains(cpIndex))
                {
                    _armVisited.Add(cpIndex);
                    if (armWhenAnyListedPassed)
                    {
                        armed = true;
                    }
                    else
                    {
                        // Require all listed mids.
                        if (_armVisited.Count >= _armIdxSet.Count) armed = true;
                    }
                }
            }
            else
            {
                // Fallback if no list is configured: arm on any mid (remove if undesired).
                armed = true;
            }

            if (verbose)
                Debug.Log($"[S/F] mid CP(local) idx={cpIndex}, armed={armed}, visitedArm={_armVisited.Count}/{_armIdxSet.Count}");
        }

        /// <summary>
        /// 32-bit population count (number of set bits). Utility for bitmask analytics.
        /// </summary>
        /// <param name="x">Integer to count bits in.</param>
        /// <returns>Number of set bits (0..32).</returns>
        static int PopCount(int x)
        {
            unchecked
            {
                x = x - ((x >> 1) & 0x55555555);
                x = (x & 0x33333333) + ((x >> 2) & 0x33333333);
                return (int)((((x + (x >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
            }
        }
    }
}
