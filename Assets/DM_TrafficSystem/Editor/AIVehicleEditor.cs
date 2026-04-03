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

            EditorGUI.BeginChangeCheck();

            // We'll track if any wheel's radius handle is modified
            float updatedRadius = vehicle.wheelRadius;

            // Give the handle a nice color
            Handles.color = Color.cyan;

            for (int i = 0; i < vehicle.wheels.Length; i++)
            {
                if (vehicle.wheels[i] == null) continue;

                // The physics wheel rests at origin - up * restLength
                Vector3 origin = vehicle.wheels[i].position;
                Vector3 wheelRestPosition = origin - vehicle.transform.up * vehicle.suspensionRestLength;

                // Draw a radius handle aligned with the wheel (assuming the wheel faces outward on X/Z)
                // Using transform.rotation so the handle circle lies perpendicular to the car's axes appropriately
                float handleValue = Handles.RadiusHandle(vehicle.transform.rotation, wheelRestPosition, vehicle.wheelRadius);

                // If this specific handle was dragged, update our radius tracker
                if (handleValue != vehicle.wheelRadius)
                {
                    updatedRadius = handleValue;
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                // Register the undo state so Ctrl+Z works in the Editor
                Undo.RecordObject(vehicle, "Change Wheel Radius");

                // Apply the new radius (currently scales all tires uniformly to match)
                vehicle.wheelRadius = updatedRadius;

                // Ensure the scene view repaints to reflect the new size in OnDrawGizmos
                SceneView.RepaintAll();
            }
        }
    }
}