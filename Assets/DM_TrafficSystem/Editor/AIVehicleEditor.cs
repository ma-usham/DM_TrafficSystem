using UnityEditor;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    [CustomEditor(typeof(AIVehicle))]
    public class AIVehicleEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            AIVehicle vehicle = (AIVehicle)target;

            if (vehicle.wheels == null) return;

            // Give the handle a nice color
            Handles.color = Color.cyan;

            for (int i = 0; i < vehicle.wheels.Length; i++)
            {
                if (vehicle.wheels[i] == null || vehicle.wheels[i].raycastTransform == null) continue;

                EditorGUI.BeginChangeCheck();

                // The physics wheel rests at origin - up * restLength
                Vector3 origin = vehicle.wheels[i].raycastTransform.position;
                Vector3 wheelRestPosition = origin - vehicle.transform.up * vehicle.wheels[i].restLength;

                // Draw a radius handle aligned with the wheel
                float handleValue = Handles.RadiusHandle(vehicle.transform.rotation, wheelRestPosition, vehicle.wheels[i].radius);

                if (EditorGUI.EndChangeCheck())
                {
                    // Register the undo state so Ctrl+Z works in the Editor
                    Undo.RecordObject(vehicle, $"Change Wheel {i} Radius");

                    // Apply the new radius just to this specific tire
                    vehicle.wheels[i].radius = handleValue;

                    // Ensure the scene view repaints to reflect the new size in OnDrawGizmos
                    SceneView.RepaintAll();
                }
            }
        }
    }
}