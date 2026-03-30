using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    [CustomEditor(typeof(SplineRoadCreator))]
    public class SplineRoadCreatorEditor : UnityEditor.Editor
    {
        private SplineRoadCreator Creator => (SplineRoadCreator)target;


        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open in Traffic System Window", GUILayout.Height(24)))
            {
                Editor_DMWindow.ShowWindow(new CreateRoadPage(Creator));
            }
            SceneView.RepaintAll();
        }

        private void OnSceneGUI() //This just shows the bezier curve and control points of that road object int he scene.
        {
            if (CreateRoadPage.isActive) return;

            Creator.CleanupNullPoints();
            var pts = Creator.controlPointsList;
            if (pts.Count < 2) return;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                if (pts[i] == null || pts[i + 1] == null) continue;
                Vector3 a = pts[i].position;
                Vector3 b = pts[i + 1].position;
                Creator.GetSegmentHandles(i, out Vector3 h1, out Vector3 h2);
                Handles.DrawBezier(a, b, h1, h2, DMTSPrefs.CurveColor, null, DMTSPrefs.CurveWidth);
            }

            Handles.color = DMTSPrefs.ControlPointColor;
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i] == null) continue;
                Handles.SphereHandleCap(0, pts[i].position, Quaternion.identity,
                    DMTSPrefs.ControlPointHandleSize * 2f, EventType.Repaint);

            }
        }







        #region Gizmos
        [DrawGizmo(GizmoType.Selected)] //This will only draw the gizmos when the object is selected, which can help reduce clutter in the scene view.
        private static void DrawGeneratedWaypointGizmos(SplineRoadCreator creator, GizmoType gizmoType) // This function is automatically called by Unity to draw gizmos in the scene view. It will draw arrows at each waypoint to indicate direction, and lines between waypoints to show the path.
        {
            Transform waypointsContainer = creator.waypointsContainer;
            if (waypointsContainer == null) return;

            Color previousColor = Handles.color;

            for (int laneIndex = 0; laneIndex < waypointsContainer.childCount; laneIndex++)
            {
                Transform laneTransform = waypointsContainer.GetChild(laneIndex);
                if (laneTransform == null) continue;

                Vector3 prevWaypointPosition = default;
                bool hasPrevWaypoint = false;

                for (int waypointIndex = 0; waypointIndex < laneTransform.childCount; waypointIndex++)
                {
                    Transform waypointTransform = laneTransform.GetChild(waypointIndex);
                    if (waypointTransform == null) continue;

                    Vector3 waypointPosition = waypointTransform.position;
                    float waypointSize = HandleUtility.GetHandleSize(waypointPosition) *
                                         DMTSPrefs.WaypointSizeMultiplier;

                    Vector3 forward;
                    if (waypointIndex < laneTransform.childCount - 1)
                    {
                        forward = (laneTransform.GetChild(waypointIndex + 1).position - waypointPosition).normalized;
                    }
                    else if (hasPrevWaypoint)
                    {
                        forward = (waypointPosition - prevWaypointPosition).normalized;
                    }
                    else
                    {
                        forward = Vector3.forward;
                    }

                    Handles.color = DMTSPrefs.WaypointColor;
                    DrawDirectionArrow(waypointPosition, forward, waypointSize);

                    if (hasPrevWaypoint)
                    {
                        Handles.color = DMTSPrefs.WaypointLineColor;
                        Handles.DrawLine(prevWaypointPosition, waypointPosition);
                    }

                    prevWaypointPosition = waypointPosition;
                    hasPrevWaypoint = true;
                }
            }

            Handles.color = previousColor;
        }

        private static void DrawDirectionArrow(Vector3 position, Vector3 forward, float size)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(Vector3.forward, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();

            float shaftLength = size * 1.3f;
            float headLength = size * 0.55f;
            float headWidth = size * 0.4f;

            Vector3 tail = position - forward * shaftLength * 0.5f;
            Vector3 tip = position + forward * shaftLength * 0.5f;
            Vector3 headBase = tip - forward * headLength;

            Handles.DrawLine(tail, tip);
            Handles.DrawLine(tip, headBase + right * headWidth);
            Handles.DrawLine(tip, headBase - right * headWidth);
        }

        #endregion

    }
}
