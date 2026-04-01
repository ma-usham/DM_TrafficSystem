using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Defines the UI and scene callbacks each traffic system editor page must implement.
    /// </summary>
    public interface IPage
    {
        /// <summary>
        /// Draws the page content inside the traffic system editor window.
        /// </summary>
        void OnGUI(DMTS_Window ctx);

        /// <summary>
        /// Draws scene view overlays and handles scene interaction for the page.
        /// </summary>
        void OnSceneGUI(SceneView sceneView, DMTS_Window ctx);
    }
}
