using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Provides editor controls for locating, creating, and configuring the scene traffic manager.
    /// </summary>
    public class TrafficManagerPage : IPage
    {
        private TrafficManager trafficManager;
        private UnityEditor.Editor editor;
        private Vector2 scrollPos;
        private int activeTab = 1; // 0 = Layer, 1 = Traffic, 2 = Pooling
        private bool drawGridDebug = false;

        /// <summary>
        /// Finds the scene traffic manager and caches the result for repeated UI draws.
        /// </summary>
        private TrafficManager FindTrafficManager()
        {
            if (trafficManager != null)
                return trafficManager;

            trafficManager = Object.FindAnyObjectByType<TrafficManager>();
            return trafficManager;
        }

        /// <summary>
        /// Creates a new traffic manager object and selects it in the hierarchy.
        /// </summary>
        private void CreateTrafficManager()
        {
            GameObject go = new GameObject("TrafficManager");
            trafficManager = go.AddComponent<TrafficManager>();
                //Undo.RegisterCreatedObjectUndo(go, "Create TrafficManager");
            TrafficSystemHierarchyUtility.ParentTrafficManager(go, "Create TrafficManager");
            Selection.activeGameObject = go;
        }

        /// <summary>
        /// Draws traffic manager creation and configuration controls.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            EditorGUILayout.LabelField("AI Traffic Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            TrafficManager manager = FindTrafficManager();

            if (manager == null)
            {
                EditorGUILayout.HelpBox(
                    "No TrafficManager found in the scene. Create one to configure traffic settings.",
                    MessageType.Info);

                EditorGUILayout.Space(4);

                if (GUILayout.Button("Create Traffic Manager", GUILayout.Height(30)))
                {
                    CreateTrafficManager();
                    manager = this.trafficManager;
                }
            }

            if (manager != null)
            {
                EditorGUILayout.Space(4);

                if (GUILayout.Button("Select in Hierarchy", GUILayout.Height(22)))
                    Selection.activeGameObject = manager.gameObject;


                EditorGUILayout.Space(6);

                if (editor == null || editor.target != manager)
                {
                    if (editor != null)
                        Object.DestroyImmediate(editor);
                    editor = UnityEditor.Editor.CreateEditor(manager);
                }

                EditorGUILayout.Space(4);

                // DRAW TABS
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(activeTab == 0, "Layer Setup", "Button", GUILayout.Height(30))) activeTab = 0;
                if (GUILayout.Toggle(activeTab == 1, "Traffic Setting", "Button", GUILayout.Height(30))) activeTab = 1;
                if (GUILayout.Toggle(activeTab == 2, "Pooling System", "Button", GUILayout.Height(30))) activeTab = 2;
                GUILayout.EndHorizontal();

                EditorGUILayout.Space(6);

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                
                SerializedObject serializedManager = editor.serializedObject;
                serializedManager.Update();

                // DRAW SELECTED TAB CONTENT
                if (activeTab == 0) // Layer Setup
                {
                    EditorGUILayout.LabelField("Physics Layers", EditorStyles.boldLabel);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(serializedManager.FindProperty("groundMask"));
                    EditorGUILayout.PropertyField(serializedManager.FindProperty("trafficMask"));
                    EditorGUILayout.PropertyField(serializedManager.FindProperty("playerMask"));
                }
                else if (activeTab == 1) // Traffic Setting
                {
                    EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(serializedManager.FindProperty("maxVehicleCountInGame"));
                    EditorGUILayout.PropertyField(serializedManager.FindProperty("densityControl"));
                    EditorGUILayout.PropertyField(serializedManager.FindProperty("vehicleCollection"));
                }
                else if (activeTab == 2) // Pooling System
                {
                    EditorGUILayout.LabelField("Optimization Rules", EditorStyles.boldLabel);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(serializedManager.FindProperty("usePlayerPooling"));
                    
                    if (serializedManager.FindProperty("usePlayerPooling").boolValue)
                    {
                        EditorGUILayout.Space(6);
                        EditorGUILayout.LabelField("Spatial Values", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedManager.FindProperty("innerSpawnRadius"));
                        EditorGUILayout.PropertyField(serializedManager.FindProperty("outerSpawnRadius"));
                        EditorGUILayout.PropertyField(serializedManager.FindProperty("despawnRadius"));
                        EditorGUILayout.PropertyField(serializedManager.FindProperty("gridSize"));
                        
                        EditorGUILayout.Space(6);
                        EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedManager.FindProperty("mainCamera"));
                        EditorGUILayout.PropertyField(serializedManager.FindProperty("playerTransform"));
                    }
                }

                serializedManager.ApplyModifiedProperties();

                EditorGUILayout.Space(16);

                if (GUILayout.Button("Bake Waypoints & Grid", GUILayout.Height(30)))
                {
                    BakeWaypointsAndGrid(manager);
                }

                EditorGUILayout.Space(6);
                drawGridDebug = EditorGUILayout.Toggle("Draw Grid Cells (Scene)", drawGridDebug);

                EditorGUILayout.EndScrollView();


            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                ctx.pageStack.Pop();
            }
        }

        /// <summary>
        /// This page currently has no scene interaction.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
            if (!drawGridDebug) return;

            TrafficManager manager = FindTrafficManager();
            if (manager == null || manager.serializedGrid == null) return;

            float size = manager.gridSize;

            Handles.color = new Color(0f, 1f, 0f, 0.5f);
            GUIStyle textStyle = new GUIStyle();
            textStyle.normal.textColor = Color.white;
            textStyle.alignment = TextAnchor.MiddleCenter;

            foreach (var cell in manager.serializedGrid)
            {
                // Calculate world center of this cell on the X and Z axes
                float centerX = (cell.cellCoordinate.x * size) + (size * 0.5f);
                float centerZ = (cell.cellCoordinate.y * size) + (size * 0.5f);
                float centerY = 0f;

                // Estimate an average Y height based on the waypoints in this cell
                if (cell.allWaypoints != null && cell.allWaypoints.Count > 0)
                {
                    float ySum = 0f;
                    int validCount = 0;
                    foreach (var wp in cell.allWaypoints)
                    {
                        if (wp != null)
                        {
                            ySum += wp.transform.position.y;
                            validCount++;
                        }
                    }
                    if (validCount > 0) centerY = ySum / validCount;
                }

                Vector3 boxCenter = new Vector3(centerX, centerY, centerZ);
                Vector3 boxSize = new Vector3(size, 2f, size);

                Handles.DrawWireCube(boxCenter, boxSize);
                
                string labelText = $"Cell: {cell.cellCoordinate}\nSpawns: {(cell.spawnWaypoints == null ? 0 : cell.spawnWaypoints.Count)}\nTotal: {(cell.allWaypoints == null ? 0 : cell.allWaypoints.Count)}";
                
                Handles.Label(boxCenter, labelText, textStyle);
            }
        }

        private void BakeWaypointsAndGrid(TrafficManager manager)
        {
            float angleThreshold = 10f; // Set a threshold for straightness
            
            AILane[] lanes = Object.FindObjectsByType<AILane>(FindObjectsInactive.Include);
            AIWaypointConnection[] connections = Object.FindObjectsByType<AIWaypointConnection>(FindObjectsInactive.Include);
            
            if ((lanes == null || lanes.Length == 0) && (connections == null || connections.Length == 0))
            {
                Debug.LogWarning("No AILanes or Connections found in the scene to bake waypoints from.");
                return;
            }

            //Undo.RecordObject(manager, "Bake Waypoint Grid");
            Dictionary<Vector2Int, WaypointGridCell> tempGrid = new Dictionary<Vector2Int, WaypointGridCell>();

            // 1. Process Lanes (True = Eligible for Spawning)
            if (lanes != null)
            {
                foreach (var lane in lanes)
                {
                    if (lane == null || lane.waypoints == null) continue;
                    ProcessWaypointList(manager, tempGrid, lane.waypoints, angleThreshold, true);
                }
            }

            // 2. Process Connections (False = Not Eligible for Spawning)
            if (connections != null)
            {
                foreach (var connection in connections)
                {
                    if (connection == null || connection.transitionWaypoints == null) continue;
                    ProcessWaypointList(manager, tempGrid, connection.transitionWaypoints, angleThreshold, false);
                }
            }

            // 3. Save
            int totalWaypoints = 0;
            manager.serializedGrid.Clear();
            foreach (var kvp in tempGrid)
            {
                manager.serializedGrid.Add(kvp.Value);
                totalWaypoints += kvp.Value.allWaypoints.Count;
            }

            EditorUtility.SetDirty(manager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

            Debug.Log($"Baked {totalWaypoints} Waypoints into {manager.serializedGrid.Count} Grid Cells.");
        }

        private void ProcessWaypointList(TrafficManager manager, Dictionary<Vector2Int, WaypointGridCell> tempGrid, List<AIWaypoint> waypoints, float angleThreshold, bool canSpawn)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                AIWaypoint current = waypoints[i];
                if (current == null) continue;

                Vector2Int cellPosition = new Vector2Int(
                    Mathf.FloorToInt(current.transform.position.x / manager.gridSize),
                    Mathf.FloorToInt(current.transform.position.z / manager.gridSize)
                );

                if (!tempGrid.ContainsKey(cellPosition))
                {
                    tempGrid[cellPosition] = new WaypointGridCell
                    {
                        cellCoordinate = cellPosition,
                        allWaypoints = new List<AIWaypoint>(),
                        spawnWaypoints = new List<AIWaypoint>()
                    };
                }

                // ALWAYS add to allWaypoints
                tempGrid[cellPosition].allWaypoints.Add(current);

                // ONLY evaluate for spawnWaypoints if permitted
                if (canSpawn && i > 0 && i < waypoints.Count - 1)
                {
                    AIWaypoint prev = waypoints[i - 1];
                    AIWaypoint next = waypoints[i + 1];

                    if (prev != null && next != null)
                    {
                        Vector3 dirIn = (current.transform.position - prev.transform.position).normalized;
                        Vector3 dirOut = (next.transform.position - current.transform.position).normalized;
                        
                        if (Vector3.Angle(dirIn, dirOut) <= angleThreshold)
                        {
                            tempGrid[cellPosition].spawnWaypoints.Add(current); 
                        }
                    }
                }
            }
        }
    }
}
