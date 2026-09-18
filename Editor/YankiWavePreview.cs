#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ODAXR.YankiXR.Data;

namespace ODAXR.YankiXR.Tool
{
    public sealed class YankiWavePreview : ScriptableObject
    {
        [SerializeField] private YankiGridData gridData;
        [SerializeField] private Transform soundSource;
        [SerializeField] private LayerMask obstacleLayers = ~0;
        [SerializeField] private float bakeHeight = 0.8f;
        [SerializeField] private float visualSpeed = 5f;
        [SerializeField] private float pulseSpacing = 5f;
        [SerializeField] private float waveWidth = 0.8f;
        [SerializeField] private bool animate = true;
        [SerializeField] private bool showHeatmap = true;
        [SerializeField] private bool drawThroughGeometry;
        [SerializeField] private float drawDistance = 150f;
        [SerializeField] private int maxDrawnCells = 10000;

        private List<YankiPropagation.Edge>[] graph;
        private Vector3[] positions;
        private float[] distances;
        private float[] transmission;
        private YankiGridData cachedData;
        private int cachedSource = -1;
        private double lastUpdate;
        private float travel;
        private bool rebuildPending;
        private GameObject selectionOwner;

        internal bool IsSelected => selectionOwner != null &&
            Selection.activeObject == selectionOwner && Selection.Contains(selectionOwner);
        private string status = "Assign a baked Grid Data and an AudioSource on YankiBridge.";
        private readonly Vector3[] quad = new Vector3[4];

        internal void Configure(YankiGridData data, Transform source, GameObject owner)
        {
            selectionOwner = owner;
            int mask = data != null ? data.bakedObstacleLayers : ~0;
            float height = data != null ? data.bakedEyeHeight : 0.8f;
            if (gridData == data && soundSource == source && obstacleLayers.value == mask && bakeHeight == height)
                return;
            gridData = data;
            soundSource = source;
            obstacleLayers = mask;
            bakeHeight = height;
            Invalidate();
        }

        private void OnEnable()
        {
            lastUpdate = EditorApplication.timeSinceStartup;
            SceneView.duringSceneGui += DrawWaves;
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.projectChanged += Invalidate;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawWaves;
            EditorApplication.update -= Tick;
            Undo.undoRedoPerformed -= Invalidate;
            EditorApplication.projectChanged -= Invalidate;
            SceneView.RepaintAll();
        }

        private void Invalidate()
        {
            graph = null;
            distances = null;
            rebuildPending = true;
            status = "Preparing selected-source preview. A baked grid and AudioSource are required.";
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            SceneView.RepaintAll();
        }

        internal void DrawControls()
        {
            if (!IsSelected) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected Source — Wave Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select this source to see waves in Scene view. Grid and source come from " +
                "YankiBridge; obstacle layers and height come from the last bake. This is an editor-only illustration.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(gridData == null || soundSource == null || !gridData.IsBaked))
                if (GUILayout.Button("Refresh Wave Preview")) Rebuild();

            EditorGUI.BeginChangeCheck();
            animate = EditorGUILayout.Toggle("Animate", animate);
            showHeatmap = EditorGUILayout.Toggle("Show Baked Heatmap", showHeatmap);
            drawThroughGeometry = EditorGUILayout.Toggle("Draw Through Geometry", drawThroughGeometry);
            visualSpeed = EditorGUILayout.Slider("Visual Speed", visualSpeed, 0.1f, 30f);
            pulseSpacing = EditorGUILayout.Slider("Pulse Spacing", pulseSpacing, 1f, 30f);
            waveWidth = EditorGUILayout.Slider("Wave Width", waveWidth, 0.1f, pulseSpacing);
            drawDistance = EditorGUILayout.Slider("Draw Distance", drawDistance, 5f, 500f);
            maxDrawnCells = EditorGUILayout.IntSlider("Maximum Drawn Cells", maxDrawnCells, 100, 20000);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            if (GUILayout.Button("Restart Waves")) { travel = 0f; SceneView.RepaintAll(); }
            EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.LabelField("Cyan: stronger • Red: weaker • Bright band: wavefront", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Visual speed is for inspection, not the physical speed of sound.", EditorStyles.wordWrappedLabel);
        }

        private void Rebuild()
        {
            Invalidate();
            rebuildPending = false;
            if (gridData == null || !gridData.IsBaked || soundSource == null ||
                gridData.serializedCells.Count == 0 || !(gridData.cellSize > 0f)) return;
            try
            {
                Physics.SyncTransforms();
                positions = new Vector3[gridData.serializedCells.Count];
                for (int i = 0; i < positions.Length; i++)
                    positions[i] = gridData.serializedCells[i].position + Vector3.up * bakeHeight;
                graph = YankiAcousticBaker.BuildOpeningGraph(positions, gridData.cellSize, obstacleLayers);
                cachedData = gridData;
                cachedSource = -1;
                travel = 0f;
                UpdateSource();
            }
            finally { EditorUtility.ClearProgressBar(); }
            SceneView.RepaintAll();
        }

        private void UpdateSource()
        {
            if (graph == null || gridData == null || soundSource == null) return;
            if (gridData != cachedData || !gridData.IsBaked || positions.Length != gridData.serializedCells.Count)
            { Invalidate(); return; }
            int index = gridData.GetCellIndex(soundSource.position);
            if (index < 0 || index >= graph.Length) return;
            if (index == cachedSource) return;
            cachedSource = index;
            distances = YankiPropagation.ShortestPaths(graph, index);
            transmission = new float[graph.Length];
            int reached = 0;
            for (int i = 0; i < graph.Length; i++)
            {
                transmission[i] = 1f - gridData.GetOcclusion(index, i);
                if (!float.IsPositiveInfinity(distances[i])) reached++;
            }
            travel = 0f;
            status = $"Source cell: {index} | Open-path cells: {reached}/{graph.Length}. " +
                "Positions outside the grid use boundary cells. Heatmap excludes AudioSource distance rolloff.";
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void Tick()
        {
            if (!IsSelected) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - lastUpdate < 1.0 / 30.0) return;
            float dt = (float)(now - lastUpdate);
            lastUpdate = now;
            if (rebuildPending) Rebuild();
            int previousSource = cachedSource;
            UpdateSource();
            if (distances == null || soundSource == null) return;
            if (animate) travel += Mathf.Min(dt, 0.1f) * visualSpeed;
            if (animate || previousSource != cachedSource) SceneView.RepaintAll();
        }

        private void DrawWaves(SceneView view)
        {
            if (!IsSelected || Event.current.type != EventType.Repaint || distances == null || gridData == null ||
                soundSource == null || view.camera == null) return;
            Color oldColor = Handles.color;
            CompareFunction oldTest = Handles.zTest;
            try
            {
                Handles.zTest = drawThroughGeometry ? CompareFunction.Always : CompareFunction.LessEqual;
                float half = gridData.cellSize * 0.43f;
                int stride = Mathf.Max(1, Mathf.CeilToInt((float)positions.Length / maxDrawnCells));
                Vector3 camera = view.camera.transform.position;
                for (int i = 0; i < positions.Length; i += stride)
                {
                    Vector3 p = positions[i];
                    if ((p - camera).sqrMagnitude > drawDistance * drawDistance) continue;
                    float strength = transmission[i];
                    float pulse = 0f;
                    if (!float.IsPositiveInfinity(distances[i]) && travel >= distances[i])
                    {
                        float phase = Mathf.Repeat(travel - distances[i], pulseSpacing);
                        pulse = Mathf.Clamp01(1f - phase / waveWidth);
                    }
                    Color color = Color.Lerp(new Color(1f, 0.18f, 0.08f), Color.cyan, strength);
                    color.a = (showHeatmap ? 0.13f : 0f) + pulse * strength * 0.8f;
                    if (color.a < 0.01f) continue;
                    quad[0] = p + new Vector3(-half, 0, -half);
                    quad[1] = p + new Vector3(-half, 0, half);
                    quad[2] = p + new Vector3(half, 0, half);
                    quad[3] = p + new Vector3(half, 0, -half);
                    Handles.DrawSolidRectangleWithOutline(quad, color, Color.clear);
                }
                Handles.color = Color.cyan;
                Handles.DrawWireDisc(positions[cachedSource], Vector3.up, gridData.cellSize * 0.45f);
                Handles.Label(positions[cachedSource] + Vector3.up * 0.2f, "Yanki source cell");
            }
            finally { Handles.color = oldColor; Handles.zTest = oldTest; }
        }
    }
}
#endif
