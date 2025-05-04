using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using Racingway.Utils;

namespace Racingway.Race
{
    public class Record
    {
        [BsonId]
        public ObjectId Id { get; set; } = new ObjectId();
        public required string RouteId { get; set; }
        public required string RouteHash { get; set; }
        public required string Name { get; set; }
        public required string World { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public float Distance { get; set; }
        public bool IsClient { get; set; }

        // To prevent document size issues, we'll downsample the path
        // and split it into chunks if needed
        [BsonField("PathPoints")]
        private Vector3[]? _pathPoints;

        // Ignored in serialization but used in code
        [BsonIgnore]
        public Vector3[]? Line
        {
            get => _pathPoints;
            set => _pathPoints = DownsamplePath(value);
        }

        // We need a parameterless constructor for BsonMapper
        public Record() { }

        public Record(
            DateTime date,
            string name,
            string world,
            TimeSpan time,
            float distance,
            Vector3[] line,
            Route route
        )
        {
            Date = date;
            Name = name;
            World = world;
            Time = time;
            Distance = distance;
            Line = line; // Will be downsampled automatically
            RouteId = route.Id.ToString();
            RouteHash = route.GetHash();
        }

        private Vector3[]? DownsamplePath(Vector3[]? originalPath)
        {
            if (originalPath == null)
                return originalPath;

            // Get max points from configuration
            int maxPoints = 500; // Default
            if (Plugin.PluginInterface.GetPluginConfig() is Configuration config)
            {
                maxPoints = config.MaxPathSamplingPoints;
            }

            // If path is small enough, return as is
            if (originalPath.Length <= maxPoints)
                return originalPath;

            // For tight speedruns, we want to be extra accurate
            // More aggressive simplification for long paths, more accuracy for short paths
            float epsilon = CalculateAdaptiveEpsilon(originalPath);

            // Use Douglas-Peucker algorithm for intelligent path simplification
            var simplified = DouglasPeuckerSimplify(originalPath, epsilon);

            // If simplification didn't reduce enough, enforce max points
            if (simplified.Length > maxPoints)
            {
                // Get critical points (significant turns, start/finish)
                var criticalPoints = ExtractCriticalPoints(originalPath, maxPoints / 4);

                // Create result array
                var result = new Vector3[maxPoints];

                // Always include start and end points
                result[0] = originalPath[0];
                result[maxPoints - 1] = originalPath[originalPath.Length - 1];

                // First add critical points to preserve important features
                int criticalPointsToUse = Math.Min(criticalPoints.Count, maxPoints / 4);
                for (int i = 0; i < criticalPointsToUse; i++)
                {
                    // Map critical points to result array, distribute evenly
                    int targetIndex = 1 + i * ((maxPoints - 2) / criticalPointsToUse);
                    if (targetIndex < maxPoints - 1)
                    {
                        result[targetIndex] = originalPath[criticalPoints[i]];
                    }
                }

                // Fill remaining points evenly from simplified path
                int filledPoints = criticalPointsToUse + 2; // start, end, and critical points
                int remainingPoints = maxPoints - filledPoints;

                if (remainingPoints > 0 && simplified.Length > 2)
                {
                    double step = (double)(simplified.Length - 2) / (remainingPoints);

                    for (int i = 0; i < remainingPoints; i++)
                    {
                        int sourceIndex = (int)Math.Round(1 + i * step);
                        sourceIndex = Math.Min(sourceIndex, simplified.Length - 2);

                        // Find an empty slot for this point
                        for (int j = 1; j < maxPoints - 1; j++)
                        {
                            // If this slot is empty (default Vector3)
                            if (result[j] == default)
                            {
                                result[j] = simplified[sourceIndex];
                                break;
                            }
                        }
                    }
                }

                // Fill any remaining empty slots
                for (int i = 1; i < maxPoints - 1; i++)
                {
                    if (result[i] == default)
                    {
                        // Interpolate between the closest non-default points
                        int prev = i - 1;
                        while (prev > 0 && result[prev] == default)
                            prev--;

                        int next = i + 1;
                        while (next < maxPoints - 1 && result[next] == default)
                            next++;

                        if (prev >= 0 && next < maxPoints)
                        {
                            float t = (float)(i - prev) / (next - prev);
                            result[i] = Vector3.Lerp(result[prev], result[next], t);
                        }
                        else if (prev >= 0)
                        {
                            result[i] = result[prev];
                        }
                        else if (next < maxPoints)
                        {
                            result[i] = result[next];
                        }
                    }
                }

                return result;
            }

            return simplified;
        }

        // Calculate adaptive epsilon based on path characteristics
        private float CalculateAdaptiveEpsilon(Vector3[] path)
        {
            if (path.Length <= 2)
                return 0.1f;

            // Get configuration epsilon or use default
            float baseEpsilon = 0.3f;
            if (Plugin.PluginInterface.GetPluginConfig() is Configuration config)
            {
                baseEpsilon = config.PathSimplificationTolerance;
            }

            // Calculate path length
            float totalDist = 0;
            for (int i = 1; i < path.Length; i++)
            {
                totalDist += Vector3.Distance(path[i - 1], path[i]);
            }

            // Shorter paths get lower epsilon for more accuracy
            if (totalDist < 500)
                return baseEpsilon * 0.5f;
            else if (totalDist < 1000)
                return baseEpsilon * 0.75f;

            return baseEpsilon;
        }

        // Find points with significant direction changes
        private List<int> ExtractCriticalPoints(Vector3[] path, int maxPoints)
        {
            if (path.Length <= 2)
                return new List<int>();

            var turnAngles = new List<(int index, float angle)>();

            // Calculate turning angles at each point
            for (int i = 1; i < path.Length - 1; i++)
            {
                Vector3 prev = path[i - 1] - path[i];
                Vector3 next = path[i + 1] - path[i];

                prev = Vector3.Normalize(prev);
                next = Vector3.Normalize(next);

                // Dot product gives cosine of angle, lower values mean sharper turns
                float dotProduct = Vector3.Dot(prev, next);

                // Ensure we're in valid range for acos
                dotProduct = Math.Clamp(dotProduct, -1.0f, 1.0f);
                float angle = (float)Math.Acos(dotProduct);

                turnAngles.Add((i, angle));
            }

            // Sort by angle (sharpest turns first) and get indices
            return turnAngles
                .OrderByDescending(t => t.angle)
                .Take(maxPoints)
                .Select(t => t.index)
                .OrderBy(i => i) // Sort by position in path
                .ToList();
        }

        // Douglas-Peucker algorithm to simplify a path while maintaining important points
        private Vector3[] DouglasPeuckerSimplify(Vector3[] points, float epsilon)
        {
            // Use the configuration tolerance instead of hardcoded value
            epsilon = Plugin.PluginInterface.GetPluginConfig() is Configuration config
                ? config.PathSimplificationTolerance
                : 0.3f;

            if (points.Length <= 2)
                return points;

            // Find the point with the maximum distance
            var dmax = 0f;
            var index = 0;

            for (var i = 1; i < points.Length - 1; i++)
            {
                var d = PerpendicularDistance(points[i], points[0], points[^1]);
                if (d > dmax)
                {
                    index = i;
                    dmax = d;
                }
            }

            // If max distance is greater than epsilon, recursively simplify
            if (dmax > epsilon)
            {
                // Recursive call
                var recResults1 = DouglasPeuckerSimplify(points[..(index + 1)], epsilon);
                var recResults2 = DouglasPeuckerSimplify(points[index..], epsilon);

                // Build the result list
                var result = new Vector3[recResults1.Length + recResults2.Length - 1];
                Array.Copy(recResults1, result, recResults1.Length);
                Array.Copy(recResults2, 1, result, recResults1.Length, recResults2.Length - 1);
                return result;
            }

            // If the max distance is less than epsilon, just return the start and end points
            return new[] { points[0], points[^1] };
        }

        // Calculate the perpendicular distance from a point to a line segment
        private float PerpendicularDistance(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            var line = lineEnd - lineStart;
            var len = line.Length();
            if (len == 0)
                return Vector3.Distance(point, lineStart);

            // Project the point onto the line
            var t = Vector3.Dot(point - lineStart, line) / (len * len);
            t = Math.Clamp(t, 0, 1);

            var projection = lineStart + t * line;
            return Vector3.Distance(point, projection);
        }

        /// Optimal output for sharing a route?
        /// Only need:
        ///     Name
        ///     World
        ///     Date
        ///     Time
        ///     Splits
        ///     Line
        ///         Can derive distance
        ///         Can also optimize this to be doubles instead of longs by getting the difference from previous point
        ///     Hash
        ///

        public string GetCSV()
        {
            // TODO: Research line simplification algos, or some other way to compress player lines... They BIG!
            //string compressedLine = Compression.ToCompressedBase64(Line);

            return $"{Date.ToString("M/dd/yyyy H:mm:ss")},{Name},{World},{Utils.Time.PrettyFormatTimeSpan(Time)},{Distance.ToString()}\n";
        }
    }
}
