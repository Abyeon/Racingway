using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using LiteDB;

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
        public bool Active { get; set; } = false;
        public List<uint> Touchers { get; set; } = new List<uint>();
        public Dictionary<uint, bool> playerStarted = new Dictionary<uint, bool>();

        public uint? FlagIcon { get; set; } = 63929;

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

            // Quick return if not in trigger and not a toucher
            if (!inTrigger && !Touchers.Contains(player.id))
                return;

            // True if grounded or grounded is not required by route
            bool grounded = (player.isGrounded && Route.RequireGroundedStart) || !Route.RequireGroundedStart;

            // When player enters trigger and wasn't already inside
            if (inTrigger && !Touchers.Contains(player.id) && grounded)
            {
                Touchers.Add(player.id);
                OnEntered(player);
            }
            // When player leaves trigger and was inside
            else if (
                (!inTrigger && Touchers.Contains(player.id)) ||
                (!grounded && Touchers.Contains(player.id)))
            {
                Touchers.Remove(player.id);
                OnLeft(player);
            }
        }

        public void OnEntered(Player player)
        {
            Active = true;
            Color = ActiveColor;

            // If the player is returning to the trigger after starting the race
            if (playerStarted.ContainsKey(player.id) && playerStarted[player.id])
            {
                // Do not process if player hasnt hit all checkpoints
                if (Route.RequireAllCheckpoints)
                {
                    int totalCheckpoints = Route.Triggers.Where(x => x is Checkpoint).Count();
                    int hitCheckpoints = player.currentSplits.Count - (player.lapsFinished * totalCheckpoints);

                    // Player failed
                    if (hitCheckpoints != totalCheckpoints)
                    {
                        playerStarted[player.id] = false;
                        Route.Failed(player);
                        player.timer.Reset();
                        player.currentSplits.Clear();
                        player.lapsFinished = 0;
                        player.ClearLine();

                        return;
                    } else
                    {
                        // Increment lap if necessary
                        if (Route.Laps > 1)
                        {
                            player.lapsFinished++;
                            
                            // Dont process any more if the player needs to complete more laps
                            if (player.lapsFinished != Route.Laps)
                            {
                                // Reset player's splits by removing reference to each checkpoint
                                // This seems stupid, but logically it should work
                                for (int i = 0; i < player.currentSplits.Count; i++)
                                {
                                    player.currentSplits[i] = new TimedCheckpoint(null, player.currentSplits[i].offset);
                                }

                                Route.FinishedLap(player);

                                return;
                            } else
                            {
                                // Reset player laps
                                player.lapsFinished = 0;
                            }
                        }
                    }
                }

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
                        Record record = new Record(
                            now,
                            player.Name,
                            player.Homeworld,
                            t,
                            distance,
                            player.RaceLine.ToArray(),
                            this.Route
                        );

                        List<long> splits = new List<long>();
                        for (int i = 0; i < player.currentSplits.Count; i++)
                        {
                            splits.Add(player.currentSplits[i].offset);
                        }

                        record.Splits = splits.ToArray();
                        record.IsClient = player.isClient;

                        Route.Finished(player, record);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error(ex.ToString());
                    }
                }
            }

            player.ClearLine();
        }

        public void OnLeft(Player player)
        {
            if (Touchers.Count == 0)
            {
                Active = false;
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

                player.ClearLine();
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
