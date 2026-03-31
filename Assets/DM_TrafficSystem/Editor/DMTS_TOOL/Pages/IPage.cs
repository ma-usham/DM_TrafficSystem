using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem.Editor
{
    public interface IPage
    {
        void OnGUI(DMTS_Window ctx);
        void OnSceneGUI(SceneView sceneView, DMTS_Window ctx);
    
    }
}
