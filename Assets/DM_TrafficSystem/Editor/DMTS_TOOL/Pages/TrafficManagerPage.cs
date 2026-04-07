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
            Undo.RegisterCreatedObjectUndo(go, "Create TrafficManager");
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

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                editor.OnInspectorGUI();

                EditorGUILayout.Space(4);

                if (GUILayout.Button("Bake Spawn Points & Grid", GUILayout.Height(22)))
                {
                    BakeSpawnPoints(manager);
                }
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
        }

        private void BakeSpawnPoints(TrafficManager manager)
        {
            float angleThreshold = 10f; // Set a threshold for straightness
            AILane[] lanes = Object.FindObjectsByType<AILane>(FindObjectsSortMode.None);
            
            if (lanes == null || lanes.Length == 0)
            {
                Debug.LogWarning("No AILanes found in the scene to bake spawn points from.");
                return;
            }

            System.Collections.Generic.List<AIWaypoint> validSpawnPoints = new System.Collections.Generic.List<AIWaypoint>();

            Undo.RecordObject(manager, "Bake Spawn Points");

            foreach (var lane in lanes)
            {
                if (lane == null || lane.waypoints == null || lane.waypoints.Count < 3) continue;

                for (int i = 1; i < lane.waypoints.Count - 1; i++)
                {
                    AIWaypoint prev = lane.waypoints[i - 1];
                    AIWaypoint current = lane.waypoints[i];
                    AIWaypoint next = lane.waypoints[i + 1];

                    if (prev == null || current == null || next == null) continue;

                    Vector3 dirIn = (current.transform.position - prev.transform.position).normalized;
                    Vector3 dirOut = (next.transform.position - current.transform.position).normalized;

                    float angle = Vector3.Angle(dirIn, dirOut);

                    if (angle <= angleThreshold)
                    {
                        validSpawnPoints.Add(current);
                    }
                }
            }

            manager.spawnWaypoints = validSpawnPoints.ToArray();

            // 2. Sort them into a temporary dictionary based on position
            System.Collections.Generic.Dictionary<Vector2Int, System.Collections.Generic.List<AIWaypoint>> tempGrid = new System.Collections.Generic.Dictionary<Vector2Int, System.Collections.Generic.List<AIWaypoint>>();
            
            foreach (var wp in validSpawnPoints)
            {
                Vector2Int cellPosition = new Vector2Int(
                    Mathf.FloorToInt(wp.transform.position.x / manager.gridSize),
                    Mathf.FloorToInt(wp.transform.position.z / manager.gridSize)
                );

                if (!tempGrid.ContainsKey(cellPosition))
                {
                    tempGrid[cellPosition] = new System.Collections.Generic.List<AIWaypoint>();
                }
                tempGrid[cellPosition].Add(wp);
            }

            // 3. Clear the old saved list, and copy the new data into it
            manager.serializedGrid.Clear();
            foreach (var kvp in tempGrid)
            {
                manager.serializedGrid.Add(new WaypointGridCell
                {
                    cellCoordinate = kvp.Key,
                    waypoints = kvp.Value
                });
            }

            EditorUtility.SetDirty(manager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

            Debug.Log($"Baked {validSpawnPoints.Count} safe spawn points from {lanes.Length} AILanes into {manager.serializedGrid.Count} Grid Cells.");
        }
    }
}
