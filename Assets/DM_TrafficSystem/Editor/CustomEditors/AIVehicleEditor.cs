using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

namespace Darkmatter.TrafficSystem
{
    [CustomEditor(typeof(AIVehicle))]
    public class AIVehicleEditor : UnityEditor.Editor
    {
        private BoxBoundsHandle _sensorBoundsHandle = new BoxBoundsHandle();
        private static readonly Color FrontSensorColor = new Color(1f, 0.85f, 0f, 1f);
        private static readonly Color SideSensorColor = new Color(0.78f, 0.35f, 1f, 1f);
        private static readonly Color ExtendedSensorColor = new Color(0.35f, 0.9f, 1f, 1f);
        private static readonly Color StoppingDistanceColor = new Color(1f, 0.25f, 0.25f, 1f);
        private const float DashedLineScreenSize = 4f;
        private const float SolidSensorFillAlpha = 0.05f;
        private const float DashedSensorFillAlpha = 0.035f;

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
        private SerializedProperty maxTiltAngleProp;

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
            maxTiltAngleProp = serializedObject.FindProperty("maxTiltAngle");
        }

        private void OnSceneGUI()
        {
            AIVehicle vehicle = (AIVehicle)target;
            if(Application.isPlaying) return;

            DrawWheelHandles(vehicle);
            DrawSensorHandle(vehicle, vehicle.frontSensor, FrontSensorColor, "Change Front Sensor Bounds", null, false, true, false, vehicle.driverBehaviour.stoppingDistance, true);
            
            // Draw left and right sensors with the side-sensor color and mirror behavior.
            DrawSensorHandle(vehicle, vehicle.leftSensor, SideSensorColor, "Change Left Sensor Bounds", vehicle.rightSensor, true, false, true);
            DrawSensorHandle(vehicle, vehicle.rightSensor, SideSensorColor, "Change Right Sensor Bounds", vehicle.leftSensor, true, false, true);

            DrawStoppingDistanceHandle(vehicle);
            DrawSpawnPaddingHandle(vehicle);
        }

        private void DrawSensorHandle(AIVehicle vehicle, Transform sensorT, Color drawColor, string undoMessage, Transform mirrorSensorT = null, bool mirrorInvertX = false, bool forceSymmetricX = false, bool forceSymmetricZ = false, float minZScale = 0.01f, bool drawExtendedZone = false)
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
                if (Event.current.type == EventType.Repaint)
                {
                    DrawSensorOutline(
                        _sensorBoundsHandle.center,
                        _sensorBoundsHandle.size,
                        drawColor);
                }

                if (drawExtendedZone && Event.current.type == EventType.Repaint)
                {
                    Vector3 extendedSize = new Vector3(_sensorBoundsHandle.size.x, _sensorBoundsHandle.size.y, AIVehicle.ExtendedSensorLength);
                    Vector3 extendedCenter = _sensorBoundsHandle.center;
                    extendedCenter.z += (_sensorBoundsHandle.size.z * 0.5f) + (AIVehicle.ExtendedSensorLength * 0.5f);

                    DrawSensorOutline(
                        extendedCenter,
                        extendedSize,
                        ExtendedSensorColor,
                        Vector3.forward,
                        true);
                }

                // Hide the bounds handle's internal wireframe so only the custom border is visible.
                _sensorBoundsHandle.handleColor = GetHighContrastHandleColor(drawColor);
                _sensorBoundsHandle.wireframeColor = Color.clear;
                Handles.color = Color.white;
                _sensorBoundsHandle.DrawHandle();
            }

            if (EditorGUI.EndChangeCheck())
            {
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

                // Keep scale positive and prevent center drifting when clamped
                if (newSize.x < 0.01f)
                {
                    float diff = 0.01f - newSize.x;
                    newSize.x = 0.01f;
                    if (newCenter.x > originalCenter.x) newCenter.x -= diff / 2f;
                    else if (newCenter.x < originalCenter.x) newCenter.x += diff / 2f;
                }
                
                if (newSize.y < 0.01f)
                {
                    float diff = 0.01f - newSize.y;
                    newSize.y = 0.01f;
                    if (newCenter.y > originalCenter.y) newCenter.y -= diff / 2f;
                    else if (newCenter.y < originalCenter.y) newCenter.y += diff / 2f;
                }
                
                if (newSize.z < minZScale)
                {
                    float diff = minZScale - newSize.z;
                    newSize.z = minZScale;
                    if (newCenter.z > originalCenter.z) newCenter.z -= diff / 2f;
                    else if (newCenter.z < originalCenter.z) newCenter.z += diff / 2f;
                }

                bool syncSensorHeight = Mathf.Abs(newSize.y - originalSize.y) > 0.001f ||
                                        Mathf.Abs(newCenter.y - originalCenter.y) > 0.001f;

                var undoTargets = new System.Collections.Generic.List<Object>();
                AddUniqueTransform(undoTargets, vehicle.frontSensor);
                AddUniqueTransform(undoTargets, vehicle.leftSensor);
                AddUniqueTransform(undoTargets, vehicle.rightSensor);
                Undo.RecordObjects(undoTargets.ToArray(), undoMessage);

                // Apply corrected changes from the handle back to the sensor transform
                sensorT.localPosition = newCenter;
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

                if (syncSensorHeight)
                {
                    SyncAllSensorHeights(vehicle, newCenter.y, newSize.y);
                }

                if (sensorT == vehicle.frontSensor)
                {
                    SyncSideSensorBordersToFront(vehicle);
                }
                else
                {
                    SyncFrontSensorBordersToSides(vehicle);
                }

                SceneView.RepaintAll();
            }
        }

        private static void AddUniqueTransform(System.Collections.Generic.List<Object> targets, Transform transformToAdd)
        {
            if (transformToAdd != null && !targets.Contains(transformToAdd))
            {
                targets.Add(transformToAdd);
            }
        }

        private static void SyncAllSensorHeights(AIVehicle vehicle, float centerY, float sizeY)
        {
            SyncSensorHeight(vehicle.frontSensor, centerY, sizeY);
            SyncSensorHeight(vehicle.leftSensor, centerY, sizeY);
            SyncSensorHeight(vehicle.rightSensor, centerY, sizeY);
        }

        private static void SyncSensorHeight(Transform sensorT, float centerY, float sizeY)
        {
            if (sensorT == null) return;

            Vector3 position = sensorT.localPosition;
            position.y = centerY;
            sensorT.localPosition = position;

            Vector3 scale = sensorT.localScale;
            scale.y = sizeY;
            sensorT.localScale = scale;
        }

        private static void SyncSideSensorBordersToFront(AIVehicle vehicle)
        {
            if (vehicle.frontSensor == null) return;

            float frontLeftBorderX = vehicle.frontSensor.localPosition.x - vehicle.frontSensor.localScale.x * 0.5f;
            float frontRightBorderX = vehicle.frontSensor.localPosition.x + vehicle.frontSensor.localScale.x * 0.5f;

            if (vehicle.leftSensor != null)
            {
                Vector3 leftPosition = vehicle.leftSensor.localPosition;
                leftPosition.x = frontLeftBorderX - vehicle.leftSensor.localScale.x * 0.5f;
                vehicle.leftSensor.localPosition = leftPosition;
            }

            if (vehicle.rightSensor != null)
            {
                Vector3 rightPosition = vehicle.rightSensor.localPosition;
                rightPosition.x = frontRightBorderX + vehicle.rightSensor.localScale.x * 0.5f;
                vehicle.rightSensor.localPosition = rightPosition;
            }
        }

        private static void SyncFrontSensorBordersToSides(AIVehicle vehicle)
        {
            if (vehicle.frontSensor == null) return;

            float? leftBorderX = GetSideInnerBorderX(vehicle.leftSensor, true);
            float? rightBorderX = GetSideInnerBorderX(vehicle.rightSensor, false);

            float frontCenterX = vehicle.frontSensor.localPosition.x;
            float desiredHalfWidth;

            if (leftBorderX.HasValue && rightBorderX.HasValue)
            {
                float leftHalfWidth = Mathf.Abs(frontCenterX - leftBorderX.Value);
                float rightHalfWidth = Mathf.Abs(rightBorderX.Value - frontCenterX);
                desiredHalfWidth = Mathf.Max(leftHalfWidth, rightHalfWidth);
            }
            else if (leftBorderX.HasValue)
            {
                desiredHalfWidth = Mathf.Abs(frontCenterX - leftBorderX.Value);
            }
            else if (rightBorderX.HasValue)
            {
                desiredHalfWidth = Mathf.Abs(rightBorderX.Value - frontCenterX);
            }
            else
            {
                return;
            }

            Vector3 frontScale = vehicle.frontSensor.localScale;
            frontScale.x = Mathf.Max(0.01f, desiredHalfWidth * 2f);
            vehicle.frontSensor.localScale = frontScale;
        }

        private static float? GetSideInnerBorderX(Transform sideSensorT, bool isLeftSensor)
        {
            if (sideSensorT == null) return null;

            float halfWidth = sideSensorT.localScale.x * 0.5f;
            return isLeftSensor
                ? sideSensorT.localPosition.x + halfWidth
                : sideSensorT.localPosition.x - halfWidth;
        }

        private static void DrawSensorOutline(Vector3 center, Vector3 size, Color outlineColor)
        {
            DrawSensorOutline(center, size, outlineColor, GetDominantAxis(size));
        }

        private static void DrawSensorOutline(Vector3 center, Vector3 size, Color outlineColor, Vector3 guideAxis, bool dashed = false)
        {
            DrawSensorFill(center, size, outlineColor, dashed ? DashedSensorFillAlpha : SolidSensorFillAlpha);

            Handles.color = outlineColor;
            if (dashed)
            {
                DrawDashedWireCube(center, size);
            }
            else
            {
                Handles.DrawWireCube(center, size);
            }

            Vector3 normalizedAxis = guideAxis.normalized;
            Vector3 halfAxis = Vector3.Scale(size, normalizedAxis) * 0.5f;
            if (dashed)
            {
                Handles.DrawDottedLine(center - halfAxis, center + halfAxis, DashedLineScreenSize);
            }
            else
            {
                Handles.DrawLine(center - halfAxis, center + halfAxis);
            }
        }

        private static void DrawSensorFill(Vector3 center, Vector3 size, Color baseColor, float alpha)
        {
            Color fillColor = baseColor;
            fillColor.a = alpha;
            Handles.color = fillColor;

            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.matrix = oldMatrix * Matrix4x4.TRS(center, Quaternion.identity, size);
            Handles.CubeHandleCap(0, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
            Handles.matrix = oldMatrix;
        }

        private static void DrawDashedWireCube(Vector3 center, Vector3 size)
        {
            Vector3 extents = size * 0.5f;

            Vector3 c0 = center + new Vector3(-extents.x, -extents.y, -extents.z);
            Vector3 c1 = center + new Vector3(extents.x, -extents.y, -extents.z);
            Vector3 c2 = center + new Vector3(extents.x, extents.y, -extents.z);
            Vector3 c3 = center + new Vector3(-extents.x, extents.y, -extents.z);
            Vector3 c4 = center + new Vector3(-extents.x, -extents.y, extents.z);
            Vector3 c5 = center + new Vector3(extents.x, -extents.y, extents.z);
            Vector3 c6 = center + new Vector3(extents.x, extents.y, extents.z);
            Vector3 c7 = center + new Vector3(-extents.x, extents.y, extents.z);

            DrawDashedEdge(c0, c1);
            DrawDashedEdge(c1, c2);
            DrawDashedEdge(c2, c3);
            DrawDashedEdge(c3, c0);

            DrawDashedEdge(c4, c5);
            DrawDashedEdge(c5, c6);
            DrawDashedEdge(c6, c7);
            DrawDashedEdge(c7, c4);

            DrawDashedEdge(c0, c4);
            DrawDashedEdge(c1, c5);
            DrawDashedEdge(c2, c6);
            DrawDashedEdge(c3, c7);
        }

        private static void DrawDashedEdge(Vector3 start, Vector3 end)
        {
            Handles.DrawDottedLine(start, end, DashedLineScreenSize);
        }

        private static Vector3 GetDominantAxis(Vector3 size)
        {
            Vector3 absoluteSize = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            if (absoluteSize.x >= absoluteSize.y && absoluteSize.x >= absoluteSize.z) return Vector3.right;
            if (absoluteSize.y >= absoluteSize.x && absoluteSize.y >= absoluteSize.z) return Vector3.up;
            return Vector3.forward;
        }

        private static Color GetHighContrastHandleColor(Color baseColor)
        {
            Color handleColor = Color.Lerp(baseColor, Color.white, 0.45f);
            handleColor.a = 1f;
            return handleColor;
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

            if (Event.current.type == EventType.Repaint)
            {
                Vector3 boxSize = new Vector3(vehicle.frontSensor.localScale.x, vehicle.frontSensor.localScale.y, currentDist);
                Vector3 boxCenter = new Vector3(0, 0, zStart + (currentDist * 0.5f));
                
                Matrix4x4 baseMatrix = Matrix4x4.TRS(worldOrigin, vehicle.transform.rotation, Vector3.one);
                using (new Handles.DrawingScope(baseMatrix))
                {
                    DrawSensorOutline(boxCenter, boxSize, StoppingDistanceColor, Vector3.forward);
                }
            }

            Handles.color = GetHighContrastHandleColor(StoppingDistanceColor);
            EditorGUI.BeginChangeCheck();

            float handleSize = HandleUtility.GetHandleSize(handleWorldPos) * 0.03f;
            Vector3 newWorldPos = Handles.Slider(handleWorldPos, vehicle.transform.forward, handleSize, Handles.DotHandleCap, 0f);

            if (EditorGUI.EndChangeCheck())
            {
                float delta = Vector3.Dot(newWorldPos - handleWorldPos, vehicle.transform.forward);
                float newDist = Mathf.Clamp(currentDist + delta, 0.1f, vehicle.frontSensor.localScale.z);

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
            labelStyle.normal.textColor = StoppingDistanceColor;
            Handles.Label(handleWorldPos + vehicle.transform.up * 0.5f, $"Stop Dist: {vehicle.driverBehaviour.stoppingDistance:F1}m", labelStyle);
        }

        private void DrawSpawnPaddingHandle(AIVehicle vehicle)
        {
            Vector3 extents = vehicle.GetSpawnBoxHalfExtents();
            Vector3 centerOffset = vehicle.GetSpawnBoxCenterOffset();

            // Draw the filled block for the spawn padding area
            if (Event.current.type == EventType.Repaint)
            {
                Color fillColor = Color.cyan;
                fillColor.a = 0.1f; // A bit transparent
                Handles.color = fillColor;

                // Set matrix to vehicle's transform space
                Matrix4x4 vehicleMatrix = Matrix4x4.TRS(vehicle.transform.position, vehicle.transform.rotation, Vector3.one);
                // The box transform is relative to the vehicle's transform
                Matrix4x4 boxMatrix = Matrix4x4.TRS(centerOffset, Quaternion.identity, extents * 2f);

                Matrix4x4 oldMatrix = Handles.matrix;
                Handles.matrix = vehicleMatrix * boxMatrix;
                Handles.CubeHandleCap(0, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
                Handles.matrix = oldMatrix;
            }

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

            for (int i = 0; i < vehicle.wheels.Length; i++)
            {
                SuspensionWheel wheel = vehicle.wheels[i];
                if (wheel == null || wheel.raycastTransform == null) continue;

                // Use visual mesh position if available, otherwise calculate from rest length
                Vector3 wheelCenter;
                if (wheel.visualMesh != null)
                {
                    wheelCenter = wheel.visualMesh.position;
                }
                else
                {
                    Vector3 origin = wheel.raycastTransform.position;
                    wheelCenter = origin - vehicle.transform.up * wheel.restLength;
                }

                Transform wheelTransform = wheel.raycastTransform;
                float radius = wheel.radius;

                // The axle is the local right vector of the wheel transform
                Vector3 axle = wheelTransform.right;
                // A vector pointing from the center to the bottom of the circumference, used for the slider
                Vector3 radiusVector = -wheelTransform.up;

                Handles.color = Color.cyan;

                // Draw the circle representing the wheel
                Handles.DrawWireDisc(wheelCenter, axle, radius);

                // Create a slider handle on the circumference to adjust the radius
                EditorGUI.BeginChangeCheck();
                Vector3 handlePosition = wheelCenter + radiusVector * radius;
                float size = HandleUtility.GetHandleSize(handlePosition) * 0.03f;

                // Use a dot cap for a cleaner look
                Vector3 newHandlePosition = Handles.Slider(handlePosition, radiusVector, size, Handles.DotHandleCap, 0.01f);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(vehicle, $"Change Wheel {i} Radius");

                    // Calculate the new radius from the handle's new position
                    float newRadius = Vector3.Dot(newHandlePosition - wheelCenter, radiusVector);
                    
                    // Apply the new radius, ensuring it's not negative or zero
                    wheel.radius = Mathf.Max(0.01f, newRadius);

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
                Undo.RegisterFullObjectHierarchyUndo(vehicle.gameObject, "Auto Setup Vehicle");
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
            if (maxTiltAngleProp != null) EditorGUILayout.PropertyField(maxTiltAngleProp);

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
                sensorsObj.transform.SetParent(vehicle.transform, false);
                sensorsObj.transform.localPosition = Vector3.zero;
                sensorsObj.transform.localRotation = Quaternion.identity;
                sensorsObj.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(sensorsObj, "Create Sensors Root");
                sensorsRoot = sensorsObj.transform;
                Debug.Log("Auto Setup: Created 'Sensors' root object.");
            }
            sensorsRoot.localPosition = Vector3.zero;
            sensorsRoot.localRotation = Quaternion.identity;
            sensorsRoot.localScale = Vector3.one;

            Transform frontSensorT = sensorsRoot.Find("FrontSensor");
            if (frontSensorT == null)
            {
                GameObject fsObj = new GameObject("FrontSensor");
                fsObj.transform.SetParent(sensorsRoot);
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
                rsObj.transform.localRotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(rsObj, "Create RightSensor");
                rightSensorT = rsObj.transform;
                Debug.Log("Auto Setup: Created 'RightSensor'.");
            }
            vehicle.rightSensor = rightSensorT;

            ConfigureSensorsFromCollider(vehicle, frontSensorT, leftSensorT, rightSensorT);

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

        private static void ConfigureSensorsFromCollider(AIVehicle vehicle, Transform frontSensorT, Transform leftSensorT, Transform rightSensorT)
        {
            BoxCollider vehicleCollider = vehicle.vehicleCollider != null
                ? vehicle.vehicleCollider
                : vehicle.GetComponent<BoxCollider>();

            if (vehicleCollider == null)
            {
                float fallbackHeight = 1f;
                float fallbackFrontLength = Mathf.Max(1f, vehicle.driverBehaviour.stoppingDistance * 2f);

                frontSensorT.localPosition = new Vector3(0f, 0.5f, fallbackFrontLength * 0.5f);
                frontSensorT.localScale = new Vector3(1f, fallbackHeight, fallbackFrontLength);

                leftSensorT.localPosition = new Vector3(-1f, 0.5f, 0f);
                leftSensorT.localScale = new Vector3(1f, fallbackHeight, 1f);

                rightSensorT.localPosition = new Vector3(1f, 0.5f, 0f);
                rightSensorT.localScale = new Vector3(1f, fallbackHeight, 1f);
                return;
            }

            Vector3 colliderCenter = vehicleCollider.center;
            Vector3 colliderSize = vehicleCollider.size;

            float sensorHeight = Mathf.Max(0.01f, colliderSize.y);
            float frontWidth = Mathf.Max(0.01f, colliderSize.x);
            float frontLength = Mathf.Max(0.1f, vehicle.driverBehaviour.stoppingDistance * 2f);
            float sideWidth = Mathf.Max(0.1f, GetDefaultSideSensorWidth(leftSensorT, rightSensorT));
            float sideLength = Mathf.Max(0.01f, colliderSize.z);

            float colliderFrontZ = colliderCenter.z + colliderSize.z * 0.5f;
            float colliderLeftX = colliderCenter.x - colliderSize.x * 0.5f;
            float colliderRightX = colliderCenter.x + colliderSize.x * 0.5f;

            frontSensorT.localPosition = new Vector3(
                colliderCenter.x,
                colliderCenter.y,
                colliderFrontZ + frontLength * 0.5f);
            frontSensorT.localScale = new Vector3(frontWidth, sensorHeight, frontLength);

            leftSensorT.localPosition = new Vector3(
                colliderLeftX - sideWidth * 0.5f,
                colliderCenter.y,
                colliderCenter.z);
            leftSensorT.localScale = new Vector3(sideWidth, sensorHeight, sideLength);

            rightSensorT.localPosition = new Vector3(
                colliderRightX + sideWidth * 0.5f,
                colliderCenter.y,
                colliderCenter.z);
            rightSensorT.localScale = new Vector3(sideWidth, sensorHeight, sideLength);
        }

        private static float GetDefaultSideSensorWidth(Transform leftSensorT, Transform rightSensorT)
        {
            float leftWidth = leftSensorT != null ? leftSensorT.localScale.x : 0f;
            float rightWidth = rightSensorT != null ? rightSensorT.localScale.x : 0f;
            return Mathf.Max(leftWidth, rightWidth, 1f);
        }
    }
}
