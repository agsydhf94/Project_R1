using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Dynamically limits vehicle speed based on track curvature.
    /// Estimates curvature ahead of the car and adjusts throttle/brake
    /// commands to keep lateral acceleration within limits.
    /// </summary>
    public class CurvatureSpeedLimiter : MonoBehaviour
    {
        [Header("Refs")]
        /// <summary>
        /// Transform of the vehicle being controlled.
        /// </summary>
        public Transform car;                  

        /// <summary>
        /// List of waypoints representing the track path (assumes loop).
        /// </summary>
        public List<Transform> waypoints;       

        /// <summary>
        /// Whether the path should wrap around as a loop.
        /// </summary>
        public bool loop = true;

        [Header("Speed Limits")]
        /// <summary>
        /// Maximum allowed straight-line speed in m/s.
        /// </summary>
        public float vMaxStraight = 35f;         

        /// <summary>
        /// Maximum lateral acceleration allowed in corners (m/s²).
        /// </summary>
        public float aLatMax = 8f;      

        /// <summary>
        /// Minimum curvature threshold to avoid division by zero.
        /// </summary>         
        public float minCurvature = 1e-4f;

        [Header("Lookahead / Sampling")]
        /// <summary>
        /// Lookahead distance along the path in meters.
        /// </summary>
        public float lookAhead = 8f;  

        /// <summary>
        /// Stride between previous/next nodes when estimating curvature.
        /// </summary>           
        public int sampleStride = 2;

        [Header("Throttle/Brake")]
        /// <summary>
        /// Gain factor for proportional throttle control.
        /// </summary>
        public float accelGain = 0.6f;           

        /// <summary>
        /// Gain factor for proportional brake control.
        /// </summary>
        public float brakeGain = 1.2f;     

        /// <summary>
        /// Speed error deadband (m/s) before throttle/brake reacts.
        /// </summary>      
        public float deadband = 0.5f;

        [Header("Debug (Readonly)")]
        /// <summary>
        /// Target speed calculated from curvature and straight limit.
        /// </summary>
        public float targetSpeed;  

        /// <summary>
        /// Estimated curvature value (1/m).
        /// </summary>              
        public float curvature;      

        /// <summary>
        /// Desired acceleration input (normalized).
        /// </summary>            
        public float desiredAccel;   

        /// <summary>
        /// Desired brake input (normalized).
        /// </summary>            
        public float desiredBrake;               

        /// <summary>
        /// Combined vertical control signal (-1..1).
        /// </summary>
        public float Vertical => Mathf.Clamp(desiredAccel - desiredBrake, -1f, 1f);


        /// <summary>
        /// Main update loop:
        /// finds closest/forward points on the path, estimates curvature,
        /// computes target speed from lateral-accel limit and straight cap,
        /// then derives proportional throttle/brake commands.
        /// </summary>
        void Update()
        {
            if (car == null || waypoints == null || waypoints.Count < 3) return;

            // 1) Find closest point P on path and lookahead point Q
            int seg; float t;
            Vector3 P = ClosestPointOnPath(car.position, out seg, out t);
            Vector3 Q = AdvanceAlongPath(seg, t, lookAhead);

            // 2) Estimate curvature κ around Q (prev, curr=Q, next samples)
            int qi = IndexOfNearestNode(Q);
            int prev = Wrap(qi - sampleStride);
            int next = Wrap(qi + sampleStride);
            curvature = EstimateCurvature(waypoints[prev].position, waypoints[qi].position, waypoints[next].position);

            // 3) Compute target speed based on lateral acceleration and straight cap
            float k = Mathf.Max(curvature, minCurvature);
            targetSpeed = Mathf.Min(Mathf.Sqrt(aLatMax / k), vMaxStraight);

            // 4) Compute throttle/brake commands based on error from current speed
            float v = GetForwardSpeed();
            float err = targetSpeed - v;

            if (err > deadband)
            {
                desiredAccel = Mathf.Clamp01(err * accelGain / 10f);
                desiredBrake = 0f;
            }
            else if (err < -deadband)
            {
                desiredAccel = 0f;
                desiredBrake = Mathf.Clamp01((-err) * brakeGain / 10f);
            }
            else
            {
                desiredAccel = 0f;
                desiredBrake = 0f;
            }
        }

        // === Utility ===


        /// <summary>
        /// Finds the closest point on the polyline path to a world position.
        /// Iterates all segments, projects the position onto each segment, and selects the nearest projection.
        /// </summary>
        /// <param name="pos">World position to test against the path.</param>
        /// <param name="bestSeg">Output: index of the segment that contains the closest point.</param>
        /// <param name="bestT">
        /// Output: normalized parameter (0..1) along <paramref name="bestSeg"/> where the closest point lies.
        /// </param>
        /// <returns>The closest point on the path in world space.</returns>
        Vector3 ClosestPointOnPath(Vector3 pos, out int bestSeg, out float bestT)
        {
            float best = float.MaxValue; bestSeg = 0; bestT = 0f;
            int n = waypoints.Count;
            int last = loop ? n : n - 1;
            for (int i = 0; i < last; i++)
            {
                int j = (i + 1) % n;
                Vector3 a = waypoints[i].position;
                Vector3 b = waypoints[j].position;
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(pos - a, ab) / ab.sqrMagnitude) : 0f;
                Vector3 p = a + t * ab;
                float d2 = (pos - p).sqrMagnitude;
                if (d2 < best) { best = d2; bestSeg = i; bestT = t; }
            }
            Vector3 A = waypoints[bestSeg].position;
            Vector3 B = waypoints[(bestSeg + 1) % n].position;
            return Vector3.Lerp(A, B, bestT);
        }


        /// <summary>
        /// Advances a given arc length along the path starting from a specific segment and parameter.
        /// Handles wrap-around for looped paths; clamps to the end for open paths.
        /// </summary>
        /// <param name="seg">Starting segment index.</param>
        /// <param name="tOnSeg">Normalized parameter (0..1) on the starting segment.</param>
        /// <param name="s">Distance in meters to advance along the path.</param>
        /// <returns>The world-space point located s meters ahead on the path.</returns>
        Vector3 AdvanceAlongPath(int seg, float tOnSeg, float s)
        {
            int n = waypoints.Count;
            Vector3 cur = Vector3.Lerp(waypoints[seg].position, waypoints[(seg + 1) % n].position, tOnSeg);
            float remain = s;
            int i = seg;
            while (remain > 0f)
            {
                Vector3 a = cur;
                Vector3 b = waypoints[(i + 1) % n].position;
                float len = Vector3.Distance(a, b);
                if (len >= remain || (!loop && i + 1 >= n - 1))
                    return Vector3.MoveTowards(a, b, remain);
                remain -= len;
                i = (i + 1) % n;
                cur = waypoints[i].position;
                if (!loop && i == n - 1) return cur;
            }
            return cur;
        }


        /// <summary>
        /// Returns the index of the waypoint node that is closest to the given point.
        /// Performs a linear search across all nodes.
        /// </summary>
        /// <param name="p">World position to compare against nodes.</param>
        /// <returns>The index of the nearest node.</returns>
        int IndexOfNearestNode(Vector3 p)
        {
            int best = 0; float d2 = float.MaxValue;
            for (int i = 0; i < waypoints.Count; i++)
            {
                float t = (p - waypoints[i].position).sqrMagnitude;
                if (t < d2) { d2 = t; best = i; }
            }
            return best;
        }


        /// <summary>
        /// Wraps or clamps an integer index to a valid node index depending on loop mode.
        /// </summary>
        /// <param name="i">Raw index (may be out of range or negative).</param>
        /// <returns>A valid index within [0, waypoints.Count-1].</returns>
        int Wrap(int i)
        {
            int n = waypoints.Count;
            return loop ? (i % n + n) % n : Mathf.Clamp(i, 0, n - 1);
        }

        /// <summary>
        /// Estimates curvature using three points via the circumscribed-circle formula.
        /// Uses κ = 1 / R = 4A / (a·b·c), where A is the triangle area and a,b,c are side lengths.
        /// </summary>
        /// <param name="a">First point of the triangle (world space).</param>
        /// <param name="b">Second point of the triangle (world space).</param>
        /// <param name="c">Third point of the triangle (world space).</param>
        /// <returns>Curvature κ in 1/m (non-negative). Returns 0 for degenerate triangles.</returns>
        float EstimateCurvature(Vector3 a, Vector3 b, Vector3 c)
        {
            float ab = Vector3.Distance(a, b);
            float bc = Vector3.Distance(b, c);
            float ca = Vector3.Distance(c, a);
            float s = Mathf.Max(ab * bc, Mathf.Max(bc * ca, ca * ab));

            if (ab < 1e-3f || bc < 1e-3f || ca < 1e-3f) return 0f;

            float area2 = Vector3.Cross(b - a, c - a).magnitude; // = 2 * area
            float kappa = (area2 > 1e-6f) ? (2f * area2) / (ab * bc * ca) : 0f; // 4A/(abc) = 2*(2A)/(abc)
            return Mathf.Abs(kappa);
        }


        /// <summary>
        /// Computes the car's current forward speed (m/s) by projecting Rigidbody velocity onto the car's forward direction.
        /// </summary>
        /// <returns>Forward speed in m/s; 0 if no Rigidbody is found.</returns>
        float GetForwardSpeed()
        {
            var rb = car.GetComponent<Rigidbody>();
            if (rb == null) return 0f;
            Vector3 v = rb.velocity;
            return Vector3.Dot(v, car.forward); // m/s
        }
    }
}
