using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Provides AI driving input using waypoint navigation.
    /// Calculates steering and acceleration based on the closest waypoint,
    /// applying distance offset and configurable steering force for smoother driving.
    /// </summary>
    public class AiInputProvider : MonoBehaviour
    {   
        /// <summary>
        /// Forward acceleration value applied to the vehicle (0..1).
        /// </summary>
        public float vertical;

        /// <summary>
        /// Steering input value (-1..1) determined by waypoint direction.
        /// </summary>
        public float horizontal;

        /// <summary>
        /// Index of the closest waypoint currently targeted.
        /// </summary>
        public int currentNode;

        [Header("AI settings")]
        /// <summary>
        /// Default acceleration value used by the AI.
        /// </summary>
        [Range(0, 1)] public float acceleration = 0.5f;

        /// <summary>
        /// Number of waypoints ahead to look when selecting the target waypoint.
        /// </summary>
        public int distanceOffset = 5;

        /// <summary>
        /// Steering force multiplier applied to horizontal input.
        /// </summary>
        public float sterrForce = 1f;


        /// <summary>
        /// Reference to the waypoint path (TrackWaypoints).
        /// </summary>
        private TrackWaypoints waypoints;

        /// <summary>
        /// Transform of the currently selected target waypoint.
        /// </summary>
        private Transform currentWaypoint;

        /// <summary>
        /// Cached list of waypoint transforms from TrackWaypoints.
        /// </summary>
        private List<Transform> nodes = new();


        /// <summary>
        /// Initializes waypoint references on start by finding a TrackWaypoints
        /// object in the scene tagged as "Path".
        /// </summary>
        private void Start()
        {
            var path = GameObject.FindGameObjectWithTag("Path");
            if (path != null)
            {
                waypoints = path.GetComponent<TrackWaypoints>();
                if (waypoints != null) nodes = waypoints.nodes;
            }
            currentWaypoint = transform;
        }


        /// <summary>
        /// Updates AI driving input every physics frame.
        /// </summary>
        private void FixedUpdate()
        {
            AIDrive();
        }


        /// <summary>
        /// Executes AI driving logic by calculating waypoint distance,
        /// adjusting steering, and applying forward acceleration.
        /// </summary>
        private void AIDrive()
        {
            CalculateDistanceOfWaypoints();
            Steering();
            vertical = acceleration;
        }


        /// <summary>
        /// Finds the nearest waypoint to the AI and updates the target waypoint
        /// using a distance offset to look ahead for smoother navigation.
        /// </summary>
        private void CalculateDistanceOfWaypoints()
        {
            if (nodes == null || nodes.Count == 0) return;

            Vector3 position = transform.position;
            float nearest = Mathf.Infinity;

            for (int i = 0; i < nodes.Count; i++)
            {
                float d = (nodes[i].position - position).magnitude;
                if (d < nearest)
                {
                    currentWaypoint = (i + distanceOffset) >= nodes.Count
                        ? nodes[1]
                        : nodes[i + distanceOffset];

                    nearest = d;
                    currentNode = i;
                }
            }
        }


        /// <summary>
        /// Calculates steering input by transforming the target waypoint
        /// position into local space and normalizing it.
        /// </summary>
        private void Steering()
        {
            if (currentWaypoint == null) return;

            Vector3 relative = transform.InverseTransformPoint(currentWaypoint.position);
            float mag = relative.magnitude;
            if (mag > 0f)
            {
                relative /= mag;
                horizontal = (relative.x / mag) * sterrForce;
            }
            else horizontal = 0f;
        }
    }
}