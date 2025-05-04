using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
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

        // Track the race state for each player
        private Dictionary<uint, RaceState> playerState = new Dictionary<uint, RaceState>();

        private enum RaceState
        {
            NotStarted, // Player has not started the race
            Running, // Player is currently in a race
            Completed // Player has completed the race
            ,
        }

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

            // Initialize player state if not already tracked
            if (!playerState.ContainsKey(player.id))
            {
                playerState[player.id] = inTrigger ? RaceState.NotStarted : RaceState.NotStarted;
            }

            // Handle state changes based on player position
            if (inTrigger)
            {
                // Player is inside the trigger
                if (!Touchers.Contains(player.id))
                {
                    Touchers.Add(player.id);
                    HandlePlayerEnteredTrigger(player);
                }
            }
            else
            {
                // Player is outside the trigger
                if (Touchers.Contains(player.id))
                {
                    Touchers.Remove(player.id);
                    HandlePlayerLeftTrigger(player);
                }
            }
        }

        private void HandlePlayerEnteredTrigger(Player player)
        {
            Color = ActiveColor;

            // Only complete the race if the player is currently in a running race
            if (playerState[player.id] == RaceState.Running)
            {
                CompleteRace(player);
            }
        }

        private void HandlePlayerLeftTrigger(Player player)
        {
            if (Touchers.Count == 0)
            {
                Color = InactiveColor;
            }

            // Only start the race if the player has not already started or completed
            if (playerState[player.id] == RaceState.NotStarted)
            {
                StartRace(player);
            }
        }

        private void StartRace(Player player)
        {
            playerState[player.id] = RaceState.Running;

            player.timer.Start();
            player.inParkour = true;
            player.AddPoint();

            Route.PlayersInParkour.Add((player, Stopwatch.StartNew()));
            Route.Started(player);

            player.raceLine.Clear();
        }

        private void CompleteRace(Player player)
        {
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

                try
                {
                    IPlayerCharacter actor = (IPlayerCharacter)player.actor;
                    Record record = new Record(
                        DateTime.UtcNow,
                        actor.Name.ToString(),
                        actor.HomeWorld.Value.Name.ToString(),
                        t,
                        distance,
                        player.raceLine.ToArray(),
                        this.Route
                    );

                    if (actor == Plugin.ClientState.LocalPlayer)
                    {
                        record.IsClient = true;
                    }

                    Route.Finished(player, record);

                    // Reset the player's state to not started
                    playerState[player.id] = RaceState.NotStarted;
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex.ToString());
                }
            }
        }

        // Interface methods that need implementation
        public void OnEntered(Player player)
        {
            // This is handled by HandlePlayerEnteredTrigger
        }

        public void OnLeft(Player player)
        {
            // This is handled by HandlePlayerLeftTrigger
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
