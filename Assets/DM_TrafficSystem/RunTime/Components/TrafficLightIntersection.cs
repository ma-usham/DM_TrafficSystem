using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Darkmatter.TrafficSystem
{
    /// <summary>
    /// Stores the renderers that represent one physical traffic light.
    /// </summary>
    [System.Serializable]
    public class TrafficLightVisuals
    {
        public Renderer redLightRenderer;
        public Renderer yellowLightRenderer;
        public Renderer greenLightRenderer;
    }

    /// <summary>
    /// Stores one traffic-light road group together with its stop points and light visuals.
    /// </summary>
    [System.Serializable]
    public class TrafficLightRoad : IIntersectionRoad
    {
        public List<AIWaypoint> stopPoints = new List<AIWaypoint>();
        public List<TrafficLightVisuals> visuals = new List<TrafficLightVisuals>();

        //Tracks the current Light State for this assigned road group.
        [HideInInspector] public TrafficLightState currentLightState = TrafficLightState.Red;

        public List<AIWaypoint> GetStopPoints() => stopPoints;
    }

    /// <summary>
    /// Cycles stop states and renderer emissions for configured traffic-light road groups at runtime.
    /// </summary>
    public class TrafficLightIntersection : IntersectionBase
    {
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

        public override IEnumerable<IIntersectionRoad> GetAllRoads() => trafficLightRoads;

        private int currentRoadIndex = 0;

        private MaterialPropertyBlock propBlock;
        private static readonly int emissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// Creates the shared material property block used for light emission updates.
        /// </summary>
        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Initializes all lights to red and starts the runtime cycle when road groups exist.
        /// </summary>
        private void Start()
        {
            foreach (var road in trafficLightRoads)
            {
                SetRoadVisualState(road, TrafficLightState.Red);
            }

            SetAllRoadsToStop(trafficLightRoads);
            if (trafficLightRoads.Count > 0)
            {
                cycleCoroutine = StartCoroutine(CycleLights());
            }
        }

        /// <summary>
        /// Alternates green, yellow, and red states across each configured road group.
        /// </summary>
        private IEnumerator CycleLights()
        {
            while (true)
            {
                TrafficLightRoad currentRoad = trafficLightRoads[currentRoadIndex];

                SetRoadStopStatus(currentRoad, false);
                SetRoadVisualState(currentRoad, TrafficLightState.Green);
                yield return new WaitForSeconds(greenTime);

                SetRoadStopStatus(currentRoad, true);
                SetRoadVisualState(currentRoad, TrafficLightState.Yellow);
                yield return new WaitForSeconds(yellowTime);

                SetRoadVisualState(currentRoad, TrafficLightState.Red);
                yield return new WaitForSeconds(1f);

                currentRoadIndex = (currentRoadIndex + 1) % trafficLightRoads.Count;
            }
        }

        /// <summary>
        /// Applies one light color state to every renderer set in the provided road group.
        /// </summary>
        private void SetRoadVisualState(TrafficLightRoad road, TrafficLightState state)
        {
            road.currentLightState = state;

            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            foreach (var vis in road.visuals)
            {
                if (vis == null) continue;

                UpdateRenderer(vis.redLightRenderer, state == TrafficLightState.Red ? redEmissionColor : offColor);
                UpdateRenderer(vis.yellowLightRenderer, state == TrafficLightState.Yellow ? yellowEmissionColor : offColor);
                UpdateRenderer(vis.greenLightRenderer, state == TrafficLightState.Green ? greenEmissionColor : offColor);
            }
        }

        /// <summary>
        /// Writes the requested emission color to one renderer using a shared property block.
        /// </summary>
        private void UpdateRenderer(Renderer renderer, Color targetColor)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(emissionColorId, targetColor);
            renderer.SetPropertyBlock(propBlock);
        }

        #region API
        /// <summary>
        /// Returns the current traffic light color state for a specific road index.
        /// </summary>
        public TrafficLightState GetRoadLightState(int roadIndex)
        {
            if (roadIndex < 0 || roadIndex >= trafficLightRoads.Count)
            {
                Debug.LogWarning($"Intersection '{intersectionName}': Invalid road index {roadIndex}");
                return TrafficLightState.Red; // Default safe state
            }

            return trafficLightRoads[roadIndex].currentLightState;
        }
        #endregion
    }
}
