using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    [CustomEditor(typeof(Road))]
    public class RoadEditor : UnityEditor.Editor
    {
        private Road road => (Road)target;


        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open in Traffic System Window", GUILayout.Height(24)))
            {
                DMTS_Window.ShowWindow(new CreateRoadPage(road));
            }
            SceneView.RepaintAll();
        }

        private void OnSceneGUI() //This just shows the bezier curve and control points of that road object int he scene.
        {
            if (CreateRoadPage.isActive) return;

            var pts = road.controlPointsList;
            if (pts.Count < 2) return;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i];
                Vector3 b = pts[i + 1];
                SplineMathUtils.GetSegmentHandles(road.controlPointsList, i, out Vector3 h1, out Vector3 h2);
                //Creator.GetSegmentHandles(i, out Vector3 h1, out Vector3 h2);
                Handles.DrawBezier(a, b, h1, h2, DMTSPrefs.CurveColor, null, DMTSPrefs.CurveWidth);
            }

            Handles.color = DMTSPrefs.ControlPointColor;
            for (int i = 0; i < pts.Count; i++)
            {
                Handles.SphereHandleCap(0, pts[i], Quaternion.identity,
                    DMTSPrefs.ControlPointHandleSize * 2f, EventType.Repaint);

            }
        }

        #region Gizmos
        [DrawGizmo(GizmoType.Selected)] //This will only draw the gizmos when the object is selected, which can help reduce clutter in the scene view.
        private static void DrawGeneratedWaypointGizmos(Road creator, GizmoType gizmoType) // This function is automatically called by Unity to draw gizmos in the scene view. It will draw arrows at each waypoint to indicate direction, and lines between waypoints to show the path.
        {
            if (creator.laneObjects == null || creator.laneObjects.Count == 0) return;

            Color previousColor = Handles.color;

            foreach (var lane in creator.laneObjects)
            {
                if (lane == null) continue;
                Transform laneTransform = lane.transform;

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
