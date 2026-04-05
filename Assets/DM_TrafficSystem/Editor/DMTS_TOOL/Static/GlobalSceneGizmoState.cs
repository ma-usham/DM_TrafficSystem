using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Stores the persistent scene-gizmo toggles owned by the traffic system window.
    /// </summary>
    [System.Serializable]
    public class GlobalSceneGizmoState
    {
        public bool isExpanded = false;
        public bool enabled = true;
        public bool visibleOnly = true;
        public bool drawRoadCurves = true;
        public bool drawControlPoints;
        public bool drawRoadNames=true;
        public bool drawWaypoints = true;
        public bool drawLaneChangeLinks = true;
        public bool drawConnections = true;
        public bool drawIntersectionState = true;
        public bool drawSpawnPoints = true;

        public bool DrawsAnyGizmo =>
            drawRoadCurves
            || drawControlPoints
            || drawRoadNames
            || drawWaypoints
            || drawLaneChangeLinks
            || drawConnections
            || drawIntersectionState
            || drawSpawnPoints;
    }
}
