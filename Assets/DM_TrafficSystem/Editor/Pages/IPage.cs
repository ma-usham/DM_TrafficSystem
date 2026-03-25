using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    public interface IPage
    {
        void OnGUI(Editor_DMWindow ctx);
        void OnSceneGUI(SceneView sceneView, Editor_DMWindow ctx);
    
    }
}
