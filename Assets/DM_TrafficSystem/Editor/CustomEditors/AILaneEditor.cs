using UnityEditor;

namespace Darkmatter.TrafficSystem.Editor
{
    /// <summary>
    /// Reserves a custom inspector slot for lane-specific tooling without changing the default inspector today.
    /// </summary>
    [CustomEditor(typeof(AILane))]
    public class AILaneEditor : UnityEditor.Editor
    {
    }
}
