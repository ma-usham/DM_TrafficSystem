using UnityEngine;
using System.Collections.Generic;

namespace Darkmatter.TrafficSystem
{
    public static class TrafficPathfinder
    {
        public static List<AIWaypoint> FindPath(AIWaypoint startNode, AIWaypoint endNode)
        {
            if (startNode == null || endNode == null) return null;

            List<AIWaypoint> openSet = new List<AIWaypoint> { startNode };
            HashSet<AIWaypoint> openSetHash = new HashSet<AIWaypoint> { startNode };
            Dictionary<AIWaypoint, AIWaypoint> cameFrom = new Dictionary<AIWaypoint, AIWaypoint>();

            Dictionary<AIWaypoint, float> gScore = new Dictionary<AIWaypoint, float> { [startNode] = 0 };

            Dictionary<AIWaypoint, float> fScore = new Dictionary<AIWaypoint, float> { [startNode] = Vector3.Distance(startNode.transform.position, endNode.transform.position) };

            while (openSet.Count > 0)
            {
                // Find node in openSet with the lowest fScore
                int currentIndex = 0;
                AIWaypoint current = openSet[0];
                float minFScore = fScore.TryGetValue(current, out float s) ? s : float.MaxValue;
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (fScore.TryGetValue(openSet[i], out float score) && score < minFScore)
                    {
                        current = openSet[i];
                        minFScore = score;
                        currentIndex = i;
                    }
                }

                // Reached destination
                if (current == endNode)
                {
                    return ReconstructPath(cameFrom, current);
                }

                // Swap with last element and remove to achieve O(1) removal
                int lastIndex = openSet.Count - 1;
                openSet[currentIndex] = openSet[lastIndex];
                openSet.RemoveAt(lastIndex);
                openSetHash.Remove(current);

                // Check neighbors
                if (current.settings.nextWaypoint != null)
                {
                    EvaluateNeighbors(current, current.settings.nextWaypoint, endNode, ref openSet, ref openSetHash, ref cameFrom, ref gScore, ref fScore);
                }

                // Consider lane-changing points as potential neighbors to allow pathfinding across lanes
                if (current.settings.laneChangePoints != null)
                {
                    EvaluateNeighbors(current, current.settings.laneChangePoints, endNode, ref openSet, ref openSetHash, ref cameFrom, ref gScore, ref fScore);
                }
            }

            return null; // Return null if route is impossible
        }

        private static void EvaluateNeighbors(AIWaypoint current, AIWaypoint[] neighbors, AIWaypoint endNode, ref List<AIWaypoint> openSet, ref HashSet<AIWaypoint> openSetHash, ref Dictionary<AIWaypoint, AIWaypoint> cameFrom, ref Dictionary<AIWaypoint, float> gScore, ref Dictionary<AIWaypoint, float> fScore)
        {
            foreach (AIWaypoint neighbor in neighbors)
            {
                if (neighbor == null) continue;

                float tentative_gScore = gScore[current] + Vector3.Distance(current.transform.position, neighbor.transform.position);

                bool hasGScore = gScore.TryGetValue(neighbor, out float neighborGScore);
                if (!hasGScore || tentative_gScore < neighborGScore)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentative_gScore;
                    fScore[neighbor] = tentative_gScore + Vector3.Distance(neighbor.transform.position, endNode.transform.position);

                    if (!openSetHash.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                        openSetHash.Add(neighbor);
                    }
                }
            }
        }

        private static List<AIWaypoint> ReconstructPath(Dictionary<AIWaypoint, AIWaypoint> cameFrom, AIWaypoint current)
        {
            List<AIWaypoint> path = new List<AIWaypoint> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }
            path.Reverse();
            return path;
        }
    }
}