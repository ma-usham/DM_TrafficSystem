using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Reserves a custom inspector slot for lane-specific tooling without changing the default inspector today.
    /// Provides Scene View handles to adjust waypoints manually.
    /// </summary>
    [CustomEditor(typeof(AILane))]
    public class AILaneEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            AILane lane = (AILane)target;

            if (lane == null || lane.waypoints == null)
                return;

            Transform handleTransform = lane.transform;

            for (int i = 0; i < lane.waypoints.Count; i++)
            {
                AIWaypoint waypoint = lane.waypoints[i];

                if (waypoint == null)
                    continue;

                Vector3 currentPos = waypoint.transform.position;
                
                EditorGUI.BeginChangeCheck();

                Handles.color = Color.cyan;
                float handleSize = HandleUtility.GetHandleSize(currentPos) * 0.12f;
                
                // Slider2D strictly locks mouse dragging to the X/Z plane, drawing a small sphere handle
                Vector3 newPos = Handles.Slider2D(currentPos, Vector3.up, Vector3.right, Vector3.forward, handleSize, Handles.SphereHandleCap, Vector2.zero);

                if (EditorGUI.EndChangeCheck())
                {
                    //Undo.RecordObject(waypoint.transform, "Move AIWaypoint");

                    // Raycast down to always snap it perfectly on the ground mesh/terrain
                    newPos.y = currentPos.y; 
                    if (Physics.Raycast(newPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f))
                    {
                        newPos.y = hit.point.y;
                    }

                    waypoint.transform.position = newPos;
                    EditorUtility.SetDirty(waypoint.transform);
                }
            }
        }
    }
}
