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

            bool needsApply = serializedLane.hasModifiedProperties || NeedsApplyConfig(lane);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Change Direction", GUILayout.Width(130)))
            {
                Undo.RecordObject(lane, "Change Lane Direction");
                
                ReverseLaneWaypoints(lane);
                
                EditorUtility.SetDirty(lane);
            }
            
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = needsApply ? Color.red : Color.green;
            if (GUILayout.Button("Apply", GUILayout.Width(100)))
            {
                serializedLane.ApplyModifiedProperties();
                ApplyWaypointSettingsFromLane(lane);
                EditorUtility.SetDirty(lane);
            }
            GUI.backgroundColor = originalColor;
            
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

        private static bool NeedsApplyConfig(AILane lane)
        {
            if (lane == null || lane.waypoints == null || lane.waypoints.Count == 0) return false;
            AIWaypoint wp = lane.waypoints[0];
            if (wp == null) return false;

            if (Mathf.Abs(wp.settings.speed - lane.laneSpeedLimit) > 0.001f) return true;

            if (wp.settings.vehicleType == null && lane.laneVehicleType != null) return true;
            if (wp.settings.vehicleType != null && lane.laneVehicleType == null) return true;
            if (wp.settings.vehicleType != null && lane.laneVehicleType != null)
            {
                if (wp.settings.vehicleType.Length != lane.laneVehicleType.Length) return true;
                for (int i = 0; i < wp.settings.vehicleType.Length; i++)
                {
                    if (wp.settings.vehicleType[i] != lane.laneVehicleType[i]) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Reverses the physical waypoints of the given lane and updates their orientations to match the new driving direction.
        /// Removes all incoming and outgoing connections to other lanes to prevent invalid traffic flow.
        /// </summary>
        private static void ReverseLaneWaypoints(AILane lane)
        {
            if (lane == null || lane.waypoints == null || lane.waypoints.Count == 0)
                return;

            Undo.RecordObject(lane, "Reverse Lane Direction");

            System.Collections.Generic.HashSet<AIWaypoint> internalWaypoints = 
                new System.Collections.Generic.HashSet<AIWaypoint>(lane.waypoints);

            // Sever all incoming external connections and delete connection objects
            AIWaypointConnection[] connections = Object.FindObjectsByType<AIWaypointConnection>(FindObjectsInactive.Exclude);
            foreach (var conn in connections)
            {
                if (conn == null) continue;
                if (internalWaypoints.Contains(conn.sourceWaypoint) || internalWaypoints.Contains(conn.targetWaypoint))
                {
                    WaypointConnectionBuilder.DeleteConnection(conn, "Reverse Lane Direction");
                }
            }

            AIWaypoint[] allWaypoints = Object.FindObjectsByType<AIWaypoint>(FindObjectsInactive.Exclude);
            foreach (var extWp in allWaypoints)
            {
                if (extWp == null || internalWaypoints.Contains(extWp)) continue;

                Undo.RecordObject(extWp, "Reverse Lane Direction");
                WaypointSettings extSettings = extWp.settings;
                
                extSettings.nextWaypoint = FilterOutWaypoints(extSettings.nextWaypoint, internalWaypoints);
                extSettings.previousWaypoint = FilterOutWaypoints(extSettings.previousWaypoint, internalWaypoints);
                extSettings.laneChangePoints = FilterOutWaypoints(extSettings.laneChangePoints, internalWaypoints);
                
                extWp.settings = extSettings;
                EditorUtility.SetDirty(extWp);
            }

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

        private static AIWaypoint[] FilterOutWaypoints(AIWaypoint[] existing, System.Collections.Generic.HashSet<AIWaypoint> toRemove)
        {
            if (existing == null || existing.Length == 0) return existing;
            
            System.Collections.Generic.List<AIWaypoint> remaining = new System.Collections.Generic.List<AIWaypoint>();
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && !toRemove.Contains(existing[i]))
                    remaining.Add(existing[i]);
            }
            
            return remaining.ToArray();
        }
    }
}
