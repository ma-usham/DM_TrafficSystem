using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    public class Editor_DMWindow : EditorWindow
    {
        //---------NAVIGATION VARIABLES---------
        public Stack<IPage> pageStack = new Stack<IPage>();

        //--SHARED DATA VARIABLES---------
        public static Editor_DMWindow editorWindow;


        #region Initialization

        [MenuItem("Tools/DarkMatter Traffic System Tool", false, 2)]
        public static void ShowWindow()
        {
            Editor_DMWindow window = (Editor_DMWindow)GetWindow(typeof(Editor_DMWindow));
            window.minSize = new Vector2(300, 200);
            window.titleContent.text = "DarkMatter Traffic System";
            window.Show();
        }

        private void OnEnable()
        {
            Debug.LogWarning("Opening Main Page");
            pageStack.Clear();
            pageStack.Push((IPage)new MainPage());
        }

        #endregion

        private void OnGUI()
        {
            if(pageStack.Count > 0)
            {
                pageStack.Peek().OnGUI(this); // Draw the current page
                pageStack.Peek().OnSceneGUI(SceneView.lastActiveSceneView, this); // Draw the current page's scene GUI
            }
        }
    }
}
