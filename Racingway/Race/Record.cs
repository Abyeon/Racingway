using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Racingway.Utils;

namespace Racingway.Race
{

    public class Record
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public DateTime Date { get; set; }
        public string Name { get; set; }
        public string World { get; set; }
        public TimeSpan Time { get; set; }
        public float Distance { get; set; }
        public double[]? Splits { get; set; } = null;
        public TimedVector3[] Line { get; set; }
        public string RouteId { get; set; }
        public string RouteName { get; set; }
        public string RouteAddress { get; set; }
        public string RouteHash { get; set; }
        public bool IsClient { get; set; }

        // Track whether line simplification has been completed
        private bool _lineSimplified = false;

        // Semaphore to prevent multiple simplification operations
        private static SemaphoreSlim _simplificationSemaphore = new SemaphoreSlim(1, 1);

        public Record(DateTime date, string name, string world, TimeSpan time, float distance, TimedVector3[] line, Route route)
        {
            Id = new();

            Date = date;
            Name = name;
            World = world;
            Time = time;
            Distance = distance;

            // Store a direct copy of the line first instead of simplifying immediately
            // This prevents main thread stalling during race finish
            Line = line.ToArray();
            _lineSimplified = false;

            RouteId = route.Id.ToString();
            RouteName = route.Name;
            RouteAddress = route.Address.LocationId;
            RouteHash = route.GetHash();

            // Start line simplification on a background thread
            Task.Run(() => SimplifyLineAsync());
        }

        // Perform line simplification on a background thread
        private async Task SimplifyLineAsync()
        {
            try
            {
                // Prevent multiple simultaneous simplifications
                await _simplificationSemaphore.WaitAsync();

                if (_lineSimplified)
                    return;

                // Make a copy to prevent race conditions
                var originalLine = Line;

                // Apply line simplification
                var simplifiedLine = LineSimplification.SimplifyLine(originalLine);

                // Update the line with simplified version
                Line = simplifiedLine;
                _lineSimplified = true;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error during line simplification: {ex.Message}");
            }
            finally
            {
                _simplificationSemaphore.Release();
            }
        }

        [BsonCtor]
        public Record(
            DateTime date,
            string name,
            string world,
            TimeSpan time,
            float distance,
            double[] splits,
            TimedVector3[] line,
            string routeId,
            string routeName,
            string routeAddress,
            string routeHash,
            bool isClient
        )
        {
            Id = new();

            Date = date;
            Name = name;
            World = world;
            Time = time;
            Distance = distance;
            Splits = splits;
            Line = line;
            RouteId = routeId;
            RouteName = routeName;
            RouteAddress = routeAddress;
            RouteHash = routeHash;
            IsClient = isClient;
            _lineSimplified = true; // Assume deserialized records already have simplified lines
        }

        /// <summary>
        /// Gets the original line points count before simplification
        /// </summary>
        /// <returns>The count of original points that would have been stored without simplification</returns>
        public int GetOriginalPointCount()
        {
            return Line.Length;
        }

        /// <summary>
        /// Ensures the line is simplified before database operations
        /// </summary>
        public async Task EnsureLineSimplified()
        {
            if (!_lineSimplified)
            {
                await SimplifyLineAsync();
            }
        }

        /// <summary>
        /// Estimates the storage savings from line simplification as a percentage
        /// </summary>
        /// <param name="originalPointCount">The count of points before simplification</param>
        /// <returns>Percentage of storage saved</returns>
        public static float CalculateStorageSavings(int originalPointCount,int simplifiedPointCount)
        {
            if (originalPointCount <= 0)
                return 0;
            return 100f * (1f - ((float)simplifiedPointCount / originalPointCount));
        }

        public string GetCSV()
        {
            return $"{Date.ToString("M/dd/yyyy H:mm:ss")},{Name},{World},{Utils.Time.PrettyFormatTimeSpan(Time)},{Distance.ToString()},{RouteName}\n";
        }
    }
}
