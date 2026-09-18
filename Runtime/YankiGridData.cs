using System;
using System.Collections.Generic;
using UnityEngine;

namespace ODAXR.YankiXR.Data
{
    [System.Serializable]
    public struct YankiCellData
    {
        public string guid;          
        public Vector3 position;     
        
        [Range(0f, 1f)] public float occlusion;      
        public float lowPassCutoff;                  
        public float reverbLevel;                    
    }

    public class YankiGridData : ScriptableObject
    {
        public float cellSize = 1.0f;
        public Vector3 gridOrigin;
        public Vector3Int gridSize;

        // Persist the geometry-query settings so editor previews match this bake.
        [HideInInspector] public int bakedObstacleLayers = ~0;
        [HideInInspector] public float bakedEyeHeight = 0.8f;
        
        public List<YankiCellData> serializedCells = new List<YankiCellData>();

        // Baked Acoustic Matrix (1 Byte / Symmetric)
        [HideInInspector] [SerializeField] private byte[] occlusionMatrix;

        // 3D Voxel Map Baked in the Editor (For Zero Runtime Lookup)
        [HideInInspector] [SerializeField] private int[] spatialLookup;

        public bool IsBaked => occlusionMatrix != null && occlusionMatrix.Length > 0 && spatialLookup != null && spatialLookup.Length > 0;

        public void InitializeMatrix(int count)
        {
            long totalSize = ((long)count * (count + 1)) / 2;
            occlusionMatrix = new byte[totalSize];
        }

        public void SetOcclusion(int fromIndex, int toIndex, float value)
        {
            if (occlusionMatrix == null || occlusionMatrix.Length == 0) return;

            int i = fromIndex < toIndex ? fromIndex : toIndex;
            int j = fromIndex < toIndex ? toIndex : fromIndex;

            int count = serializedCells.Count;
            long index = (long)i * count - ((long)i * (i - 1)) / 2 + (j - i);

            if (index >= 0 && index < occlusionMatrix.Length)
            {
                occlusionMatrix[index] = (byte)(Mathf.Clamp01(value) * 255f);
            }
        }

        public float GetOcclusion(int fromIndex, int toIndex)
        {
            if (!IsBaked) return 0f;

            int count = serializedCells.Count;
            if (fromIndex < 0 || fromIndex >= count || toIndex < 0 || toIndex >= count) return 0f;

            int i = fromIndex < toIndex ? fromIndex : toIndex;
            int j = fromIndex < toIndex ? toIndex : fromIndex;

            long index = (long)i * count - ((long)i * (i - 1)) / 2 + (j - i);

            if (index >= 0 && index < occlusionMatrix.Length)
            {
                return occlusionMatrix[index] / 255f;
            }

            return 0f;
        }

        // --- PURE O(1) MATHEMATICAL INDEX ACCESS (ZERO LOOPS) ---
        public int GetCellIndex(Vector3 worldPosition)
        {
            if (spatialLookup == null || spatialLookup.Length == 0) return -1;

            Vector3 local = worldPosition - gridOrigin;

            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / cellSize), 0, gridSize.x - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(local.y / cellSize), 0, gridSize.y - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / cellSize), 0, gridSize.z - 1);

            int flatIndex = x + y * gridSize.x + z * (gridSize.x * gridSize.y);

            return spatialLookup[flatIndex];
        }

        // --- RUNS DURING THE BAKE STAGE: PRE-MAPS THE ENTIRE SPACE ---
        public void BuildSpatialLookup()
        {
            if (serializedCells == null || serializedCells.Count == 0) return;

            int totalVoxels = gridSize.x * gridSize.y * gridSize.z;
            if (totalVoxels <= 0) return;

            spatialLookup = new int[totalVoxels];
            for (int i = 0; i < totalVoxels; i++) spatialLookup[i] = -1;

            // 1. Place the voxels directly mapped to cells
            for (int i = 0; i < serializedCells.Count; i++)
            {
                Vector3 local = serializedCells[i].position - gridOrigin;
                int x = Mathf.Clamp(Mathf.FloorToInt(local.x / cellSize), 0, gridSize.x - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(local.y / cellSize), 0, gridSize.y - 1);
                int z = Mathf.Clamp(Mathf.FloorToInt(local.z / cellSize), 0, gridSize.z - 1);

                int index = x + y * gridSize.x + z * (gridSize.x * gridSize.y);
                spatialLookup[index] = i;
            }

            // 2. Pre-bake the nearest cells so no voxel remains empty (Voronoi Pre-Bake)
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int z = 0; z < gridSize.z; z++)
                    {
                        int flatIndex = x + y * gridSize.x + z * (gridSize.x * gridSize.y);
                        if (spatialLookup[flatIndex] != -1) continue;

                        Vector3 voxelWorldPos = gridOrigin + new Vector3(
                            (x + 0.5f) * cellSize,
                            (y + 0.5f) * cellSize,
                            (z + 0.5f) * cellSize
                        );

                        int closestCell = 0;
                        float minDist = float.MaxValue;
                        for (int c = 0; c < serializedCells.Count; c++)
                        {
                            float dist = (serializedCells[c].position - voxelWorldPos).sqrMagnitude;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                closestCell = c;
                            }
                        }
                        spatialLookup[flatIndex] = closestCell;
                    }
                }
            }
        }
    }
}