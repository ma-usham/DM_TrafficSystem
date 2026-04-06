using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

namespace Darkmatter.TrafficSystem
{
    [CustomEditor(typeof(AIVehicle))]
    public class AIVehicleEditor : UnityEditor.Editor
    {
        private BoxBoundsHandle _sensorBoundsHandle = new BoxBoundsHandle();

        private void OnSceneGUI()
        {
            AIVehicle vehicle = (AIVehicle)target;

            DrawWheelHandles(vehicle);
            DrawSensorHandle(vehicle, vehicle.frontSensor, new Color(1.0f, 0.6f, 0.0f, 1.0f), "Change Front Sensor Bounds");
            
            // Draw left and right sensors with identical colors (Magenta) and pass mirror reference
            DrawSensorHandle(vehicle, vehicle.leftSensor, Color.magenta, "Change Left Sensor Bounds", vehicle.rightSensor, true);
            DrawSensorHandle(vehicle, vehicle.rightSensor, Color.magenta, "Change Right Sensor Bounds", vehicle.leftSensor, true);
        }

        private void DrawSensorHandle(AIVehicle vehicle, Transform sensorT, Color drawColor, string undoMessage, Transform mirrorSensorT = null, bool mirrorInvertX = false)
        {
            if (sensorT == null) return;

            // Set the handle's current data from the sensor's transform properties
            _sensorBoundsHandle.center = sensorT.localPosition;
            _sensorBoundsHandle.size = sensorT.localScale;

            EditorGUI.BeginChangeCheck();

            // Align the handle to the main vehicle's space
            Matrix4x4 handleMatrix = Matrix4x4.TRS(vehicle.transform.position, vehicle.transform.rotation, Vector3.one);

            using (new Handles.DrawingScope(handleMatrix))
            {
                // Draw the handle
                _sensorBoundsHandle.SetColor(drawColor);
                _sensorBoundsHandle.DrawHandle();
            }

            if (EditorGUI.EndChangeCheck())
            {
                if (mirrorSensorT != null)
                {
                    Undo.RecordObjects(new Object[] { sensorT, mirrorSensorT }, undoMessage);
                }
                else
                {
                    // Record the sensor's transform for Undo
                    Undo.RecordObject(sensorT, undoMessage);
                }

                // Apply changes from the handle back to the sensor transform
                sensorT.localPosition = _sensorBoundsHandle.center;
                
                // Keep scale positive
                Vector3 newSize = _sensorBoundsHandle.size;
                newSize.x = Mathf.Max(0.01f, newSize.x);
                newSize.y = Mathf.Max(0.01f, newSize.y);
                newSize.z = Mathf.Max(0.01f, newSize.z);
                sensorT.localScale = newSize;

                // Apply mirroring to opposite side sensor
                if (mirrorSensorT != null)
                {
                    mirrorSensorT.localScale = newSize;
                    if (mirrorInvertX)
                    {
                        Vector3 mirroredCenter = _sensorBoundsHandle.center;
                        mirroredCenter.x = -mirroredCenter.x;
                        mirrorSensorT.localPosition = mirroredCenter;
                    }
                }

                SceneView.RepaintAll();
            }
        }

        private void DrawWheelHandles(AIVehicle vehicle)
        {
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

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            AIVehicle vehicle = (AIVehicle)target;

            GUILayout.Space(15);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("Auto Setup Vehicle", GUILayout.Height(35)))
            {
                Undo.RecordObject(vehicle, "Auto Setup Vehicle");
                AutoSetup(vehicle);
                EditorUtility.SetDirty(vehicle);
            }
            GUI.backgroundColor = Color.white;
        }

        private void AutoSetup(AIVehicle vehicle)
        {
            // 1. Setup Wheels
            Transform wheelsRoot = vehicle.transform.Find("Wheels");
            if (wheelsRoot != null)
            {
                System.Collections.Generic.List<SuspensionWheel> newWheels = new System.Collections.Generic.List<SuspensionWheel>();
                int wheelIndex = 0;

                foreach (Transform suspensionT in wheelsRoot)
                {
                    if (suspensionT.childCount == 0)
                    {
                        Debug.LogWarning($"Auto Setup: Wheel suspension '{suspensionT.name}' has no child (visual mesh). Skipping.");
                        continue;
                    }

                    Transform meshT = suspensionT.GetChild(0);

                    SuspensionWheel w = new SuspensionWheel
                    {
                        raycastTransform = suspensionT,
                        visualMesh = meshT,
                        isFrontWheel = (wheelIndex < 2) // First two are front wheels
                    };

                    // Auto calculate radius using MeshRenderer bounds
                    Renderer renderer = meshT.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        w.radius = renderer.bounds.extents.y;
                        if (w.radius < 0.05f) w.radius = 0.35f; // Fallback
                    }
                    else
                    {
                        w.radius = 0.35f; // Fallback
                    }

                    // Distance between raycast point and visual mesh = rest length
                    float calculatedRestLength = Vector3.Distance(suspensionT.position, meshT.position);
                    w.restLength = calculatedRestLength > 0.01f ? calculatedRestLength : 0.5f;

                    newWheels.Add(w);
                    wheelIndex++;
                }

                vehicle.wheels = newWheels.ToArray();
                Debug.Log($"Auto Setup: Successfully assigned {newWheels.Count} wheels.");
            }
            else
            {
                Debug.LogWarning("Auto Setup: Could not find a child GameObject named 'Wheels' at the root of the vehicle.");
            }

            // 2. Setup Sensors
            Transform sensorsRoot = vehicle.transform.Find("Sensors");
            if (sensorsRoot != null)
            {
                Transform frontSensorT = sensorsRoot.Find("FrontSensor");
                if (frontSensorT != null)
                {
                    vehicle.frontSensor = frontSensorT;
                    Debug.Log("Auto Setup: Successfully assigned FrontSensor.");
                }

                Transform leftSensorT = sensorsRoot.Find("LeftSensor");
                if (leftSensorT != null)
                {
                    vehicle.leftSensor = leftSensorT;
                    Debug.Log("Auto Setup: Successfully assigned LeftSensor.");
                }

                Transform rightSensorT = sensorsRoot.Find("RightSensor");
                if (rightSensorT != null)
                {
                    vehicle.rightSensor = rightSensorT;
                    Debug.Log("Auto Setup: Successfully assigned RightSensor.");
                }
            }
            else
            {
                // Fallback check if FrontSensor is just at the root
                Transform frontSensorT = vehicle.transform.Find("FrontSensor");
                if (frontSensorT != null)
                {
                    vehicle.frontSensor = frontSensorT;
                    Debug.Log("Auto Setup: Successfully assigned FrontSensor from root.");
                }
            }
        }
    }
}