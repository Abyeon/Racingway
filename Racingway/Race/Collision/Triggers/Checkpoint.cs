using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LiteDB;
using ZLinq;

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
        public bool Active { get; set; } = false;
        public List<uint> Touchers { get; set; } = new List<uint>();
        public uint? FlagIcon { get; set; } = 60557;

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

        public void CheckCollision(Player player)
        {
            var inTrigger = Cube.PointInCube(player.position);

            // Fast return if nothing changed
            if ((!inTrigger && !Touchers.Contains(player.id)))
            {
                return;
            }

            bool grounded = (player.isGrounded && Route.RequireGroundedCheckpoint) || !Route.RequireGroundedCheckpoint;

            if (inTrigger && !Touchers.Contains(player.id) && grounded)
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
            // Return if the player has this checkpoint in their splits
            if (player.currentSplits.AsValueEnumerable().Where(x => x.checkpoint == this).Count() > 0) return;

            player.currentSplits.Add(new TimedCheckpoint(this, player.timer.ElapsedMilliseconds));
            Route.HitCheckpoint(player);

            Active = true;
            Color = ActiveColor;
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
