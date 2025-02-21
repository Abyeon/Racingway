using Dalamud.Game.ClientState.Objects.SubKinds;
using Racingway.Utils;
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
    public class Finish : ITrigger
    {
        public ObjectId Id { get; set; }
        public Route Route { get; set; }
        public Cube Cube { get; set; }

        private static readonly uint InactiveColor = 0x22FF3061;
        private static readonly uint ActiveColor = 0x2200FF00;

        public uint Color { get; set; } = InactiveColor;
        public List<uint> Touchers { get; set; } = new List<uint>();

        public Finish(Route route, Vector3 position, Vector3 scale, Vector3 rotation)
        {
            this.Id = new();
            this.Route = route;
            this.Cube = new Cube(position, scale, rotation);
        }

        public Finish(Route route, Cube cube)
        {
            this.Id = new();
            this.Route = route;
            this.Cube = cube;
        }

        public void OnEntered(Player player)
        {
            Color = ActiveColor;
            DateTime now = DateTime.UtcNow;

            if (Route.PlayersInParkour.Contains(player))
            {
                player.AddPoint();
                player.inParkour = false;
                Route.PlayersInParkour.Remove(player);
                Touchers.Remove(player.id);
                OnLeft(player);

                var elapsedTime = player.timer.ElapsedMilliseconds;
                var t = TimeSpan.FromMilliseconds(elapsedTime);

                var distance = player.GetDistanceTraveled();

                player.timer.Stop();
                player.timer.Reset();

                try
                {
                    IPlayerCharacter actor = (IPlayerCharacter)player.actor;
                    Record record = new Record(now, actor.Name.ToString(), actor.HomeWorld.Value.Name.ToString(), t, distance, player.raceLine.ToArray());
                    Route.Finished(player, record);
                } catch (Exception ex)
                {
                    Plugin.Log.Error(ex.ToString());
                }
            }
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
            doc["Type"] = "Finish";

            BsonArray cube = [
                Cube.Position.X.ToString(), Cube.Position.Y.ToString(), Cube.Position.Z.ToString(),  // Position
                Cube.Scale.X.ToString(),    Cube.Scale.Y.ToString(),    Cube.Scale.Z.ToString(),     // Scale
                Cube.Rotation.X.ToString(), Cube.Rotation.Y.ToString(), Cube.Rotation.Z.ToString()]; // Roration

            doc["Cube"] = cube;

            return doc;
        }
    }
}
