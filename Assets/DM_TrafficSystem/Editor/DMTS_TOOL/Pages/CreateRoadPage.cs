using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
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

            if (GUILayout.Button("Back", GUILayout.Width(100)))
            {
                ClosePage(ctx);
            }
        }

        public void OnSceneGUI(SceneView sceneView, DMTS_Window ctx)
        {
            if (TryCloseMissingRoad(ctx))
                return;

            sceneTool.OnSceneGUI(sceneView, ctx);
        }

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

        private string GetTitle()
        {
            return editMode && sceneTool.Road != null
                ? $"Edit Road - {sceneTool.Road.gameObject.name}"
                : "Create Road";
        }

        private void DrawLinkRoadDistanceField()
        {
            if (sceneTool.Road == null)
                return;

            EditorGUILayout.LabelField("Link Offset", GUILayout.Width(80));

            EditorGUI.BeginChangeCheck();
            int linkRoadDistance = EditorGUILayout.IntField(sceneTool.Road.laneChangeLinkRoadDistance, GUILayout.Width(45));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sceneTool.Road, "Change Link Offset");
                sceneTool.Road.laneChangeLinkRoadDistance = Mathf.Max(1, linkRoadDistance);
                EditorUtility.SetDirty(sceneTool.Road);
            }
        }

        private bool HasGeneratedLaneData()
        {
            return sceneTool.Road != null
                && sceneTool.Road.laneObjects != null
                && sceneTool.Road.laneObjects.Count > 1;
        }

        private bool TryCloseMissingRoad(DMTS_Window ctx)
        {
            if (!sceneTool.HasMissingRoad)
                return false;

            ClosePage(ctx);
            return true;
        }

        private void ClosePage(DMTS_Window ctx)
        {
            isActive = false;
            if (ctx.pageStack.Count > 0)
                ctx.pageStack.Pop();
        }
    }
}
