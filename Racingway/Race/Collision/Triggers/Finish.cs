using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using LiteDB;
using Newtonsoft.Json;
using Racingway.Utils;

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
        public bool Active { get; set; } = false;
        public List<uint> Touchers { get; set; } = new List<uint>();

        // To prevent duplicate finish processing
        private HashSet<uint> _recentlyProcessed = new HashSet<uint>();
        private readonly object _processLock = new object();

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

        public void CheckCollision(Player player)
        {
            var inTrigger = Cube.PointInCube(player.position);

            // Fast return if nothing changed
            if (
                (!inTrigger && !Touchers.Contains(player.id))
                || (_recentlyProcessed.Contains(player.id))
            )
            {
                return;
            }

            bool grounded = (player.isGrounded && Route.RequireGroundedFinish) || !Route.RequireGroundedFinish;

            if (inTrigger && !Touchers.Contains(player.id) && grounded)
            {
                Touchers.Add(player.id);
                OnEntered(player);
            }
            else if (!inTrigger && Touchers.Contains(player.id))
            {
                KickOut(player);
            }
        }

        // Required by ITrigger interface
        public void OnEntered(Player player)
        {
            // This method is required by the interface, but we're using ProcessPlayerFinish instead
            // Starting the process on a background thread to avoid blocking the main thread
            Task.Run(() => ProcessPlayerFinish(player));
        }

        private void ProcessPlayerFinish(Player player)
        {
            // Prevent double-finishing for the same player
            lock (_processLock)
            {
                if (_recentlyProcessed.Contains(player.id))
                    return;

                _recentlyProcessed.Add(player.id);
            }

            try
            {
                Active = true;
                Color = ActiveColor;
                DateTime now = DateTime.UtcNow;
                int index = Route.PlayersInParkour.FindIndex(x => x.Item1 == player);

                if (index != -1)
                {
                    var elapsedTime = Route.PlayersInParkour[index].Item2.ElapsedMilliseconds;
                    var t = TimeSpan.FromMilliseconds(elapsedTime);

                    player.AddPoint();
                    player.inParkour = false;

                    KickOut(player);

                    Route.PlayersInParkour.RemoveAt(index);

                    var distance = player.GetDistanceTraveled();

                    player.timer.Stop();
                    player.timer.Reset();

                    try
                    {
                        Record record = new Record(
                            now,
                            player.Name,
                            player.Homeworld,
                            t,
                            distance,
                            player.RaceLine.ToArray(),
                            this.Route
                        );

                        record.IsClient = player.isClient;

                        Route.Finished(player, record);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error(ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error processing player finish: {ex}");
            }
        }

        public void OnLeft(Player player)
        {
            if (Touchers.Count == 0)
            {
                Active = false;
                Color = InactiveColor;
            }
        }

        // Kick the player "out" of the trigger.
        public void KickOut(Player player)
        {
            Touchers.Remove(player.id);
            OnLeft(player);

            // Clean up recently processed after a short delay
            Task.Delay(1000)
            .ContinueWith(_ =>
            {
                lock (_processLock)
                {
                    _recentlyProcessed.Remove(player.id);
                }
            });
        }

        public BsonDocument GetSerialized()
        {
            var doc = new BsonDocument();
            doc["_id"] = Id;
            doc["Type"] = "Finish";

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
                Cube.Rotation.Z.ToString(), // Rotation
            ];

            doc["Cube"] = cube;

            return doc;
        }
    }
}
