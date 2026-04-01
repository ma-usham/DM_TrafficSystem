using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Hosts the road creation workflow, lane settings, and lane-link actions for a single road.
    /// </summary>
    public class CreateRoadPage : IPage
    {
        public static bool isActive;

        private readonly bool editMode;
        private readonly RoadSceneTool sceneTool;
        private Vector2 scrollPosition;

        public CreateRoadPage()
        {
            sceneTool = new RoadSceneTool();
        }

        public CreateRoadPage(Road existingRoad)
        {
            sceneTool = new RoadSceneTool(existingRoad);
            editMode = existingRoad != null;
            isActive = existingRoad != null;
        }

        /// <summary>
        /// Draws the road editing workflow for the selected or newly created road.
        /// </summary>
        public void OnGUI(DMTS_Window ctx)
        {
            if (TryCloseMissingRoad(ctx))
                return;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(GetTitle(), EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            RoadSettingsPanel.DrawHelpBox();

            EditorGUILayout.Space(6);


            if (sceneTool.Road != null)
            {
                RoadSettingsPanel.DrawSettings(sceneTool.Road);
                EditorGUILayout.Space(4);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
                DrawLaneConfigurations();
                EditorGUILayout.EndScrollView();
            }



            EditorGUILayout.Space(4);
            DrawGenerationControls();

            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                ClosePage(ctx);
            }
        }

        /// <summary>
        /// Forwards scene interaction to the shared road scene tool while this page is active.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
            if (TryCloseMissingRoad(ctx))
                return;

            sceneTool.OnSceneGUI(sceneView, ctx);
        }

        /// <summary>
        /// Draws the generate, link, and unlink controls for the current road.
        /// </summary>
        private void DrawGenerationControls()
        {
            EditorGUI.BeginDisabledGroup(!sceneTool.CanGenerateRoad);
            if (GUILayout.Button("Generate Road", GUILayout.Width(100)))
            {
                RoadBuilder.GenerateRoadWaypoints(sceneTool.Road);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.BeginHorizontal();
            DrawLinkRoadDistanceField();
            EditorGUI.BeginDisabledGroup(!HasGeneratedLaneData());
            if (GUILayout.Button("Link Lanes", GUILayout.Width(100)))
            {
                RoadBuilder.LinkLanes(sceneTool.Road);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Unlink Lanes", GUILayout.Width(100)))
            {
                RoadBuilder.UnlinkLanes(sceneTool.Road);
                SceneView.RepaintAll();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws editable panels for each generated lane on the road.
        /// </summary>
        private void DrawLaneConfigurations()
        {
            if (sceneTool.Road == null
                || sceneTool.Road.laneObjects == null
                || sceneTool.Road.laneObjects.Count == 0)
            {
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Lane Configurations", EditorStyles.boldLabel);

            for (int i = 0; i < sceneTool.Road.laneObjects.Count; i++)
            {
                AILane lane = sceneTool.Road.laneObjects[i];
                if (lane == null)
                    continue;

                LaneSettingsPanel.Draw(lane);
                EditorGUILayout.Space(10);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Returns the page title based on whether an existing road is being edited.
        /// </summary>
        private string GetTitle()
        {
            return editMode && sceneTool.Road != null
                ? $"Edit Road - {sceneTool.Road.gameObject.name}"
                : "Create Road";
        }

        /// <summary>
        /// Draws the lane-change link settings beside the lane-link actions.
        /// </summary>
        private void DrawLinkRoadDistanceField()
        {
            if (sceneTool.Road == null)
                return;

            EditorGUILayout.LabelField("Link Offset", GUILayout.Width(80));

            EditorGUI.BeginChangeCheck();
            int linkOffset = EditorGUILayout.IntField(sceneTool.Road.laneChangeLinkOffset, GUILayout.Width(45));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sceneTool.Road, "Change Link Offset");
                sceneTool.Road.laneChangeLinkOffset = Mathf.Max(1, linkOffset);
                EditorUtility.SetDirty(sceneTool.Road);
            }

            EditorGUILayout.LabelField("Max Turn Angle", GUILayout.Width(95));

            EditorGUI.BeginChangeCheck();
            float maxTurnAngle = EditorGUILayout.FloatField(
                sceneTool.Road.laneChangeMaxTurnAngle > 0f ? sceneTool.Road.laneChangeMaxTurnAngle : 10f,
                GUILayout.Width(45));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sceneTool.Road, "Change Max Lane Change Turn Angle");
                sceneTool.Road.laneChangeMaxTurnAngle = Mathf.Max(1f, maxTurnAngle);
                EditorUtility.SetDirty(sceneTool.Road);
            }
        }

        /// <summary>
        /// Returns whether the current road has enough generated lanes to support lane linking.
        /// </summary>
        private bool HasGeneratedLaneData()
        {
            return sceneTool.Road != null
                && sceneTool.Road.laneObjects != null
                && sceneTool.Road.laneObjects.Count > 1;
        }

        /// <summary>
        /// Closes the page automatically if the backing road object has been deleted.
        /// </summary>
        private bool TryCloseMissingRoad(DMTS_Window ctx)
        {
            if (!sceneTool.HasMissingRoad)
                return false;

            ClosePage(ctx);
            return true;
        }

        /// <summary>
        /// Leaves the create-road workflow and returns to the previous page.
        /// </summary>
        private void ClosePage(DMTS_Window ctx)
        {
            isActive = false;
            if (ctx.pageStack.Count > 0)
                ctx.pageStack.Pop();
        }
    }
}
