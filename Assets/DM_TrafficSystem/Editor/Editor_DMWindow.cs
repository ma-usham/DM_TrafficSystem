using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public class Editor_DMWindow : EditorWindow
    {
        public Stack<IPage> pageStack = new Stack<IPage>();
        public static Editor_DMWindow editorWindow;

        [MenuItem("Tools/DarkMatter Traffic System Tool", false, 2)]
        public static void ShowWindow(IPage openPage = null)
        {
            Editor_DMWindow window = (Editor_DMWindow)GetWindow(typeof(Editor_DMWindow));
            window.minSize = new Vector2(320, 240);
            window.titleContent.text = "DarkMatter Traffic System";
            window.Show();
            if (openPage != null)
            {
               window.pageStack.Push(openPage);
            }
        }

        private void OnEnable()
        {
            editorWindow = this;
            pageStack.Clear();
            pageStack.Push(new MainPage());
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            CreateRoadPage.isActive = false;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            if (pageStack.Count > 0)
                pageStack.Peek().OnGUI(this);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (pageStack.Count > 0)
            {
                CreateRoadPage.isActive = pageStack.Peek() is CreateRoadPage;
                pageStack.Peek().OnSceneGUI(sceneView, this);
            }
            else
            {
                CreateRoadPage.isActive = false;
            }
        }
    }
}
