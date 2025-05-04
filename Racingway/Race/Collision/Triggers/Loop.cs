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
            try
            {
                // Check if player is in the trigger area
                var inTrigger = Cube.PointInCube(player.position);

                // Initialize player state if not already tracked
                if (!playerState.ContainsKey(player.id))
                {
                    playerState[player.id] = RaceState.NotStarted;
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
            catch (Exception ex)
            {
                Racingway.Plugin.Log.Error(ex, "Error in Loop.CheckCollision");
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
            try
            {
                // Set player state first
                playerState[player.id] = RaceState.Running;

                // Start the player timer
                player.timer.Reset(); // Reset first to ensure accurate timing
                player.timer.Start();
                player.inParkour = true;
                player.AddPoint();

                // Check if this is the local player
                bool isLocalPlayer = false;
                try
                {
                    isLocalPlayer = player.actor == Racingway.Plugin.ClientState.LocalPlayer;
                }
                catch (Exception ex)
                {
                    Racingway.Plugin.Log.Error(ex, "Error checking if player is local player");
                }

                // Store the route locally to avoid race conditions
                Route routeRef = Route;

                // Add to the players in parkour list using the thread-safe method
                routeRef.AddPlayerToParkour(player, Stopwatch.StartNew());

                // Important: Clear the race line first before starting
                player.raceLine.Clear();

                // Fire the started event directly - we're letting the event handler
                // manage thread safety to avoid excessive delays in event propagation
                routeRef.Started(player);

                // Log for debugging
                Racingway.Plugin.Log.Debug(
                    $"Started race for player {player.id}. Is local player: {isLocalPlayer}"
                );
            }
            catch (Exception ex)
            {
                Racingway.Plugin.Log.Error(ex, "Error in StartRace");
            }
        }

        private void CompleteRace(Player player)
        {
            try
            {
                // Store the route reference to prevent race conditions
                Route routeRef = Route;

                // Check if this is the local player
                bool isLocalPlayer = false;
                try
                {
                    isLocalPlayer = player.actor == Racingway.Plugin.ClientState.LocalPlayer;
                }
                catch
                {
                    // Ignore if we can't check local player status
                }

                // Try to remove the player from parkour tracking and get elapsed time
                TimeSpan t;
                if (routeRef.RemovePlayerFromParkour(player, out t))
                {
                    // Update player state
                    player.AddPoint();
                    player.inParkour = false;

                    // Get the distance and record the final race stats
                    var distance = player.GetDistanceTraveled();

                    // Stop timer
                    player.timer.Stop();
                    player.timer.Reset();

                    // Log completion for local player
                    if (isLocalPlayer)
                    {
                        Racingway.Plugin.Log.Debug($"Local player race completed. Time: {t}");

                        // Print directly to chat for local player
                        string prettyTime = Racingway.Utils.Time.PrettyFormatTimeSpan(t);
                        Racingway.Plugin.ChatGui.Print(
                            $"[RACE] You completed {routeRef.Name} in {prettyTime}!"
                        );
                    }

                    // Log for debugging
                    Racingway.Plugin.Log.Debug(
                        $"Completed race for player {player.id} in {t}. Is local player: {isLocalPlayer}"
                    );

                    try
                    {
                        // Create a record immediately
                        IPlayerCharacter actor = (IPlayerCharacter)player.actor;
                        Record record = new Record
                        {
                            Date = DateTime.UtcNow,
                            Name = actor.Name.ToString(),
                            World = actor.HomeWorld.Value.Name.ToString(),
                            Time = t,
                            Distance = distance,
                            Line = player.GetRaceLineAsVectorArray(),
                            RouteId = routeRef.Id.ToString(),
                            RouteHash = routeRef.GetHash(),
                        };

                        if (isLocalPlayer)
                        {
                            record.IsClient = true;
                        }

                        // Fire the finished event directly - let event handlers manage thread safety
                        routeRef.Finished(player, record);

                        // Reset the player's state
                        playerState[player.id] = RaceState.NotStarted;
                    }
                    catch (Exception ex)
                    {
                        Racingway.Plugin.Log.Error(
                            ex,
                            "Error creating record or firing finished event"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Racingway.Plugin.Log.Error(ex, "Error in CompleteRace");
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
