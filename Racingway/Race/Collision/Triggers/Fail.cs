using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using LiteDB;

namespace Racingway.Race.Collision.Triggers
{
    public class Fail : ITrigger
    {
        public ObjectId Id { get; set; }
        public Route Route { get; set; }
        public Cube Cube { get; set; }

        private static readonly uint InactiveColor = 0x22FF3061;
        private static readonly uint ActiveColor = 0x2200FF00;

        public uint Color { get; set; } = InactiveColor;
        public bool Active { get; set; } = false;
        public List<uint> Touchers { get; set; } = new List<uint>();
        public uint? FlagIcon { get; set; } = null;

        public Fail(Route route, Vector3 position, Vector3 scale, Vector3 rotation)
        {
            this.Id = new();
            this.Route = route;
            this.Cube = new Cube(position, scale, rotation);
        }

        public Fail(Route route, Cube cube)
        {
            this.Id = new();
            this.Route = route;
            this.Cube = cube;
        }

        public void OnEntered(Player player)
        {
            Active = true;
            Color = ActiveColor;

            int index = Route.PlayersInParkour.FindIndex(x => x.Item1 == player);
            if (index == -1)
                return;

            Route.Failed(player);
            player.inParkour = false;
            player.timer.Reset();
            player.currentSplits.Clear();
            player.lapsFinished = 0;

            player.ClearLine();
            Route.PlayersInParkour.RemoveAt(index);
            Touchers.Remove(player.id);
            OnLeft(player);
        }

        public void OnLeft(Player player)
        {
            if (Touchers.Count == 0)
            {
                Active = false;
                Color = InactiveColor;
            }
        }

        public BsonDocument GetSerialized()
        {
            var doc = new BsonDocument();
            doc["_id"] = Id;
            doc["Type"] = "Fail";

            BsonArray cube =
            [
                Cube.Position.X.ToString(CultureInfo.InvariantCulture),
                Cube.Position.Y.ToString(CultureInfo.InvariantCulture),
                Cube.Position.Z.ToString(CultureInfo.InvariantCulture), // Position
                Cube.Scale.X.ToString(CultureInfo.InvariantCulture),
                Cube.Scale.Y.ToString(CultureInfo.InvariantCulture),
                Cube.Scale.Z.ToString(CultureInfo.InvariantCulture), // Scale
                Cube.Rotation.X.ToString(CultureInfo.InvariantCulture),
                Cube.Rotation.Y.ToString(CultureInfo.InvariantCulture),
                Cube.Rotation.Z.ToString(CultureInfo.InvariantCulture),
            ]; // Rotation

            doc["Cube"] = cube;

            return doc;
        }
    }
}
