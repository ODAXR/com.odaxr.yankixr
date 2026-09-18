using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using ODAXR.YankiXR.Data;

namespace ODAXR.YankiXR.Tool
{
    public class YankiGridRecognizer : EditorWindow
    {
        private enum EnvironmentSource
        {
            Scene,
            Custom
        }

        private float cellSize = 1.0f;

        private bool drawGizmos = true;
        private bool drawObstacleGizmos = true;
        private float gizmoDrawDistance = 150f;

        [SerializeField]
        private bool showDataPositions = true;

        private LayerMask groundLayer = ~0;

        private EnvironmentSource environmentSource =
            EnvironmentSource.Scene;

        private GameObject customEnvironment;

        private float customRadius = 50.0f;

        private List<YankiCellData> generatedCells =
            new List<YankiCellData>();

        private List<Bounds> obstacleBounds =
            new List<Bounds>();

        private YankiGridData targetGridData;

        [MenuItem("Tools/ODAXR/Yanki/Grid Recognizer")]
        public static void ShowWindow()
        {
            GetWindow<YankiGridRecognizer>(
                "Yanki Grid Recognizer"
            );
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            GUILayout.Label(
                "ODAXR - Yanki XR Automatic Floor Baker",
                EditorStyles.boldLabel
            );

            GUILayout.Space(6);

            cellSize = EditorGUILayout.FloatField(
                "Cell Size",
                cellSize
            );

            cellSize = Mathf.Max(
                0.01f,
                cellSize
            );

            groundLayer = LayerMaskField(
                "Ground Layer",
                groundLayer
            );

            GUILayout.Space(6);

            environmentSource =
                (EnvironmentSource)EditorGUILayout.EnumPopup(
                    "Environment Source",
                    environmentSource
                );

            GUILayout.Space(4);

            if (environmentSource ==
                EnvironmentSource.Custom)
            {
                customEnvironment =
                    (GameObject)EditorGUILayout.ObjectField(
                        "Environment",
                        customEnvironment,
                        typeof(GameObject),
                        true
                    );

                customRadius =
                    EditorGUILayout.FloatField(
                        "Grid Radius",
                        customRadius
                    );

                customRadius = Mathf.Max(
                    0.1f,
                    customRadius
                );

                if (customEnvironment == null)
                {
                    EditorGUILayout.HelpBox(
                        "Assign an Environment GameObject.",
                        MessageType.Info
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"Environment: " +
                        $"{customEnvironment.name}\n" +
                        $"Grid Radius: " +
                        $"{customRadius}m\n" +
                        $"Area: " +
                        $"{customRadius * 2.0f}m x " +
                        $"{customRadius * 2.0f}m",
                        MessageType.None
                    );
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Scene mode scans the selected floor object.",
                    MessageType.Info
                );
            }

            GUILayout.Space(6);

            targetGridData =
                (YankiGridData)EditorGUILayout.ObjectField(
                    "Yanki Grid Data Asset",
                    targetGridData,
                    typeof(YankiGridData),
                    false
                );

            GUILayout.Space(8);

            if (environmentSource ==
                EnvironmentSource.Scene)
            {
                if (Selection.activeGameObject == null)
                {
                    EditorGUILayout.HelpBox(
                        "Select the floor object in the scene.",
                        MessageType.Info
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"Selected Floor: " +
                        $"{Selection.activeGameObject.name}",
                        MessageType.None
                    );
                }
            }

            GUILayout.Space(4);

            if (GUILayout.Button(
                "Recognize Environment & Generate GUIDs",
                GUILayout.Height(28)))
            {
                RecognizeEnvironmentAndGenerate();
            }

            if (GUILayout.Button(
                "Save to YankiGridData",
                GUILayout.Height(24)))
            {
                SaveToDataAsset();
            }

            GUILayout.Space(6);

            drawGizmos =
                EditorGUILayout.Toggle(
                    "Draw Cell Gizmos",
                    drawGizmos
                );

            drawObstacleGizmos =
                EditorGUILayout.Toggle(
                    "Draw Obstacle Gizmos",
                    drawObstacleGizmos
                );

            gizmoDrawDistance =
                EditorGUILayout.FloatField(
                    "Gizmo Draw Distance",
                    gizmoDrawDistance
                );

            gizmoDrawDistance = Mathf.Max(
                1.0f,
                gizmoDrawDistance
            );

            showDataPositions =
                EditorGUILayout.Toggle(
                    "Show Data Positions",
                    showDataPositions
                );

            if (generatedCells.Count > 0)
            {
                GUILayout.Label(
                    $"Generated Cells: " +
                    $"{generatedCells.Count}",
                    EditorStyles.miniLabel
                );
            }

            if (obstacleBounds.Count > 0)
            {
                GUILayout.Label(
                    $"Detected Obstacles: " +
                    $"{obstacleBounds.Count}",
                    EditorStyles.miniLabel
                );
            }
        }

        private void RecognizeEnvironmentAndGenerate()
        {
            Bounds bounds;
            Transform environmentRoot;

            if (environmentSource ==
                EnvironmentSource.Scene)
            {
                GameObject selectedObj =
                    Selection.activeGameObject;

                if (selectedObj == null)
                {
                    EditorUtility.DisplayDialog(
                        "Warning",
                        "Select a floor object in the scene.",
                        "OK"
                    );

                    return;
                }

                bounds =
                    GetObjectBounds(
                        selectedObj
                    );

                environmentRoot =
                    selectedObj.transform;

                if (bounds.size == Vector3.zero)
                {
                    EditorUtility.DisplayDialog(
                        "Error",
                        "The selected object has no valid " +
                        "Renderer or Collider.",
                        "OK"
                    );

                    return;
                }
            }
            else
            {
                if (customEnvironment == null)
                {
                    EditorUtility.DisplayDialog(
                        "Warning",
                        "Assign an Environment GameObject.",
                        "OK"
                    );

                    return;
                }

                Vector3 center =
                    customEnvironment.transform.position;

                bounds = new Bounds(
                    center,
                    new Vector3(
                        customRadius * 2.0f,
                        1000.0f,
                        customRadius * 2.0f
                    )
                );

                environmentRoot =
                    customEnvironment.transform;
            }

            generatedCells.Clear();
            obstacleBounds.Clear();

            int cols =
                Mathf.CeilToInt(
                    bounds.size.x / cellSize
                );

            int rows =
                Mathf.CeilToInt(
                    bounds.size.z / cellSize
                );

            Vector3 startPos =
                bounds.min;

            float raycastStartHeight =
                bounds.max.y + 2.0f;

            float raycastDistance =
                bounds.size.y + 4.0f;

            int chunkSize = 50;

            int totalSteps =
                Mathf.CeilToInt(
                    (float)cols / chunkSize
                ) *
                Mathf.CeilToInt(
                    (float)rows / chunkSize
                );

            int currentStep = 0;

            try
            {
                for (
                    int xChunk = 0;
                    xChunk < cols;
                    xChunk += chunkSize)
                {
                    for (
                        int zChunk = 0;
                        zChunk < rows;
                        zChunk += chunkSize)
                    {
                        currentStep++;

                        float progress =
                            (float)currentStep /
                            totalSteps;

                        EditorUtility.DisplayProgressBar(
                            "Yanki Grid Recognizer",
                            $"Scanning area... " +
                            $"({currentStep}/{totalSteps})",
                            progress
                        );

                        int xMax =
                            Mathf.Min(
                                xChunk + chunkSize,
                                cols
                            );

                        int zMax =
                            Mathf.Min(
                                zChunk + chunkSize,
                                rows
                            );

                        for (
                            int x = xChunk;
                            x < xMax;
                            x++)
                        {
                            for (
                                int z = zChunk;
                                z < zMax;
                                z++)
                            {
                                float sampleX =
                                    startPos.x +
                                    (x * cellSize) +
                                    (cellSize * 0.5f);

                                float sampleZ =
                                    startPos.z +
                                    (z * cellSize) +
                                    (cellSize * 0.5f);

                                Vector3 rayOrigin =
                                    new Vector3(
                                        sampleX,
                                        raycastStartHeight,
                                        sampleZ
                                    );

                                if (!Physics.Raycast(
                                    rayOrigin,
                                    Vector3.down,
                                    out RaycastHit hit,
                                    raycastDistance,
                                    groundLayer))
                                {
                                    continue;
                                }

                                if (!IsPartOfEnvironment(
                                    hit.collider.gameObject,
                                    environmentRoot))
                                {
                                    continue;
                                }

                                YankiCellData cell =
                                    new YankiCellData
                                    {
                                        guid =
                                            Guid.NewGuid().ToString(),

                                        position =
                                            hit.point +
                                            Vector3.up * 0.02f,

                                        occlusion = 0.0f,

                                        lowPassCutoff =
                                            22000f,

                                        reverbLevel =
                                            0.0f
                                    };

                                generatedCells.Add(
                                    cell
                                );
                            }
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            SceneView.RepaintAll();

            Debug.Log(
                $"[Yanki XR] Scan complete. " +
                $"Cells: {generatedCells.Count}, " +
                $"Obstacles: {obstacleBounds.Count}"
            );
        }

        private bool IsPartOfEnvironment(
            GameObject hitObject,
            Transform environmentRoot)
        {
            if (hitObject == null ||
                environmentRoot == null)
            {
                return false;
            }

            Transform hitTransform =
                hitObject.transform;

            return
                hitTransform == environmentRoot ||
                hitTransform.IsChildOf(
                    environmentRoot
                );
        }

        private Bounds GetObjectBounds(
            GameObject obj)
        {
            Renderer[] renderers =
                obj.GetComponentsInChildren<Renderer>(
                    true
                );

            Collider[] colliders =
                obj.GetComponentsInChildren<Collider>(
                    true
                );

            Bounds bounds =
                new Bounds();

            bool initialized = false;

            foreach (
                Renderer renderer
                in renderers)
            {
                if (!initialized)
                {
                    bounds =
                        renderer.bounds;

                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds
                    );
                }
            }

            foreach (
                Collider collider
                in colliders)
            {
                if (!initialized)
                {
                    bounds =
                        collider.bounds;

                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(
                        collider.bounds
                    );
                }
            }

            if (!initialized)
            {
                return new Bounds(
                    obj.transform.position,
                    Vector3.zero
                );
            }

            return bounds;
        }

        private LayerMask LayerMaskField(
            string label,
            LayerMask layerMask)
        {
            var layers =
                UnityEditorInternal
                    .InternalEditorUtility
                    .layers;

            var layerNumbers =
                new List<int>();

            for (
                int i = 0;
                i < layers.Length;
                i++)
            {
                layerNumbers.Add(
                    LayerMask.NameToLayer(
                        layers[i]
                    )
                );
            }

            int maskWithoutEmpty = 0;

            for (
                int i = 0;
                i < layerNumbers.Count;
                i++)
            {
                if (
                    ((1 << layerNumbers[i]) &
                    layerMask.value) != 0)
                {
                    maskWithoutEmpty |=
                        (1 << i);
                }
            }

            maskWithoutEmpty =
                EditorGUILayout.MaskField(
                    label,
                    maskWithoutEmpty,
                    layers
                );

            int mask = 0;

            for (
                int i = 0;
                i < layerNumbers.Count;
                i++)
            {
                if (
                    (maskWithoutEmpty &
                    (1 << i)) != 0)
                {
                    mask |=
                        (1 << layerNumbers[i]);
                }
            }

            return mask;
        }

        private void SaveToDataAsset()
        {
            if (targetGridData == null)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    "Drag a valid YankiGridData " +
                    "ScriptableObject into the asset field.",
                    "OK"
                );

                return;
            }

            targetGridData.cellSize =
                cellSize;

            targetGridData.serializedCells =
                new List<YankiCellData>(
                    generatedCells
                );

            EditorUtility.SetDirty(
                targetGridData
            );

            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Yanki XR] Saved " +
                $"{generatedCells.Count} cells."
            );
        }

        private void OnSceneGUI(
            SceneView sceneView)
        {
            if (Camera.current == null)
                return;

            Vector3 cameraPos =
                Camera.current.transform.position;

            if (drawGizmos)
            {
                Handles.color =
                    new Color(
                        0,
                        1,
                        1,
                        0.8f
                    );

                foreach (
                    var cell
                    in generatedCells)
                {
                    if (
                        Vector3.Distance(
                            cameraPos,
                            cell.position
                        ) <= gizmoDrawDistance)
                    {
                        Handles.DrawWireCube(
                            cell.position,
                            new Vector3(
                                cellSize,
                                0.05f,
                                cellSize
                            )
                        );
                    }
                }
            }

            if (drawObstacleGizmos)
            {
                Handles.color =
                    new Color(
                        1,
                        0,
                        0,
                        0.9f
                    );

                foreach (
                    var obsBound
                    in obstacleBounds)
                {
                    if (
                        Vector3.Distance(
                            cameraPos,
                            obsBound.center
                        ) <=
                        gizmoDrawDistance +
                        obsBound.extents.magnitude)
                    {
                        Handles.DrawWireCube(
                            obsBound.center,
                            obsBound.size
                        );
                    }
                }
            }

            if (
                showDataPositions &&
                targetGridData != null &&
                targetGridData.serializedCells != null)
            {
                foreach (
                    var cell
                    in targetGridData.serializedCells)
                {
                    if (
                        Vector3.Distance(
                            cameraPos,
                            cell.position
                        ) <= gizmoDrawDistance)
                    {
                        Handles.DrawWireCube(
                            cell.position,
                            new Vector3(
                                targetGridData.cellSize,
                                0.05f,
                                targetGridData.cellSize
                            )
                        );
                    }
                }
            }

            DrawCustomRadiusGizmo();
        }

        private void DrawCustomRadiusGizmo()
        {
            if (
                environmentSource !=
                EnvironmentSource.Custom ||
                customEnvironment == null)
            {
                return;
            }

            Vector3 center =
                customEnvironment.transform.position;

            float size =
                customRadius * 2.0f;

            Handles.color =
                new Color(
                    1,
                    1,
                    0,
                    0.8f
                );

            Handles.DrawWireCube(
                center,
                new Vector3(
                    size,
                    0.1f,
                    size
                )
            );

            Handles.Label(
                center + Vector3.up * 0.5f,
                $"YankiXR Radius: {customRadius}m"
            );
        }
    }
}