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
        private string[] _tabs = new string[] { "Driver Behaviour", "Suspension & Sensors" };

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
        private SerializedProperty showDebugStatsProp;
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
            showDebugStatsProp = serializedObject.FindProperty("showDebugStats");
            spawnPaddingProp = serializedObject.FindProperty("spawnPadding");
            bodyTransformProp = serializedObject.FindProperty("bodyTransform");
            tiltAmountProp = serializedObject.FindProperty("tiltAmount");
            smoothProp = serializedObject.FindProperty("smooth");
        }

        private void OnSceneGUI()
        {
            AIVehicle vehicle = (AIVehicle)target;

            DrawWheelHandles(vehicle);
            DrawSensorHandle(vehicle, vehicle.frontSensor, new Color(1.0f, 0.6f, 0.0f, 1.0f), "Change Front Sensor Bounds", null, false, true, false);
            
            // Draw left and right sensors with identical colors (Magenta) and pass mirror reference
            DrawSensorHandle(vehicle, vehicle.leftSensor, Color.magenta, "Change Left Sensor Bounds", vehicle.rightSensor, true, false, true);
            DrawSensorHandle(vehicle, vehicle.rightSensor, Color.magenta, "Change Right Sensor Bounds", vehicle.leftSensor, true, false, true);

            DrawStoppingDistanceHandle(vehicle);
            DrawSpawnPaddingHandle(vehicle);
        }

        private void DrawSensorHandle(AIVehicle vehicle, Transform sensorT, Color drawColor, string undoMessage, Transform mirrorSensorT = null, bool mirrorInvertX = false, bool forceSymmetricX = false, bool forceSymmetricZ = false)
        {
            if (sensorT == null) return;

            Vector3 originalCenter = sensorT.localPosition;
            Vector3 originalSize = sensorT.localScale;

            // Set the handle's current data from the sensor's transform properties
            _sensorBoundsHandle.center = originalCenter;
            _sensorBoundsHandle.size = originalSize;

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

                Vector3 newCenter = _sensorBoundsHandle.center;
                Vector3 newSize = _sensorBoundsHandle.size;

                // Enforce symmetric scaling around the center if requested
                if (forceSymmetricX)
                {
                    if (Mathf.Abs(newSize.x - originalSize.x) > 0.001f)
                    {
                        newSize.x = 2f * newSize.x - originalSize.x;
                        newCenter.x = originalCenter.x;
                    }
                }
                if (forceSymmetricZ)
                {
                    if (Mathf.Abs(newSize.z - originalSize.z) > 0.001f)
                    {
                        newSize.z = 2f * newSize.z - originalSize.z;
                        newCenter.z = originalCenter.z;
                    }
                }

                // Apply changes from the handle back to the sensor transform
                sensorT.localPosition = newCenter;
                
                // Keep scale positive
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
                        Vector3 mirroredCenter = newCenter;
                        mirroredCenter.x = -mirroredCenter.x;
                        mirrorSensorT.localPosition = mirroredCenter;
                    }
                }

                SceneView.RepaintAll();
            }
        }

        private void DrawStoppingDistanceHandle(AIVehicle vehicle)
        {
            if (vehicle.frontSensor == null) return;

            // Calculate the world position of the tip of the stopping distance box
            Vector3 worldOrigin = vehicle.transform.position + vehicle.transform.rotation * vehicle.frontSensor.localPosition;
            float zStart = -vehicle.frontSensor.localScale.z * 0.5f;
            float currentDist = vehicle.driverBehaviour.stoppingDistance;

            Vector3 handleLocalPos = new Vector3(0, 0, zStart + currentDist);
            Vector3 handleWorldPos = worldOrigin + vehicle.transform.rotation * handleLocalPos;

            Handles.color = Color.red;
            EditorGUI.BeginChangeCheck();

            float handleSize = HandleUtility.GetHandleSize(handleWorldPos) * 0.03f;
            Vector3 newWorldPos = Handles.Slider(handleWorldPos, vehicle.transform.forward, handleSize, Handles.DotHandleCap, 0f);

            if (EditorGUI.EndChangeCheck())
            {
                float delta = Vector3.Dot(newWorldPos - handleWorldPos, vehicle.transform.forward);
                float newDist = Mathf.Max(0.1f, currentDist + delta);

                // Modify through SerializedProperty to correctly support prefabs and Undo
                serializedObject.Update();
                SerializedProperty stopDistProp = serializedObject.FindProperty("driverBehaviour.stoppingDistance");
                if (stopDistProp != null)
                {
                    stopDistProp.floatValue = newDist;
                    serializedObject.ApplyModifiedProperties();
                }
                
                SceneView.RepaintAll();
            }

            GUIStyle labelStyle = new GUIStyle { fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = Color.red;
            Handles.Label(handleWorldPos + vehicle.transform.up * 0.5f, $"Stop Dist: {vehicle.driverBehaviour.stoppingDistance:F1}m", labelStyle);
        }

        private void DrawSpawnPaddingHandle(AIVehicle vehicle)
        {
            Vector3 extents = vehicle.GetSpawnBoxHalfExtents();
            Vector3 centerOffset = vehicle.GetSpawnBoxCenterOffset();

            Handles.color = Color.cyan;
            float deltaPadding = 0f;
            bool isChanged = false;

            // Helper function to draw a slider handle for a specific side
            void DrawDirectionalHandle(Vector3 localDir, Vector3 worldDir)
            {
                Vector3 localHandlePos = centerOffset + Vector3.Scale(extents, localDir);
                Vector3 handleWorldPos = vehicle.transform.position + vehicle.transform.rotation * localHandlePos;

                EditorGUI.BeginChangeCheck();
                float handleSize = HandleUtility.GetHandleSize(handleWorldPos) * 0.03f;
                Vector3 newWorldPos = Handles.Slider(handleWorldPos, worldDir, handleSize, Handles.DotHandleCap, 0f);

                if (EditorGUI.EndChangeCheck())
                {
                    deltaPadding = Vector3.Dot(newWorldPos - handleWorldPos, worldDir);
                    isChanged = true;
                }
            }

            // Draw the 4 side handles
            DrawDirectionalHandle(Vector3.right, vehicle.transform.right);
            DrawDirectionalHandle(Vector3.left, -vehicle.transform.right);
            DrawDirectionalHandle(Vector3.forward, vehicle.transform.forward);
            DrawDirectionalHandle(Vector3.back, -vehicle.transform.forward);

            // Apply changes if any handle was dragged
            if (isChanged)
            {
                float newPadding = Mathf.Max(0f, vehicle.spawnPadding + deltaPadding);

                // Modify through SerializedProperty to correctly support prefabs and Undo
                serializedObject.Update();
                SerializedProperty paddingProp = serializedObject.FindProperty("spawnPadding");
                if (paddingProp != null)
                {
                    paddingProp.floatValue = newPadding;
                    serializedObject.ApplyModifiedProperties();
                }
                
                SceneView.RepaintAll();
            }

            // Draw the label next to the right handle
            Vector3 labelLocalPos = centerOffset + new Vector3(extents.x, 0, 0);
            Vector3 labelWorldPos = vehicle.transform.position + vehicle.transform.rotation * labelLocalPos;
            GUIStyle labelStyle = new GUIStyle { fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = Color.cyan;
            Handles.Label(labelWorldPos + vehicle.transform.up * 0.5f, $"Spawn Padding: {vehicle.spawnPadding:F1}m", labelStyle);
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

            EditorGUILayout.LabelField("Fake Physics & Body Tilt", EditorStyles.boldLabel);
            if (bodyTransformProp != null) EditorGUILayout.PropertyField(bodyTransformProp);
            if (tiltAmountProp != null) EditorGUILayout.PropertyField(tiltAmountProp);
            if (smoothProp != null) EditorGUILayout.PropertyField(smoothProp);

            GUILayout.Space(10);
            if (showDebugStatsProp != null) EditorGUILayout.PropertyField(showDebugStatsProp);
            if (debugDataProp != null) EditorGUILayout.PropertyField(debugDataProp, true);
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
            if (sensorsRoot == null)
            {
                GameObject sensorsObj = new GameObject("Sensors");
                sensorsObj.transform.SetParent(vehicle.transform);
                sensorsObj.transform.localPosition = Vector3.zero;
                sensorsObj.transform.localRotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(sensorsObj, "Create Sensors Root");
                sensorsRoot = sensorsObj.transform;
                Debug.Log("Auto Setup: Created 'Sensors' root object.");
            }

            Transform frontSensorT = sensorsRoot.Find("FrontSensor");
            if (frontSensorT == null)
            {
                GameObject fsObj = new GameObject("FrontSensor");
                fsObj.transform.SetParent(sensorsRoot);
                fsObj.transform.localPosition = new Vector3(0f, 0.5f, 2.5f);
                fsObj.transform.localRotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(fsObj, "Create FrontSensor");
                frontSensorT = fsObj.transform;
                Debug.Log("Auto Setup: Created 'FrontSensor'.");
            }
            vehicle.frontSensor = frontSensorT;

            Transform leftSensorT = sensorsRoot.Find("LeftSensor");
            if (leftSensorT == null)
            {
                GameObject lsObj = new GameObject("LeftSensor");
                lsObj.transform.SetParent(sensorsRoot);
                lsObj.transform.localPosition = new Vector3(-1f, 0.5f, 0f);
                lsObj.transform.localRotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(lsObj, "Create LeftSensor");
                leftSensorT = lsObj.transform;
                Debug.Log("Auto Setup: Created 'LeftSensor'.");
            }
            vehicle.leftSensor = leftSensorT;

            Transform rightSensorT = sensorsRoot.Find("RightSensor");
            if (rightSensorT == null)
            {
                GameObject rsObj = new GameObject("RightSensor");
                rsObj.transform.SetParent(sensorsRoot);
                rsObj.transform.localPosition = new Vector3(1f, 0.5f, 0f);
                rsObj.transform.localRotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(rsObj, "Create RightSensor");
                rightSensorT = rsObj.transform;
                Debug.Log("Auto Setup: Created 'RightSensor'.");
            }
            vehicle.rightSensor = rightSensorT;

            // 3. Setup Body
            Transform bodyT = vehicle.transform.Find("Body");
            if (bodyT != null)
            {
                vehicle.bodyTransform = bodyT;
                Debug.Log("Auto Setup: Successfully assigned Body.");
            }
            else
            {
                Debug.LogWarning("Auto Setup: Could not find a child GameObject named 'Body' at the root of the vehicle.");
            }
        }
    }
}