using UnityEditor;
using UnityEngine;
using ODAXR.YankiXR.Data;

namespace ODAXR.YankiXR.Tool
{
    internal static class YankiAssetMenu
    {
        [MenuItem("Tools/ODAXR/Yanki/Create Grid Data")]
        private static void CreateGridData()
        {
            ProjectWindowUtil.CreateAsset(
                ScriptableObject.CreateInstance<YankiGridData>(), "NewYankiGridData.asset");
        }
    }
}
