using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    /// <summary>
    /// Represents a set of waypoints for a track.
    /// Provides utilities for collecting child transforms as nodes,
    /// wrapping indices for looping paths, and drawing gizmos for visualization.
    /// </summary>
    public class TrackWaypoints : MonoBehaviour
    {
        /// <summary>
        /// The color of the gizmo lines connecting waypoints.
        /// </summary>
        public Color lineColor = Color.cyan;

        /// <summary>
        /// The radius of the gizmo spheres drawn at each waypoint.
        /// </summary>
        [Range(0f, 1f)] public float sphereRadius = 0.2f;

        /// <summary>
        /// Whether the path loops back to the start after the last waypoint.
        /// </summary>
        public bool loop = true;

        /// <summary>
        /// The list of waypoint nodes collected from child transforms.
        /// </summary>
        public List<Transform> nodes;


        /// <summary>
        /// Collects all child transforms (excluding the root) and assigns them as nodes.
        /// </summary>
        private void CollectChildren()
        {
            nodes.Clear();
            foreach (var child in GetComponentsInChildren<Transform>())
            {
                if (child != transform)
                {
                    nodes.Add(child);
                }
            }
        }


        /// <summary>
        /// Wraps the given index to fit within the bounds of the node list.
        /// </summary>
        /// <param name="i">The index to wrap.</param>
        /// <returns>The wrapped or clamped index depending on loop mode.</returns>
        private int Wrap(int i)
        {
            if (loop)
            {
                // Apply modulo twice to ensure negative indices are converted back to positive
                int count = nodes.Count;
                int wrappedIndex = (i % count + count) % count;
                return wrappedIndex;
            }
            else
            {
                // Clamp index to the start or end if it exceeds the valid range
                return Mathf.Clamp(i, 0, nodes.Count - 1);
            }
        }


        /// <summary>
        /// Draws gizmos for waypoints in the scene view, including connecting lines and spheres.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (nodes.Count < 2) CollectChildren();
            Gizmos.color = lineColor;

            int segCount = loop ? nodes.Count : nodes.Count - 1;
            for (int i = 0; i < segCount; i++)
            {
                var a = nodes[i].position;
                var b = nodes[Wrap(i + 1)].position;
                Gizmos.DrawLine(a, b);
                Gizmos.DrawSphere(a, sphereRadius);
            }
        }
    }
}
