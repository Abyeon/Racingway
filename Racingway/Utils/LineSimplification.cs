using Racingway.Race;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Racingway.Utils
{
    /// <summary>
    /// Contains algorithms for simplifying racing lines to reduce database size
    /// </summary>
    public static class LineSimplification
    {
        /// <summary>
        /// Simplifies a racing line using the Douglas-Peucker algorithm with adaptive epsilon based on path length
        /// </summary>
        /// <param name="points">The original racing line points</param>
        /// <param name="baseEpsilon">The base epsilon value for simplification tolerance</param>
        /// <param name="minPoints">Minimum number of points to retain in the simplified line</param>
        /// <returns>A simplified racing line with fewer points</returns>
        public static TimedVector3[] SimplifyLine(
            TimedVector3[] points,
            float baseEpsilon = 0.5f,
            int minPoints = 10
        )
        {
            if (points == null || points.Length <= 2)
                return points;

            // Calculate path length to scale epsilon
            float pathLength = CalculatePathLength(points);

            // Improved adaptive epsilon calculation for more consistent results
            // Square root scaling provides better balance between short and long paths
            float adaptiveEpsilon = baseEpsilon * (float)Math.Sqrt(Math.Max(pathLength, 10) / 10);

            // Cap epsilon to prevent over-simplification on very long paths
            adaptiveEpsilon = Math.Min(adaptiveEpsilon, baseEpsilon * 5);

            // Identify critical turning points that must be preserved
            HashSet<int> criticalPoints = IdentifyCriticalTurningPoints(points);

            // Create markers array for points to keep
            bool[] markers = new bool[points.Length];

            // Mark endpoints
            markers[0] = true;
            markers[points.Length - 1] = true;

            // Mark critical turning points
            foreach (int idx in criticalPoints)
            {
                markers[idx] = true;
            }

            // Apply Douglas-Peucker algorithm
            DouglasPeuckerRecursive(points, markers, 0, points.Length - 1, adaptiveEpsilon);

            // Create simplified list
            List<TimedVector3> simplified = new List<TimedVector3>();
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i])
                {
                    simplified.Add(points[i]);
                }
            }

            // Ensure minimum number of points
            if (simplified.Count < minPoints && points.Length > minPoints)
            {
                // If we have too few points, add evenly spaced points from the original set
                return EnsureMinimumPoints(points, simplified, minPoints);
            }

            return simplified.ToArray();
        }

        /// <summary>
        /// Recursively applies the Douglas-Peucker algorithm to simplify a line segment
        /// </summary>
        private static void DouglasPeuckerRecursive(
            TimedVector3[] points,
            bool[] markers,
            int startIdx,
            int endIdx,
            float epsilon
        )
        {
            if (endIdx <= startIdx + 1)
                return;

            // Find the point with the maximum distance from the line segment
            float maxDistance = 0;
            int maxIndex = startIdx;

            Vector3 lineStart = points[startIdx].asVector();
            Vector3 lineEnd = points[endIdx].asVector();

            for (int i = startIdx + 1; i < endIdx; i++)
            {
                float distance = PerpendicularDistance(points[i].asVector(), lineStart, lineEnd);

                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    maxIndex = i;
                }
            }

            // If the maximum distance is greater than epsilon, recursively simplify the two segments
            if (maxDistance > epsilon)
            {
                markers[maxIndex] = true;

                DouglasPeuckerRecursive(points, markers, startIdx, maxIndex, epsilon);
                DouglasPeuckerRecursive(points, markers, maxIndex, endIdx, epsilon);
            }
        }

        /// <summary>
        /// Calculates the perpendicular distance from a point to a line segment
        /// </summary>
        private static float PerpendicularDistance(
            Vector3 point,
            Vector3 lineStart,
            Vector3 lineEnd
        )
        {
            Vector3 line = lineEnd - lineStart;
            float lineLength = line.Length();

            if (lineLength == 0)
                return Vector3.Distance(point, lineStart);

            // Project point onto line
            Vector3 lineDirection = line / lineLength;
            float projection = Vector3.Dot(point - lineStart, lineDirection);

            // Handle points outside the line segment
            if (projection <= 0)
                return Vector3.Distance(point, lineStart);
            if (projection >= lineLength)
                return Vector3.Distance(point, lineEnd);

            // Calculate the perpendicular distance
            Vector3 projectedPoint = lineStart + lineDirection * projection;
            return Vector3.Distance(point, projectedPoint);
        }

        /// <summary>
        /// Calculates the total path length of a racing line
        /// </summary>
        private static float CalculatePathLength(TimedVector3[] points)
        {
            float length = 0;
            for (int i = 1; i < points.Length; i++)
            {
                length += Vector3.Distance(points[i - 1].asVector(), points[i].asVector());
            }
            return length;
        }

        /// <summary>
        /// Identifies critical turning points that should be preserved in the simplified line
        /// </summary>
        private static HashSet<int> IdentifyCriticalTurningPoints(TimedVector3[] points)
        {
            HashSet<int> criticalPoints = new HashSet<int>();
            if (points.Length < 3)
                return criticalPoints;

            // Increased angle threshold for sharper turns only (originally 25 degrees)
            const float angleThreshold = 35.0f * (float)(Math.PI / 180.0); // 35 degrees in radians

            // Add points at regular intervals to maintain shape even on gentle curves
            int interval = Math.Max(5, points.Length / 20); // Add at least one point per 20 points

            for (int i = 1; i < points.Length - 1; i++)
            {
                // Keep points at regular intervals regardless of angle
                if (i % interval == 0)
                {
                    criticalPoints.Add(i);
                    continue;
                }

                Vector3 v1 = Vector3.Normalize(points[i].asVector() - points[i - 1].asVector());
                Vector3 v2 = Vector3.Normalize(points[i + 1].asVector() - points[i].asVector());

                // Calculate angle between segments
                float dotProduct = Vector3.Dot(v1, v2);
                dotProduct = Math.Clamp(dotProduct, -1.0f, 1.0f); // Ensure within valid range for acos
                float angle = (float)Math.Acos(dotProduct);

                // If the angle is significant, mark as a critical turning point
                if (angle > angleThreshold)
                {
                    criticalPoints.Add(i);
                }
            }

            return criticalPoints;
        }

        /// <summary>
        /// Ensures a minimum number of points in the simplified line
        /// </summary>
        private static TimedVector3[] EnsureMinimumPoints(
            TimedVector3[] original,
            List<TimedVector3> simplified,
            int minPoints
        )
        {
            // Keep all points that were already marked for keeping
            HashSet<int> keptIndices = new HashSet<int>();
            foreach (var point in simplified)
            {
                for (int i = 0; i < original.Length; i++)
                {
                    if (
                        point.X == original[i].X
                        && point.Y == original[i].Y
                        && point.Z == original[i].Z
                        && point.Offset == original[i].Offset
                    )
                    {
                        keptIndices.Add(i);
                        break;
                    }
                }
            }

            // Calculate number of additional points needed
            int pointsToAdd = minPoints - simplified.Count;
            if (pointsToAdd <= 0)
                return simplified.ToArray();

            // Calculate indices to add, distributed evenly
            int totalAvailable = original.Length - simplified.Count;
            int step = Math.Max(1, totalAvailable / pointsToAdd);

            List<int> indicesToAdd = new List<int>();
            for (int i = 0; i < original.Length && indicesToAdd.Count < pointsToAdd; i += step)
            {
                if (!keptIndices.Contains(i))
                {
                    indicesToAdd.Add(i);
                }
            }

            // Add the additional points to the simplified list
            foreach (int idx in indicesToAdd)
            {
                simplified.Add(original[idx]);
            }

            // Sort by offset to maintain temporal ordering
            simplified.Sort((a, b) => a.Offset.CompareTo(b.Offset));

            return simplified.ToArray();
        }
    }
}
