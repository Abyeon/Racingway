using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using LiteDB;

namespace Racingway.Race.Collision.Triggers
{
    public class Checkpoint : ITrigger
    {
        public ObjectId Id { get; set; }
        public Route Route { get; set; }
        public Cube Cube { get; set; }

        private static readonly uint InactiveColor = 0x22FF3061;
        private static readonly uint ActiveColor = 0x2200FF00;

        public uint Color { get; set; } = InactiveColor;
        public List<uint> Touchers { get; set; } = new List<uint>();

        public Checkpoint(Route route, Vector3 position, Vector3 scale, Vector3 rotation)
        {
            this.Id = new();
            this.Route = route;
            this.Cube = new Cube(position, scale, rotation);
        }

        public Checkpoint(Route route, Cube cube)
        {
            this.Id = new();
            this.Route = route;
            this.Cube = cube;
        }

        public void OnEntered(Player player)
        {
            Color = ActiveColor;
        }

        public void OnLeft(Player player)
        {
            if (Touchers.Count == 0)
            {
                Color = InactiveColor;
            }
        }

        public BsonDocument GetSerialized()
        {
            var doc = new BsonDocument();
            doc["_id"] = Id;
            doc["Type"] = "Checkpoint";

            BsonArray cube = [
                Cube.Position.X.ToString(), Cube.Position.Y.ToString(), Cube.Position.Z.ToString(),  // Position
                Cube.Scale.X.ToString(),    Cube.Scale.Y.ToString(),    Cube.Scale.Z.ToString(),     // Scale
                Cube.Rotation.X.ToString(), Cube.Rotation.Y.ToString(), Cube.Rotation.Z.ToString()]; // Roration

            doc["Cube"] = cube;

            return doc;
        }
    }
}
