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
    public class Loop : ITrigger
    {
        public ObjectId Id { get; set; }
        public Route Route { get; set; }
        public Cube Cube { get; set; }

        private static readonly uint InactiveColor = 0x22FF3061;
        private static readonly uint ActiveColor = 0x2200FF00;

        public uint Color { get; set; } = InactiveColor;
        public List<uint> Touchers { get; set; } = new List<uint>();
        private Dictionary<uint, bool> playerStarted = new Dictionary<uint, bool>();

        public Loop(Route route, Vector3 position, Vector3 scale, Vector3 rotation)
        {
            this.Id = new();
            this.Route = route;
            this.Cube = new Cube(position, scale, rotation);
        }

        public Loop(Route route, Cube cube)
        {
            this.Id = new();
            this.Route = route;
            this.Cube = cube;
        }

        public void CheckCollision(Player player)
        {
            var inTrigger = Cube.PointInCube(player.position);

            // When player enters trigger and wasn't already inside
            if (inTrigger && !Touchers.Contains(player.id))
            {
                Touchers.Add(player.id);
                OnEntered(player);
            }
            // When player leaves trigger and was inside
            else if (!inTrigger && Touchers.Contains(player.id))
            {
                Touchers.Remove(player.id);
                OnLeft(player);
            }
        }

        public void OnEntered(Player player)
        {
            Color = ActiveColor;

            // If the player is returning to the trigger after starting the race
            if (playerStarted.ContainsKey(player.id) && playerStarted[player.id])
            {
                DateTime now = DateTime.UtcNow;
                int index = Route.PlayersInParkour.FindIndex(x => x.Item1 == player);

                if (index != -1)
                {
                    var elapsedTime = Route.PlayersInParkour[index].Item2.ElapsedMilliseconds;
                    var t = TimeSpan.FromMilliseconds(elapsedTime);

                    player.AddPoint();
                    player.inParkour = false;

                    Route.PlayersInParkour.RemoveAt(index);

                    var distance = player.GetDistanceTraveled();

                    player.timer.Stop();
                    player.timer.Reset();

                    // Reset player's status for future races
                    playerStarted[player.id] = false;

                    try
                    {
                        var actor = player.actor;
                        if (
                            actor != null
                            && actor
                                is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter playerActor
                        )
                        {
                            Record record = new Record(
                                now,
                                playerActor.Name.ToString(),
                                playerActor.HomeWorld.Value.Name.ToString(),
                                t,
                                distance,
                                player.raceLine.ToArray(),
                                this.Route
                            );

                            if (playerActor == Plugin.ClientState.LocalPlayer)
                            {
                                record.IsClient = true;
                            }

                            Route.Finished(player, record);
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error(ex.ToString());
                    }
                }
            }
        }

        public void OnLeft(Player player)
        {
            if (Touchers.Count == 0)
            {
                Color = InactiveColor;
            }

            // Only start the race if this player hasn't already started a race from this trigger
            // or if they've already completed a race (playerStarted reset to false)
            if (!playerStarted.ContainsKey(player.id) || !playerStarted[player.id])
            {
                // Mark this player as having started the race
                playerStarted[player.id] = true;

                player.timer.Start();
                player.inParkour = true;
                player.AddPoint();

                Route.PlayersInParkour.Add((player, Stopwatch.StartNew()));
                Route.Started(player);

                player.raceLine.Clear();
            }
        }

        public BsonDocument GetSerialized()
        {
            var doc = new BsonDocument();
            doc["_id"] = Id;
            doc["Type"] = "Loop";

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
