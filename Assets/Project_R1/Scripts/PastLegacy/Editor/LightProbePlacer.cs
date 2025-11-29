#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;

namespace R1
{
    /// <summary>
    /// Editor tool for generating LightProbeGroups along a track using centerline waypoints.
    /// Supports cross-section rows, vertical layers, corner densification, and surface snapping.
    /// </summary>
    public class LightProbePlacer : EditorWindow
    {
        
        [Header("Path (Centerline Waypoints)")]
        /// <summary>
        /// Path waypoints used as the centerline reference for placing probes.
        /// </summary>
        [SerializeField] private Transform[] waypoints;

        /// <summary>
        /// Default sampling interval along straight path segments.
        /// </summary>
        [SerializeField] private float interval = 5f;          

        [Header("Cross Section (Width)")]
        /// <summary>
        /// Number of rows across the track cross-section (recommended 3–5).
        /// </summary>
        [SerializeField] private int rowsAcross = 5;

        /// <summary>
        /// Half-width of the track (road radius).
        /// </summary>            
        [SerializeField] private float trackHalfWidth = 5f;

        /// <summary>
        /// Lateral margin extending outward from the track (shell thickness).
        /// </summary>
        [SerializeField] private float lateralMargin = 1.5f;    



        [Header("Vertical Layers (Height)")]
        /// <summary>
        /// Number of vertical layers (recommended 2–3).
        /// </summary>
        [SerializeField] private int layers = 2;     

        /// <summary>
        /// Base height for the first vertical layer.
        /// </summary>           
        [SerializeField] private float baseHeight = 1.5f;  

        /// <summary>
        /// Vertical spacing between layers.
        /// </summary>     
        [SerializeField] private float layerStep = 2.0f;

        /// <summary>
        /// Extra vertical offset for the top layer.
        /// </summary>
        [SerializeField] private float topExtra = 1.0f;         


        [Header("Corners / Special Densify")]
        /// <summary>
        /// Whether to increase sampling density at corners.
        /// </summary>
        [SerializeField] private bool densifyCorners = true;

        /// <summary>
        /// Dot product threshold for detecting corners. Straight = 1, more bent = lower.
        /// </summary>
        [SerializeField, Range(0.9f, 0.999f)]
        private float cornerDotThreshold = 0.985f;

        /// <summary>
        /// Interval to use in corner sections (smaller for denser probes).
        /// </summary>             
        [SerializeField] private float cornerInterval = 2.0f;

        [Header("Surface Snap (Bottom Layer Only)")]
        /// <summary>
        /// Whether to snap the bottom layer to the surface below.
        /// </summary>
        [SerializeField] private bool snapToSurface = true;

        /// <summary>
        /// Layer mask for surfaces eligible for snapping.
        /// </summary>     
        [SerializeField] private LayerMask surfaceMask = ~0;    

        /// <summary>
        /// Upward offset from which to start surface raycasts.
        /// </summary>
        [SerializeField] private float rayUp = 20f;     

        /// <summary>
        /// Downward depth to search for surfaces during snapping.
        /// </summary>       
        [SerializeField] private float rayDown = 60f;

        /// <summary>
        /// Vertical offset applied above the hit surface.
        /// </summary>          
        [SerializeField] private float surfaceOffset = 1.2f;

        /// <summary>
        /// If true, use Terrain.SampleHeight for snapping instead of raycasts.
        /// </summary>    
        [SerializeField] private bool useTerrainSample = false;

        [MenuItem("Tools/Lighting/Light Probe Placer")]
        /// <summary>
        /// Opens the LightProbePlacer editor window.
        /// </summary>
        public static void Open()
        {
            var win = GetWindow<LightProbePlacer>();
            win.titleContent = new GUIContent("Light Probe Placer");
            win.minSize = new Vector2(420, 340);
            win.Show();
        }

        /// <summary>
        /// Draws the custom inspector GUI for the window.
        /// </summary>
        private void OnGUI()
        {
            var so = new SerializedObject(this);

            EditorGUILayout.PropertyField(so.FindProperty("waypoints"), true);
            interval = EditorGUILayout.FloatField("Interval (m)", interval);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cross Section (Width)", EditorStyles.boldLabel);
            rowsAcross = Mathf.Max(1, EditorGUILayout.IntField("Rows Across", rowsAcross));
            trackHalfWidth = EditorGUILayout.FloatField("Track Half Width", trackHalfWidth);
            lateralMargin = EditorGUILayout.FloatField("Lateral Margin", lateralMargin);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Vertical Layers (Height)", EditorStyles.boldLabel);
            layers = Mathf.Max(1, EditorGUILayout.IntField("Layers", layers));
            baseHeight = EditorGUILayout.FloatField("Base Height", baseHeight);
            layerStep = EditorGUILayout.FloatField("Layer Step", layerStep);
            topExtra = EditorGUILayout.FloatField("Top Extra", topExtra);

            EditorGUILayout.Space();
            densifyCorners = EditorGUILayout.Toggle("Densify Corners", densifyCorners);
            if (densifyCorners)
            {
                cornerDotThreshold = EditorGUILayout.Slider("Corner Dot Threshold", cornerDotThreshold, 0.9f, 0.999f);
                cornerInterval = EditorGUILayout.FloatField("Corner Interval", cornerInterval);
            }

            EditorGUILayout.Space();
            snapToSurface = EditorGUILayout.Toggle("Snap To Surface (Bottom Layer Only)", snapToSurface);
            if (snapToSurface)
            {
                surfaceMask = LayerMaskField("Surface Mask", surfaceMask);
                rayUp = EditorGUILayout.FloatField("Ray Up", rayUp);
                rayDown = EditorGUILayout.FloatField("Ray Down", rayDown);
                surfaceOffset = EditorGUILayout.FloatField("Surface Offset", surfaceOffset);
                useTerrainSample = EditorGUILayout.Toggle("Use Terrain.SampleHeight", useTerrainSample);
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Generate Light Probes", GUILayout.Height(32)))
            {
                Generate();
            }

            so.ApplyModifiedProperties();
        }


        /// <summary>
        /// Generates a LightProbeGroup based on current waypoint and configuration settings.
        /// </summary>
        private void Generate()
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                Debug.LogError("At least 2 waypoints are required.");
                return;
            }

            // Row offsets across the cross-section (uniform distribution from -1 to 1)
            var acrossOffsets = new List<float>();
            if (rowsAcross == 1) acrossOffsets.Add(0f);
            else
            {
                for (int i = 0; i < rowsAcross; i++)
                {
                    float t = i / (rowsAcross - 1f);           // 0..1
                    acrossOffsets.Add(Mathf.Lerp(-1f, 1f, t)); // -1..1
                }
            }

            float half = trackHalfWidth + lateralMargin;
            var positions = new List<Vector3>();

            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                Vector3 a = waypoints[i].position;
                Vector3 b = waypoints[i + 1].position;

                Vector3 dir = (b - a).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                float segLen = Vector3.Distance(a, b);

                // Corner densification
                float step = interval;
                if (densifyCorners && i > 0)
                {
                    Vector3 prevDir = (a - waypoints[i - 1].position).normalized;
                    float dot = Vector3.Dot(prevDir, dir);
                    if (dot < cornerDotThreshold) step = cornerInterval;
                }

                int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / Mathf.Max(0.01f, step)));
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    Vector3 center = Vector3.Lerp(a, b, t);

                    // Vertical layers
                    for (int layerIndex = 0; layerIndex < layers; layerIndex++)
                    {
                        float intendedY = baseHeight + layerIndex * layerStep + (layerIndex == layers - 1 ? topExtra : 0f);

                        // Across rows
                        for (int k = 0; k < acrossOffsets.Count; k++)
                        {
                            float off = acrossOffsets[k] * half;
                            Vector3 baseXZ = center + right * off;
                            Vector3 pos = baseXZ + Vector3.up * intendedY;

                            // Snap only the bottom layer (layerIndex == 0)
                            if (snapToSurface && layerIndex == 0)
                                pos = SnapToSurface(baseXZ, intendedY);

                            positions.Add(pos);
                        }
                    }
                }
            }

            var probeGroupObject = new GameObject("Track LightProbes_Volume");
            var probeGroup = probeGroupObject.AddComponent<LightProbeGroup>();
            probeGroup.probePositions = positions.ToArray();
            Selection.activeObject = probeGroupObject;

            Debug.Log($"[LightProbePlacer] Generation complete: {positions.Count} probes  (rows={rowsAcross}, layers={layers})");
        }

        /// <summary>
        /// Snaps a probe position to the detected surface below (only for the bottom layer).
        /// </summary>
        /// <param name="baseXZ">XZ base position.</param>
        /// <param name="intendedY">Fallback Y position if snapping fails.</param>
        /// <returns>Snapped or fallback position.</returns>
        private Vector3 SnapToSurface(Vector3 baseXZ, float intendedY)
        {
            // Prefer Terrain height if available
            if (useTerrainSample && Terrain.activeTerrain != null)
            {
                float groundY = Terrain.activeTerrain.SampleHeight(baseXZ) + Terrain.activeTerrain.transform.position.y;
                if (!float.IsNaN(groundY))
                    return new Vector3(baseXZ.x, groundY + surfaceOffset, baseXZ.z);
            }

            // Raycast downward from above
            Vector3 startTop = baseXZ + Vector3.up * rayUp;
            if (Physics.Raycast(startTop, Vector3.down, out var hit, rayUp + rayDown, surfaceMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * surfaceOffset;

            // Auxiliary raycast upward from below (e.g., under bridges)
            Vector3 startBottom = baseXZ - Vector3.up * rayDown;
            if (Physics.Raycast(startBottom, Vector3.up, out hit, rayUp + rayDown, surfaceMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * surfaceOffset;

            // Keep intended Y if no surface found
            return new Vector3(baseXZ.x, intendedY, baseXZ.z);
        }

        /// <summary>
        /// Draws a custom LayerMask field in the editor.
        /// </summary>
        /// <param name="label">Label text for the field.</param>
        /// <param name="layerMask">Current layer mask value.</param>
        /// <returns>Updated LayerMask value.</returns>
        private static LayerMask LayerMaskField(string label, LayerMask layerMask)
        {
            var layers = InternalEditorUtility.layers;
            int maskWithoutEmpty = 0;

            for (int i = 0; i < layers.Length; i++)
            {
                int layer = LayerMask.NameToLayer(layers[i]);
                if ((layerMask.value & (1 << layer)) != 0)
                    maskWithoutEmpty |= (1 << i);
            }

            maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers);

            int mask = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                if ((maskWithoutEmpty & (1 << i)) != 0)
                    mask |= (1 << LayerMask.NameToLayer(layers[i]));
            }
            layerMask.value = mask;
            return layerMask;
        }
    }
}
#endif