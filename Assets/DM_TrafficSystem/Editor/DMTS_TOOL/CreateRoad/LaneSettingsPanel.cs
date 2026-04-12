using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Draws editable lane properties and applies them back onto generated waypoints.
    /// </summary>
    public static class LaneSettingsPanel
    {
        /// <summary>
        /// Renders the inspector-style controls for one generated lane.
        /// </summary>
        public static void Draw(AILane lane)
        {
            if (lane == null)
                return;

            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.LabelField(lane.gameObject.name, EditorStyles.boldLabel);

            SerializedObject serializedLane = new SerializedObject(lane);
            SerializedProperty waypointsProperty = serializedLane.FindProperty("waypoints");
            SerializedProperty speedLimitProperty = serializedLane.FindProperty("laneSpeedLimit");
            SerializedProperty vehicleTypeProperty = serializedLane.FindProperty("laneVehicleType");

            serializedLane.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(speedLimitProperty, new GUIContent("Speed Limit"));
            EditorGUILayout.PropertyField(vehicleTypeProperty, new GUIContent("Vehicle Type"));

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Change Direction", GUILayout.Width(130)))
            {
                Undo.RecordObject(lane, "Change Lane Direction");
                
                ReverseLaneWaypoints(lane);
                
                EditorUtility.SetDirty(lane);
            }
            if (GUILayout.Button("Apply", GUILayout.Width(100)))
            {
                serializedLane.ApplyModifiedProperties();
                ApplyWaypointSettingsFromLane(lane);
                EditorUtility.SetDirty(lane);
            }
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.LabelField("Waypoint Count", waypointsProperty.arraySize.ToString());
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(waypointsProperty, true);
            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
                serializedLane.ApplyModifiedProperties();
                EditorUtility.SetDirty(lane);
            }

            if (GUILayout.Button("Select Lane Object"))
            {
                Selection.activeGameObject = lane.gameObject;
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Copies lane-level speed and vehicle filters onto every waypoint in the lane.
        /// </summary>
        private static void ApplyWaypointSettingsFromLane(AILane lane)
        {
            if (lane == null || lane.waypoints == null)
                return;

            Undo.RecordObject(lane, "Apply Lane Settings");

            for (int i = 0; i < lane.waypoints.Count; i++)
            {
                AIWaypoint waypoint = lane.waypoints[i];
                if (waypoint == null)
                    continue;

                Undo.RecordObject(waypoint, "Apply Lane Settings");
                WaypointSettings settings = waypoint.settings;
                settings.speed = lane.laneSpeedLimit;
                settings.vehicleType = lane.laneVehicleType;
                waypoint.settings = settings;
                EditorUtility.SetDirty(waypoint);
            }
        }

        /// <summary>
        /// Reverses the physical waypoints of the given lane and updates their orientations to match the new driving direction.
        /// </summary>
        private static void ReverseLaneWaypoints(AILane lane)
        {
            if (lane == null || lane.waypoints == null || lane.waypoints.Count == 0)
                return;

            Undo.RecordObject(lane, "Reverse Lane Direction");

            System.Collections.Generic.HashSet<AIWaypoint> internalWaypoints = 
                new System.Collections.Generic.HashSet<AIWaypoint>(lane.waypoints);

            lane.waypoints.Reverse();

            for (int i = 0; i < lane.waypoints.Count; i++)
            {
                AIWaypoint waypoint = lane.waypoints[i];
                if (waypoint == null) continue;

                Undo.RecordObject(waypoint, "Reverse Lane Direction");
                Undo.RecordObject(waypoint.gameObject, "Reverse Lane Direction");
                Undo.RecordObject(waypoint.transform, "Reverse Lane Direction");

                waypoint.gameObject.name = $"Waypoint_{i}";

                WaypointSettings settings = waypoint.settings;
                
                // Keep only internal links to avoid having backwards connections to other roads
                System.Collections.Generic.List<AIWaypoint> filteredNext = new System.Collections.Generic.List<AIWaypoint>();
                if (settings.nextWaypoint != null)
                {
                    for (int j = 0; j < settings.nextWaypoint.Length; j++)
                    {
                        if (settings.nextWaypoint[j] != null && internalWaypoints.Contains(settings.nextWaypoint[j]))
                            filteredNext.Add(settings.nextWaypoint[j]);
                    }
                }
                
                System.Collections.Generic.List<AIWaypoint> filteredPrev = new System.Collections.Generic.List<AIWaypoint>();
                if (settings.previousWaypoint != null)
                {
                    for (int j = 0; j < settings.previousWaypoint.Length; j++)
                    {
                        if (settings.previousWaypoint[j] != null && internalWaypoints.Contains(settings.previousWaypoint[j]))
                            filteredPrev.Add(settings.previousWaypoint[j]);
                    }
                }

                settings.nextWaypoint = filteredPrev.ToArray();
                settings.previousWaypoint = filteredNext.ToArray();

                // Clear lane change links since changing direction breaks them
                settings.laneChangePoints = new AIWaypoint[0];

                waypoint.settings = settings;

                // Re-align the waypoint's rotation to the new order
                if (i < lane.waypoints.Count - 1)
                {
                    AIWaypoint next = lane.waypoints[i + 1];
                    if (next != null)
                    {
                        Vector3 dir = (next.transform.position - waypoint.transform.position).normalized;
                        if (dir != Vector3.zero)
                            waypoint.transform.rotation = Quaternion.LookRotation(dir);
                    }
                }
                else if (i > 0)
                {
                    AIWaypoint prev = lane.waypoints[i - 1];
                    if (prev != null)
                    {
                        Vector3 dir = (waypoint.transform.position - prev.transform.position).normalized;
                        if (dir != Vector3.zero)
                            waypoint.transform.rotation = Quaternion.LookRotation(dir);
                    }
                }

                EditorUtility.SetDirty(waypoint);
            }

            SceneView.RepaintAll();
        }
    }
}
