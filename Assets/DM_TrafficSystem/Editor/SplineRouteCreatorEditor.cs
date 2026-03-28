using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    [CustomEditor(typeof(SplineRouteCreator))]
    public class SplineRouteCreatorEditor : UnityEditor.Editor
    {
        private SplineRouteCreator Creator => (SplineRouteCreator)target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open in Traffic System Window", GUILayout.Height(24)))
            {
                Editor_DMWindow.ShowWindow();
            }
        }

        private void OnSceneGUI()
        {
            if (CreateRoadPage.isActive) return;

            Creator.CleanupNullPoints();
            var pts = Creator.controlPointsList;
            if (pts.Count < 2) return;

            Handles.color = new Color(1f, 0.85f, 0.1f);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                if (pts[i] == null || pts[i + 1] == null) continue;
                Vector3 a = pts[i].position;
                Vector3 b = pts[i + 1].position;
                Creator.GetSegmentHandles(i, out Vector3 h1, out Vector3 h2);
                Handles.DrawBezier(a, b, h1, h2, new Color(1f, 0.85f, 0.1f), null, 2.5f);
            }

            Handles.color = Color.white;
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i] == null) continue;
                float sz = HandleUtility.GetHandleSize(pts[i].position) * 0.1f;
                Handles.SphereHandleCap(0, pts[i].position, Quaternion.identity,
                    sz * 2f, EventType.Repaint);
            }
        }
    }
}
