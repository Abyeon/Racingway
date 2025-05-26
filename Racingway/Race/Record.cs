using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using MessagePack;
using Racingway.Utils;

namespace Racingway.Race
{
    [MessagePackObject]
    public class Record
    {
        [BsonId, IgnoreMember]
        public ObjectId Id { get; set; }
        [Key(0)] public DateTime Date { get; set; }
        [Key(1)] public string Name { get; set; }
        [Key(2)] public string World { get; set; }
        [Key(3)] public TimeSpan Time { get; set; }
        [Key(4)] public float Distance { get; set; }
        [Key(5)] public double[]? Splits { get; set; } = null;
        [Key(6)] public TimedVector3[] Line { get; set; }
        [Key(7)] public string RouteId { get; set; }
        [Key(8)] public string RouteName { get; set; }
        [Key(9)] public string RouteAddress { get; set; }
        [Key(10)] public string RouteHash { get; set; }
        [Key(11)] public bool IsClient { get; set; }

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

        [BsonCtor, SerializationConstructor]
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
