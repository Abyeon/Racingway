using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using Newtonsoft.Json;

namespace Racingway.Race.Collision.Triggers
{
    public class Start : ITrigger
    {
        public ObjectId Id { get; set; }
        public Route Route { get; set; }
        public Cube Cube { get; set; }

        private static readonly uint InactiveColor = 0x22FF3061;
        private static readonly uint ActiveColor = 0x2200FF00;

        public uint Color { get; set; } = InactiveColor;
        public List<uint> Touchers { get; set; } = new List<uint>();

        public Start(Route route, Vector3 position, Vector3 scale, Vector3 rotation)
        {
            this.Id = new();
            this.Route = route;
            this.Cube = new Cube(position, scale, rotation);
        }

        public Start(Route route, Cube cube)
        {
            this.Id = new();
            this.Route = route;
            this.Cube = cube;
        }

        public void CheckCollision(Player player)
        {
            var inTrigger = Cube.PointInCube(player.position);
            if (inTrigger && !Touchers.Contains(player.id))
            {
                Touchers.Add(player.id);
                OnEntered(player);
            }
            else if (!inTrigger && Touchers.Contains(player.id))
            {
                Touchers.Remove(player.id);
                OnLeft(player);
            }
        }

        public void OnEntered(Player player)
        {
            Color = ActiveColor;

            Route.Failed(player);

            int index = Route.PlayersInParkour.FindIndex(x => x.Item1 == player);
            if (index == -1)
                return; // return if player is not in parkour

            player.inParkour = false;

            Route.PlayersInParkour.RemoveAt(index);
            player.raceLine.Clear();
            player.timer.Reset();
        }

        public void OnLeft(Player player)
        {
            if (Touchers.Count == 0)
            {
                Color = InactiveColor;
            }

            player.timer.Start();
            player.inParkour = true;
            player.AddPoint();

            Route.PlayersInParkour.Add((player, Stopwatch.StartNew()));
            Route.Started(player);

            player.raceLine.Clear();
        }

        public BsonDocument GetSerialized()
        {
            var doc = new BsonDocument();
            doc["_id"] = Id;
            doc["Type"] = "Start";

            BsonArray cube =
            [
                Cube.Position.X.ToString(),
                Cube.Position.Y.ToString(),
                Cube.Position.Z.ToString(), // Position
                Cube.Scale.X.ToString(),
                Cube.Scale.Y.ToString(),
                Cube.Scale.Z.ToString(), // Scale
                Cube.Rotation.X.ToString(),
                Cube.Rotation.Y.ToString(),
                Cube.Rotation.Z.ToString(),
            ]; // Roration

            doc["Cube"] = cube;

            return doc;
        }
    }
}
