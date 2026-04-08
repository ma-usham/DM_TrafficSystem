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
            Dictionary<AIWaypoint, AIWaypoint> cameFrom = new Dictionary<AIWaypoint, AIWaypoint>();

            Dictionary<AIWaypoint, float> gScore = new Dictionary<AIWaypoint, float>();
            gScore[startNode] = 0;

            Dictionary<AIWaypoint, float> fScore = new Dictionary<AIWaypoint, float>();
            fScore[startNode] = Vector3.Distance(startNode.transform.position, endNode.transform.position);

            while (openSet.Count > 0)
            {
                // Find node in openSet with the lowest fScore
                AIWaypoint current = openSet[0];
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (fScore.TryGetValue(openSet[i], out float score) && score < fScore[current])
                    {
                        current = openSet[i];
                    }
                }

                // Reached destination
                if (current == endNode)
                {
                    return ReconstructPath(cameFrom, current);
                }

                openSet.Remove(current);

                // Check neighbors
                if (current.settings.nextWaypoint != null)
                {
                    foreach (AIWaypoint neighbor in current.settings.nextWaypoint)
                    {
                        if (neighbor == null) continue;

                        float tentative_gScore = gScore[current] + Vector3.Distance(current.transform.position, neighbor.transform.position);

                        if (!gScore.ContainsKey(neighbor) || tentative_gScore < gScore[neighbor])
                        {
                            cameFrom[neighbor] = current;
                            gScore[neighbor] = tentative_gScore;
                            fScore[neighbor] = tentative_gScore + Vector3.Distance(neighbor.transform.position, endNode.transform.position);

                            if (!openSet.Contains(neighbor))
                            {
                                openSet.Add(neighbor);
                            }
                        }
                    }
                }
            }

            return null; // Return null if route is impossible
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