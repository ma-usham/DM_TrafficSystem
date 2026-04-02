using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    [System.Serializable]
    public class TrafficLightVisuals
    {
        public Renderer redLightRenderer;
        public Renderer yellowLightRenderer;
        public Renderer greenLightRenderer;
    }

    [System.Serializable]
    public class TrafficLightRoad
    {
        public List<AIWaypoint> stopPoints = new List<AIWaypoint>();
        public List<TrafficLightVisuals> visuals = new List<TrafficLightVisuals>();
    }

    public class TrafficLightIntersection : MonoBehaviour
    {
        public string intersectionName = "New Traffic Light Intersection";

        [Tooltip("Time the light stays green")]
        public float greenTime = 5f;
        [Tooltip("Time the light stays yellow before red")]
        public float yellowTime = 2f;

        [Header("Emission Settings")]
        [ColorUsage(false, true)] public Color redEmissionColor = Color.red * 2f;
        [ColorUsage(false, true)] public Color yellowEmissionColor = Color.yellow * 2f;
        [ColorUsage(false, true)] public Color greenEmissionColor = Color.green * 2f;
        [ColorUsage(false, true)] public Color offColor = Color.black;

        public List<TrafficLightRoad> trafficLightRoads = new List<TrafficLightRoad>();

        private int currentRoadIndex = 0;

        private MaterialPropertyBlock propBlock;
        private static readonly int emissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            // Reset all visually to Red upon Start
            foreach (var road in trafficLightRoads)
            {
                SetRoadVisualState(road, TrafficLightState.Red);
            }

            SetAllRoadsToStop();
            if (trafficLightRoads.Count > 0)
            {
                StartCoroutine(CycleLights());
            }
        }

        private IEnumerator CycleLights()
        {
            while (true)
            {
                TrafficLightRoad currentRoad = trafficLightRoads[currentRoadIndex];

                // GREEN
                SetRoadStopStatus(currentRoad, false);
                SetRoadVisualState(currentRoad, TrafficLightState.Green);
                yield return new WaitForSeconds(greenTime);

                // YELLOW (In simple logic we keep it as stop point but maybe it should be a warning)
                // For now, treat yellow as stop for incoming cars
                SetRoadStopStatus(currentRoad, true);
                SetRoadVisualState(currentRoad, TrafficLightState.Yellow);
                yield return new WaitForSeconds(yellowTime);

                // RED
                SetRoadVisualState(currentRoad, TrafficLightState.Red);
                yield return new WaitForSeconds(1f); // Optional all-red delay for safety

                currentRoadIndex = (currentRoadIndex + 1) % trafficLightRoads.Count;
            }
        }

        private void SetRoadVisualState(TrafficLightRoad road, TrafficLightState state)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            foreach (var vis in road.visuals)
            {
                if (vis == null) continue;

                UpdateRenderer(vis.redLightRenderer, state == TrafficLightState.Red ? redEmissionColor : offColor);
                UpdateRenderer(vis.yellowLightRenderer, state == TrafficLightState.Yellow ? yellowEmissionColor : offColor);
                UpdateRenderer(vis.greenLightRenderer, state == TrafficLightState.Green ? greenEmissionColor : offColor);
            }
        }

        private void UpdateRenderer(Renderer renderer, Color targetColor)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(emissionColorId, targetColor);
            renderer.SetPropertyBlock(propBlock);
        }

        private void SetRoadStopStatus(TrafficLightRoad road, bool isStop)
        {
            foreach (var wp in road.stopPoints)
            {
                if (wp != null)
                {
                    wp.settings.isStopPoint = isStop;
                }
            }
        }

        private void SetAllRoadsToStop()
        {
            foreach (var road in trafficLightRoads)
            {
                SetRoadStopStatus(road, true);
            }
        }
    }
}