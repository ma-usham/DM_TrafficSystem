using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    [CustomEditor(typeof(AILane))]
    public class AILaneEditor : UnityEditor.Editor
    {
    //     [DrawGizmo(GizmoType.Selected)]
    //     private static void DrawSelectedLaneGizmos(AILane lane, GizmoType gizmoType)
    //     {
    //         if (lane == null)
    //             return;

    //         var waypoints = lane.waypoints;
    //         if (waypoints == null || waypoints.Count == 0)
    //             return;

    //         Color previousColor = Handles.color;
    //         Vector3 previousWaypointPosition = default;
    //         bool hasPreviousWaypoint = false;

    //         for (int waypointIndex = 0; waypointIndex < waypoints.Count; waypointIndex++)
    //         {
    //             AIWaypoint waypoint = waypoints[waypointIndex];
    //             if (waypoint == null)
    //                 continue;

    //             Vector3 waypointPosition = waypoint.transform.position;
    //             float waypointSize =
    //                 HandleUtility.GetHandleSize(waypointPosition) * DMTSPrefs.WaypointSizeMultiplier;

    //             Vector3 forward = GetWaypointForward(waypoints, waypointIndex, waypointPosition, previousWaypointPosition, hasPreviousWaypoint);

    //             Handles.color = DMTSPrefs.WaypointColor;
    //             DrawDirectionArrow(waypointPosition, forward, waypointSize);

    //             if (hasPreviousWaypoint)
    //             {
    //                 Handles.color = DMTSPrefs.WaypointLineColor;
    //                 Handles.DrawLine(previousWaypointPosition, waypointPosition);
    //             }

    //             previousWaypointPosition = waypointPosition;
    //             hasPreviousWaypoint = true;
    //         }

    //         Handles.color = previousColor;
    //     }

    //     private static Vector3 GetWaypointForward(
    //         System.Collections.Generic.IReadOnlyList<AIWaypoint> waypoints,
    //         int waypointIndex,
    //         Vector3 waypointPosition,
    //         Vector3 previousWaypointPosition,
    //         bool hasPreviousWaypoint)
    //     {
    //         if (waypointIndex < waypoints.Count - 1 && waypoints[waypointIndex + 1] != null)
    //             return (waypoints[waypointIndex + 1].transform.position - waypointPosition).normalized;

    //         if (hasPreviousWaypoint)
    //             return (waypointPosition - previousWaypointPosition).normalized;

    //         return Vector3.forward;
    //     }

    //     private static void DrawDirectionArrow(Vector3 position, Vector3 forward, float size)
    //     {
    //         Vector3 right = Vector3.Cross(Vector3.up, forward);
    //         if (right.sqrMagnitude < 0.001f)
    //             right = Vector3.Cross(Vector3.forward, forward);
    //         if (right.sqrMagnitude < 0.001f)
    //             right = Vector3.right;
    //         right.Normalize();

    //         float shaftLength = size * 1.3f;
    //         float headLength = size * 0.55f;
    //         float headWidth = size * 0.4f;

    //         Vector3 tail = position - forward * shaftLength * 0.5f;
    //         Vector3 tip = position + forward * shaftLength * 0.5f;
    //         Vector3 headBase = tip - forward * headLength;

    //         Handles.DrawLine(tail, tip);
    //         Handles.DrawLine(tip, headBase + right * headWidth);
    //         Handles.DrawLine(tip, headBase - right * headWidth);
    //     }
     }
}
