#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using ODAXR.YankiXR.Data;

namespace ODAXR.YankiXR.Tool
{
    public sealed class YankiAcousticBaker : EditorWindow
    {
        [SerializeField] private YankiGridData targetGridData;
        private LayerMask obstacleLayer = ~0;
        private float fallbackOcclusionPerWall = 0.6f;
        private float eyeHeightOffset = 0.8f;
        private bool useOpeningPaths = true;
        private float detourLossPerUnit = 0.15f;

        [MenuItem("Tools/ODAXR/Yanki/Acoustic Baker")]
        private static void OpenWindow()
        {
            var window = GetWindow<YankiAcousticBaker>();
            window.titleContent = new GUIContent("Acoustic Baker");
            window.minSize = new Vector2(380f, 310f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Yanki XR - Voxel Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            targetGridData = (YankiGridData)EditorGUILayout.ObjectField(
                new GUIContent("Grid Data Asset"), targetGridData, typeof(YankiGridData), false);

            // MaskField uses compact named-layer indices, not physics layer numbers.
            string[] layers = InternalEditorUtility.layers;
            int compactMask = 0;
            for (int i = 0; i < layers.Length; i++)
                if ((obstacleLayer.value & (1 << LayerMask.NameToLayer(layers[i]))) != 0)
                    compactMask |= 1 << i;
            compactMask = EditorGUILayout.MaskField("Obstacle Layer", compactMask, layers);
            int physicsMask = 0;
            for (int i = 0; i < layers.Length; i++)
                if ((compactMask & (1 << i)) != 0)
                    physicsMask |= 1 << LayerMask.NameToLayer(layers[i]);
            obstacleLayer = physicsMask;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Acoustic Fallback Settings", EditorStyles.boldLabel);
            fallbackOcclusionPerWall = EditorGUILayout.Slider(
                new GUIContent("Fallback Occlusion", "Default coefficient for obstacles without a YankiAcousticMaterial."),
                fallbackOcclusionPerWall, 0.1f, 1.0f);

            eyeHeightOffset = EditorGUILayout.FloatField("Eye Height Offset", eyeHeightOffset);

            useOpeningPaths = EditorGUILayout.Toggle("Use Opening Paths", useOpeningPaths);
            if (useOpeningPaths)
            {
                detourLossPerUnit = Mathf.Max(0f, EditorGUILayout.FloatField(
                    new GUIContent("Detour Loss Per Unit", "Attenuation for extra travel around obstacles."),
                    detourLossPerUnit));
                EditorGUILayout.HelpBox("The grid must cover openings and the route around obstacles. " +
                    "Use cells smaller than the opening. This is a path approximation, not a wave simulation.",
                    MessageType.Info);
            }
            EditorGUILayout.Space();

            if (GUILayout.Button("Bake", GUILayout.Height(32f)))
            {
                BakeFullMatrix();
            }
        }

        private void BakeFullMatrix()
        {
            if (targetGridData == null || targetGridData.serializedCells == null || targetGridData.serializedCells.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "A valid YankiGridData asset was not selected!", "OK");
                return;
            }

            if (targetGridData.cellSize <= 0f || float.IsNaN(targetGridData.cellSize) ||
                float.IsInfinity(targetGridData.cellSize))
            {
                EditorUtility.DisplayDialog("Error", "Cell size must be finite and positive.", "OK");
                return;
            }

            try
            {
                Physics.SyncTransforms();
                var cells = targetGridData.serializedCells;
                int count = cells.Count;

                // Calculate bounds
                Vector3 min = cells[0].position;
                Vector3 max = cells[0].position;

                for (int i = 1; i < count; i++)
                {
                    min = Vector3.Min(min, cells[i].position);
                    max = Vector3.Max(max, cells[i].position);
                }

                targetGridData.gridOrigin = min - Vector3.one * (targetGridData.cellSize * 0.5f);
                int sizeX = Mathf.RoundToInt((max.x - min.x) / targetGridData.cellSize) + 1;
                int sizeY = Mathf.RoundToInt((max.y - min.y) / targetGridData.cellSize) + 1;
                int sizeZ = Mathf.RoundToInt((max.z - min.z) / targetGridData.cellSize) + 1;
                targetGridData.gridSize = new Vector3Int(Mathf.Max(1, sizeX), Mathf.Max(1, sizeY), Mathf.Max(1, sizeZ));

                var positions = new Vector3[count];
                for (int i = 0; i < count; i++)
                    positions[i] = cells[i].position + Vector3.up * eyeHeightOffset;
                var graph = useOpeningPaths ? BuildOpeningGraph(positions, targetGridData.cellSize, obstacleLayer) : null;

                // 1. Initialize and bake the acoustic half-matrix
                targetGridData.InitializeMatrix(count);

                for (int i = 0; i < count; i++)
                {
                    Vector3 posI = positions[i];
                    float[] paths = graph != null ? YankiPropagation.ShortestPaths(graph, i) : null;

                    for (int j = i; j < count; j++)
                    {
                        if (i == j)
                        {
                            targetGridData.SetOcclusion(i, j, 0f);
                            continue;
                        }

                        Vector3 posJ = positions[j];
                        Vector3 dir = posJ - posI;
                        float dist = dir.magnitude;

                        RaycastHit[] hits = Physics.RaycastAll(posI, dir.normalized, dist, obstacleLayer);

                        // Calculate acoustic occlusion (YankiAcousticMaterial support)
                        float physicalOcclusion = CalculateRayOcclusion(hits);

                        if (paths != null)
                            physicalOcclusion = YankiPropagation.Combine(
                                physicalOcclusion, dist, paths[j], detourLossPerUnit);
                        targetGridData.SetOcclusion(i, j, physicalOcclusion);
                    }

                    if (i % 300 == 0)
                    {
                        EditorUtility.DisplayProgressBar("Yanki Acoustic Bake", $"Raycasting cells {i}/{count}...", (float)i / (count * 2));
                    }
                }

                // 2. Bake the voxel spatial map for runtime lookup
                EditorUtility.DisplayProgressBar("Yanki Acoustic Bake", "Building Spatial Voxel Map...", 0.8f);
                targetGridData.BuildSpatialLookup();

                targetGridData.bakedObstacleLayers = obstacleLayer.value;
                targetGridData.bakedEyeHeight = eyeHeightOffset;
                EditorUtility.SetDirty(targetGridData);
                AssetDatabase.SaveAssets();

                System.GC.Collect();

                Debug.Log($"<color=#00FF00>[YankiAcousticBaker]</color> Bake completed. " +
                          $"Cell Count: {count} | Voxel Size: {targetGridData.gridSize}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        internal static List<YankiPropagation.Edge>[] BuildOpeningGraph(
            Vector3[] positions, float cellSize, LayerMask obstacleLayer)
        {
            int count = positions.Length;
            var graph = new List<YankiPropagation.Edge>[count];
            var clear = new bool[count];
            float clearance = Mathf.Max(0.001f, cellSize * 0.01f);
            float maxDistance = cellSize * 1.5f;
            for (int i = 0; i < count; i++)
            {
                graph[i] = new List<YankiPropagation.Edge>();
                // A node inside an obstacle must not join otherwise disconnected rooms.
                clear[i] = !Physics.CheckSphere(positions[i], clearance, obstacleLayer);
            }
            for (int i = 0; i < count; i++)
            {
                if (i % 100 == 0)
                    EditorUtility.DisplayProgressBar("Yanki Acoustic Bake",
                        $"Finding opening paths {i}/{count}...", 0f);
                if (!clear[i]) continue;
                for (int j = i + 1; j < count; j++)
                {
                    if (!clear[j]) continue;
                    Vector3 delta = positions[j] - positions[i];
                    float distance = delta.magnitude;
                    if (distance <= 0f || distance > maxDistance) continue;
                    // Test both directions: rays originating inside geometry can miss its surface.
                    if (Physics.Raycast(positions[i], delta / distance, distance, obstacleLayer) ||
                        Physics.Raycast(positions[j], -delta / distance, distance, obstacleLayer)) continue;
                    graph[i].Add(new YankiPropagation.Edge(j, distance));
                    graph[j].Add(new YankiPropagation.Edge(i, distance));
                }
            }
            return graph;
        }

        /// <summary>
        /// Calculates the cumulative occlusion value by collecting all YankiAcousticMaterial
        /// components encountered by the raycast.
        /// </summary>
        private float CalculateRayOcclusion(RaycastHit[] hits)
        {
            if (hits == null || hits.Length == 0) return 0f;

            float combinedOcclusion = 0f;

            foreach (var hit in hits)
            {
                // Find YankiAcousticMaterial on the object or its parent
                YankiAcousticMaterial mat = hit.collider.GetComponent<YankiAcousticMaterial>();
                if (mat == null)
                {
                    mat = hit.collider.GetComponentInParent<YankiAcousticMaterial>();
                }

                // Use the material coefficient if available; otherwise use the default fallback value
                float absorption = (mat != null) ? mat.absorptionCoefficient : fallbackOcclusionPerWall;

                // Cumulative acoustic absorption: (1 - Total) * (1 - New)
                combinedOcclusion += absorption * (1f - combinedOcclusion);
            }

            return Mathf.Clamp01(combinedOcclusion);
        }
    }
}
#endif
