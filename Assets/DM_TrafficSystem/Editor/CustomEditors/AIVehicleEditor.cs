using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

namespace Darkmatter.TrafficSystem
{
    [CustomEditor(typeof(AIVehicle))]
    public class AIVehicleEditor : UnityEditor.Editor
    {
        private BoxBoundsHandle _sensorBoundsHandle = new BoxBoundsHandle();

        private int _currentTab = 0;
        private string[] _tabs = new string[] { "Driver Behaviour", "Suspension & Sensors", "Visuals" };

        private SerializedProperty vehicleTypeProp;
        private SerializedProperty behaviorProp;
        private SerializedProperty wheelsProp;
        private SerializedProperty springStrengthProp;
        private SerializedProperty springDamperProp;
        private SerializedProperty sensorFacesWaypointProp;
        private SerializedProperty frontSensorProp;
        private SerializedProperty leftSensorProp;
        private SerializedProperty rightSensorProp;
        private SerializedProperty debugDataProp;
        private SerializedProperty spawnPaddingProp;
        private SerializedProperty bodyTransformProp;
        private SerializedProperty tiltAmountProp;
        private SerializedProperty smoothProp;

        private void OnEnable()
        {
            vehicleTypeProp = serializedObject.FindProperty("vehicleType");
            behaviorProp = serializedObject.FindProperty("driverBehaviour");
            wheelsProp = serializedObject.FindProperty("wheels");
            springStrengthProp = serializedObject.FindProperty("springStrength");
            springDamperProp = serializedObject.FindProperty("springDamper");
            sensorFacesWaypointProp = serializedObject.FindProperty("sensorFacesWaypoint");
            frontSensorProp = serializedObject.FindProperty("frontSensor");
            leftSensorProp = serializedObject.FindProperty("leftSensor");
            rightSensorProp = serializedObject.FindProperty("rightSensor");
            debugDataProp = serializedObject.FindProperty("debugData");
            spawnPaddingProp = serializedObject.FindProperty("spawnPadding");
            bodyTransformProp = serializedObject.FindProperty("bodyTransform");
            tiltAmountProp = serializedObject.FindProperty("tiltAmount");
            smoothProp = serializedObject.FindProperty("smooth");
        }

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
            serializedObject.Update();

            AIVehicle vehicle = (AIVehicle)target;

            // Draw script reference manually
            SerializedProperty scriptProp = serializedObject.FindProperty("m_Script");
            if (scriptProp != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(scriptProp);
                }
            }

            GUILayout.Space(10);

            // Tab Toolbar
            _currentTab = GUILayout.Toolbar(_currentTab, _tabs, GUILayout.Height(30));
            GUILayout.Space(10);

            if (_currentTab == 0)
            {
                DrawDriverBehaviour();
            }
            else if (_currentTab == 1)
            {
                DrawSuspensionAndSensors();
            }
            else if (_currentTab == 2)
            {
                DrawVisuals();
            }

            serializedObject.ApplyModifiedProperties();

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

        private void DrawDriverBehaviour()
        {
            if (vehicleTypeProp != null) EditorGUILayout.PropertyField(vehicleTypeProp);
            
            GUILayout.Space(10);
            
            if (behaviorProp == null) return;

            // --- Speed & Control ---
            EditorGUILayout.LabelField("Speed & Control", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(behaviorProp.FindPropertyRelative("engineMaxSpeed"));
            
            SerializedProperty speedMultProp = behaviorProp.FindPropertyRelative("speedMultiplierRange");
            Vector2 speedMultVal = speedMultProp.vector2Value;
            
            EditorGUILayout.LabelField(new GUIContent("Speed Multiplier Range", "How much this driver adheres to the waypoint speed limit. (0.7 = 30% under, 1.3 = 30% over)."));
            EditorGUI.indentLevel++;
            float newMin = EditorGUILayout.Slider("Min", speedMultVal.x, 0.7f, 1.0f);
            float newMax = EditorGUILayout.Slider("Max", speedMultVal.y, 1.0f, 1.3f);
            speedMultProp.vector2Value = new Vector2(newMin, newMax);
            EditorGUI.indentLevel--;

            EditorGUILayout.PropertyField(behaviorProp.FindPropertyRelative("acceleration"));
            EditorGUILayout.PropertyField(behaviorProp.FindPropertyRelative("brakingPower"));
            EditorGUILayout.PropertyField(behaviorProp.FindPropertyRelative("turnSpeed"));
            EditorGUILayout.PropertyField(behaviorProp.FindPropertyRelative("stoppingDistance"));

            GUILayout.Space(10);

            // --- Personality ---
            EditorGUILayout.LabelField("Personality", EditorStyles.boldLabel);
            
            SerializedProperty willChangeLaneProp = behaviorProp.FindPropertyRelative("willChangeLane");
            EditorGUILayout.PropertyField(willChangeLaneProp);

            if (willChangeLaneProp != null && willChangeLaneProp.boolValue)
            {
                EditorGUI.indentLevel++;
                
                SerializedProperty fTime = behaviorProp.FindPropertyRelative("frustrationTime");
                EditorGUILayout.LabelField(new GUIContent("Frustration Time", "Min and Max time to follow a slow vehicle before trying to overtake."));
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(fTime.FindPropertyRelative("x"), new GUIContent("Min"));
                EditorGUILayout.PropertyField(fTime.FindPropertyRelative("y"), new GUIContent("Max"));
                EditorGUI.indentLevel--;

                SerializedProperty lCooldown = behaviorProp.FindPropertyRelative("laneChangeCooldown");
                EditorGUILayout.LabelField(new GUIContent("Lane Change Cooldown", "Min and Max cooldown after changing a lane before it can change again."));
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(lCooldown.FindPropertyRelative("x"), new GUIContent("Min"));
                EditorGUILayout.PropertyField(lCooldown.FindPropertyRelative("y"), new GUIContent("Max"));
                EditorGUI.indentLevel--;
                
                EditorGUILayout.PropertyField(behaviorProp.FindPropertyRelative("aiOvertakeProbability"));
                EditorGUI.indentLevel--;
            }
        }

        private void DrawSuspensionAndSensors()
        {
            if (spawnPaddingProp != null)
            {
                EditorGUILayout.PropertyField(spawnPaddingProp);
                GUILayout.Space(10);
            }

            EditorGUILayout.LabelField("Raycast Suspension", EditorStyles.boldLabel);
            if (wheelsProp != null) EditorGUILayout.PropertyField(wheelsProp, true);
            if (springStrengthProp != null) EditorGUILayout.PropertyField(springStrengthProp);
            if (springDamperProp != null) EditorGUILayout.PropertyField(springDamperProp);
            
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Sensors", EditorStyles.boldLabel);
            if (sensorFacesWaypointProp != null) EditorGUILayout.PropertyField(sensorFacesWaypointProp);
            if (frontSensorProp != null) EditorGUILayout.PropertyField(frontSensorProp);
            if (leftSensorProp != null) EditorGUILayout.PropertyField(leftSensorProp);
            if (rightSensorProp != null) EditorGUILayout.PropertyField(rightSensorProp);

            GUILayout.Space(10);
            if (debugDataProp != null) EditorGUILayout.PropertyField(debugDataProp, true);
        }

        private void DrawVisuals()
        {
            EditorGUILayout.LabelField("Fake Physics & Body Tilt", EditorStyles.boldLabel);
            if (bodyTransformProp != null) EditorGUILayout.PropertyField(bodyTransformProp);
            if (tiltAmountProp != null) EditorGUILayout.PropertyField(tiltAmountProp);
            if (smoothProp != null) EditorGUILayout.PropertyField(smoothProp);
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