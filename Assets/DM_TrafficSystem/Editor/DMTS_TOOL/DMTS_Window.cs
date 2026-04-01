using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Hosts the traffic system editor pages and forwards scene GUI events to the active page.
    /// </summary>
    public class DMTS_Window : EditorWindow
    {
        public Stack<IPage> pageStack = new Stack<IPage>();
        public static DMTS_Window editorWindow;

        /// <summary>
        /// Opens the traffic system window from the Unity Tools menu.
        /// </summary>
        [MenuItem("Tools/DarkMatter Traffic System Tool", false, 2)]
        private static void ShowWindowFromMenu()
        {
            ShowWindow();
        }

        /// <summary>
        /// Opens the window and optionally pushes a specific page on top of the navigation stack.
        /// </summary>
        public static DMTS_Window ShowWindow(IPage openPage = null)
        {
            DMTS_Window window = GetWindow<DMTS_Window>();
            window.minSize = new Vector2(320, 240);
            window.titleContent.text = "DarkMatter Traffic System";
            window.Show();

            if (openPage != null)
                window.pageStack.Push(openPage);

            return window;
        }

        /// <summary>
        /// Initializes the root page and hooks the window into scene GUI updates.
        /// </summary>
        private void OnEnable()
        {
            editorWindow = this;
            pageStack.Clear();
            pageStack.Push(new MainPage());
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>
        /// Resets transient page state and detaches the scene GUI callback when the window closes.
        /// </summary>
        private void OnDisable()
        {
            CreateRoadPage.isActive = false;
            ConnectRoadPage.isActive = false;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        /// <summary>
        /// Draws the currently active page.
        /// </summary>
        private void OnGUI()
        {
            if (pageStack.Count > 0)
                pageStack.Peek().OnGUI(this);
        }

        /// <summary>
        /// Forwards scene GUI handling to the active page and tracks whether create-road mode is active.
        /// </summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            if (pageStack.Count > 0)
            {
                IPage activePage = pageStack.Peek();
                CreateRoadPage.isActive = activePage is CreateRoadPage;
                ConnectRoadPage.isActive = activePage is ConnectRoadPage;
                activePage.OnSceneGUI(sceneView, this);
            }
            else
            {
                CreateRoadPage.isActive = false;
                ConnectRoadPage.isActive = false;
            }
        }
    }
}
