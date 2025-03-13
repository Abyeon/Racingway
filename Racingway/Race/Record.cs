using LiteDB;
using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

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

        public Record(DateTime date, string name, string world, TimeSpan time, float distance, TimedVector3[] line, Route route)
        {
            Id = new();

            Date = date;
            Name = name;
            World = world;
            Time = time;
            Distance = distance;
            Line = line;
            RouteId = route.Id.ToString();
            RouteName = route.Name;
            RouteAddress = route.Address.LocationId;
            RouteHash = route.GetHash();
        }

        [BsonCtor]
        public Record(DateTime date, string name, string world, TimeSpan time, float distance, double[] splits, TimedVector3[] line, string routeId, string routeName, string routeAddress, string routeHash, bool isClient)
        {
            Id = new();

            Date = date;
            Name = name;
            World = world;
            Time = time;
            Distance = distance;
            Line = line;
            RouteId = routeId;
            RouteName = routeName;
            RouteAddress = routeAddress;
            RouteHash = routeHash;
            IsClient = isClient;
        }

        public string GetCSV()
        {
            // TODO: Research line simplification algos, or some other way to compress player lines... They BIG!
            //string compressedLine = Compression.ToCompressedBase64(Line);

            return $"{Date.ToString("M/dd/yyyy H:mm:ss")},{Name},{World},{Utils.Time.PrettyFormatTimeSpan(Time)},{Distance.ToString()},{RouteName}\n";
        }
    }
}
