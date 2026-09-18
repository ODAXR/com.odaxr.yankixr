#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ODAXR.YankiXR.Runtime;

namespace ODAXR.YankiXR.Tool
{
    // Selection owns the preview, independent of Inspector focus or locked Inspectors.
    [InitializeOnLoad]
    internal static class YankiSelectedSourcePreview
    {
        private static YankiBridge owner;
        private static YankiWavePreview preview;

        static YankiSelectedSourcePreview()
        {
            Selection.selectionChanged += RefreshSelection;
            EditorApplication.update += RefreshSelection;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
        }

        internal static YankiWavePreview For(YankiBridge bridge)
        {
            RefreshSelection();
            return owner == bridge ? preview : null;
        }

        private static void RefreshSelection()
        {
            GameObject selected = Selection.activeObject as GameObject;
            YankiBridge next = selected != null && Selection.Contains(selected) && !EditorUtility.IsPersistent(selected)
                ? selected.GetComponent<YankiBridge>() : null;
            if (next != owner || (next != null && preview == null))
            {
                Clear();
                owner = next;
                if (owner != null)
                {
                    preview = ScriptableObject.CreateInstance<YankiWavePreview>();
                    preview.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            if (preview != null && owner != null)
                preview.Configure(owner.PreviewGrid, owner.PreviewSource, owner.gameObject);
            else if (preview != null)
                Clear();
        }

        private static void Clear()
        {
            bool hadPreview = preview != null;
            if (hadPreview) Object.DestroyImmediate(preview);
            preview = null;
            owner = null;
            if (hadPreview)
            {
                SceneView.RepaintAll();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }
    }

    [CustomEditor(typeof(YankiBridge))]
    public sealed class YankiBridgeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var preview = YankiSelectedSourcePreview.For((YankiBridge)target);
            if (preview != null) preview.DrawControls();

        }
    }
}
#endif
