using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using LiteDB;
using Newtonsoft.Json;
using Racingway.Race.Collision;
using Racingway.Race.Collision.Triggers;
using Racingway.Utils;

namespace Racingway.Race
{
    /// <summary>
    /// Container for triggers making up a race.
    /// To be used to differentiate races.
    /// </summary>

    public class Route
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public Address Address { get; set; }
        public string Description { get; set; }
        public bool AllowMounts { get; set; }
        public bool Enabled { get; set; }
        public List<ITrigger> Triggers { get; set; }
        public List<Record> Records { get; set; }
        public int? ClientFails { get; set; }
        public int? ClientFinishes { get; set; }

        // Per-route cleanup settings
        public bool EnableAutoCleanup { get; set; } = false;
        public float MinTimeFilter { get; set; } = 5.0f;
        public int MaxRecordsToKeep { get; set; } = 100;
        public bool RemoveNonClientRecords { get; set; } = false;
        public bool KeepPersonalBestOnly { get; set; } = false;

        [BsonIgnore]
        public Record? BestRecord = null;

        [BsonIgnore]
        private readonly object _parkourLock = new object();

        [BsonIgnore]
        public List<(Player, Stopwatch)> PlayersInParkour = new();

        [BsonIgnore]
        public event EventHandler<Player> OnStarted = delegate { };

        [BsonIgnore]
        public event EventHandler<(Player, Record)> OnFinished = delegate { };

        [BsonIgnore]
        public event EventHandler<Player> OnFailed = delegate { };

        [BsonIgnore]
        private List<Record>? _lazyLoadedRecords = null;

        public Route(
            string name,
            Address address,
            string description,
            List<ITrigger> triggers,
            List<Record> records,
            bool allowMounts = false,
            bool enabled = true,
            int clientFails = 0,
            int clientFinishes = 0,
            bool enableAutoCleanup = false,
            float minTimeFilter = 5.0f,
            int maxRecordsToKeep = 100,
            bool removeNonClientRecords = false,
            bool keepPersonalBestOnly = false
        )
        {
            this.Id = ObjectId.NewObjectId();

            this.Name = name;
            this.Address = address;
            this.Description = description;
            this.Triggers = triggers;
            this.Records = records;
            this.AllowMounts = allowMounts;
            this.Enabled = enabled;
            this.ClientFails = clientFails;
            this.ClientFinishes = clientFinishes;

            // Auto-cleanup settings
            this.EnableAutoCleanup = enableAutoCleanup;
            this.MinTimeFilter = minTimeFilter;
            this.MaxRecordsToKeep = maxRecordsToKeep;
            this.RemoveNonClientRecords = removeNonClientRecords;
            this.KeepPersonalBestOnly = keepPersonalBestOnly;

            //this.ClientFinishes = records.Where(r => r.IsClient).Count();
        }

        public BsonDocument GetSerialized()
        {
            BsonDocument doc = new BsonDocument();
            doc["_id"] = Id;
            doc["name"] = Name;
            doc["description"] = Description;

            try
            {
                doc["address"] = Address.GetSerialized();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex.ToString());
            }

            BsonArray serializedTriggers = new BsonArray();
            Triggers.ForEach(x =>
            {
                serializedTriggers.Add(x.GetSerialized());
            });

            doc["triggers"] = serializedTriggers;
            doc["records"] = BsonMapper.Global.Serialize<List<Record>>(Records);

            doc["allowMounts"] = AllowMounts;
            doc["enabled"] = Enabled;

            doc["clientFails"] = ClientFails;
            doc["clientFinishes"] = ClientFinishes;

            // Auto-cleanup settings
            doc["enableAutoCleanup"] = EnableAutoCleanup;
            doc["minTimeFilter"] = MinTimeFilter;
            doc["maxRecordsToKeep"] = MaxRecordsToKeep;
            doc["removeNonClientRecords"] = RemoveNonClientRecords;
            doc["keepPersonalBestOnly"] = KeepPersonalBestOnly;

            return doc;
        }

        public BsonDocument GetEmptySerialized()
        {
            BsonDocument doc = new BsonDocument();
            doc["_id"] = Id;
            doc["name"] = Name;
            doc["description"] = Description;

            try
            {
                doc["address"] = Address.GetSerialized();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex.ToString());
            }

            BsonArray serializedTriggers = new BsonArray();
            Triggers.ForEach(x =>
            {
                serializedTriggers.Add(x.GetSerialized());
            });

            doc["triggers"] = serializedTriggers;
            doc["records"] = null;

            doc["allowMounts"] = AllowMounts;
            doc["enabled"] = Enabled;

            doc["clientFails"] = 0;
            doc["clientFinishes"] = 0;

            // Auto-cleanup settings
            doc["enableAutoCleanup"] = EnableAutoCleanup;
            doc["minTimeFilter"] = MinTimeFilter;
            doc["maxRecordsToKeep"] = MaxRecordsToKeep;
            doc["removeNonClientRecords"] = RemoveNonClientRecords;
            doc["keepPersonalBestOnly"] = KeepPersonalBestOnly;

            return doc;
        }

        private struct SmallRoute
        {
            public SmallRoute(string locationId, ITrigger[] triggers)
            {
                LocationId = locationId;
                Triggers = triggers;
            }

            public string LocationId;
            public ITrigger[] Triggers;
        }

        public string GetHash()
        {
            SmallRoute smallRoute = new SmallRoute(
                this.Address.LocationId,
                this.Triggers.ToArray()
            );

            string input = System.Text.Json.JsonSerializer.Serialize(smallRoute);
            string text = Compression.ToCompressedBase64(input);
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            SHA256 sha256 = SHA256.Create();
            byte[] data = sha256.ComputeHash(bytes);

            var sb = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("x2"));
            }

            return sb.ToString();
        }

        public void CheckCollision(Player player)
        {
            try
            {
                // If player is not in parkour, only check collision with start trigger
                if (Triggers.Count == 0)
                    return;

                bool isInParkour = IsPlayerInParkour(player);

                if (!AllowMounts && player.inMount)
                {
                    if (isInParkour)
                    {
                        // No need to check return value; we already know player is in parkour
                        TimeSpan elapsed;
                        RemovePlayerFromParkour(player, out elapsed);
                        Failed(player);
                    }

                    return;
                }

                if (!isInParkour && Triggers.Exists(x => x is Start))
                {
                    // For better performance, only check start trigger if player isn't already in parkour
                    // There shouldn't be more than one start trigger.
                    ITrigger start = Triggers.First(x => x is Start);
                    try
                    {
                        start.CheckCollision(player);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error(ex, "Error checking collision with Start trigger");
                    }
                }
                else
                {
                    // If player is in parkour, check all triggers in this route
                    // Use a try/catch for each trigger to prevent one bad trigger from breaking everything
                    foreach (ITrigger trigger in Triggers)
                    {
                        try
                        {
                            trigger.CheckCollision(player);
                        }
                        catch (Exception ex)
                        {
                            // Log but don't rethrow to keep checking other triggers
                            Plugin.Log.Error(
                                ex,
                                $"Error checking collision with trigger {trigger.GetType().Name}"
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't rethrow to prevent crashing
                Plugin.Log.Error(ex, "Error in CheckCollision");
            }
        }

        public void Started(Player player)
        {
            try
            {
                // Log that the started event is being fired
                Plugin.Log.Debug($"Firing OnStarted event for player {player.id}");

                // Directly invoke the event
                if (OnStarted != null)
                {
                    OnStarted(this, player);
                }
                else
                {
                    Plugin.Log.Error("OnStarted event is null - not subscribed");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in Started event");
            }
        }

        public void Finished(Player player, Record record)
        {
            try
            {
                // Don't use RunOnFrameworkThread here - let the Plugin.OnFinish method handle thread synchronization
                OnFinished?.Invoke(this, (player, record));
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in Finished event");
            }
        }

        public void Failed(Player player)
        {
            try
            {
                // Don't use RunOnFrameworkThread here - let the Plugin.OnFailed method handle thread synchronization
                OnFailed?.Invoke(this, player);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in Failed event");
            }
        }

        [BsonIgnore]
        public List<Record> GetRecordsOptimized()
        {
            // This method helps prevent performance issues when dealing with large record collections
            // By lazy loading records when needed and caching the result
            if (_lazyLoadedRecords != null)
                return _lazyLoadedRecords;

            if (Records != null)
            {
                // Sort records by time once to avoid repeated sorting operations
                _lazyLoadedRecords = Records.OrderBy(r => r.Time.TotalMilliseconds).ToList();
                return _lazyLoadedRecords;
            }

            return new List<Record>();
        }

        // Call this method to invalidate the record cache when records are modified
        public void InvalidateRecordCache()
        {
            _lazyLoadedRecords = null;
        }

        // Thread-safe methods for accessing the PlayersInParkour list
        public void AddPlayerToParkour(Player player, Stopwatch timer)
        {
            lock (_parkourLock)
            {
                // First remove the player if they're already in the list (to prevent duplicates)
                int existingIndex = PlayersInParkour.FindIndex(x => x.Item1 == player);
                if (existingIndex != -1)
                {
                    PlayersInParkour.RemoveAt(existingIndex);
                }

                // Now add them with the new timer
                PlayersInParkour.Add((player, timer));

                // Log for debugging
                Plugin.Log.Debug(
                    $"Player {player.id} added to parkour. Count: {PlayersInParkour.Count}"
                );
            }
        }

        public bool RemovePlayerFromParkour(Player player, out TimeSpan elapsed)
        {
            lock (_parkourLock)
            {
                int index = PlayersInParkour.FindIndex(x => x.Item1 == player);
                if (index != -1)
                {
                    var elapsedMs = PlayersInParkour[index].Item2.ElapsedMilliseconds;
                    elapsed = TimeSpan.FromMilliseconds(elapsedMs);
                    PlayersInParkour.RemoveAt(index);

                    // Log for debugging
                    Plugin.Log.Debug(
                        $"Player {player.id} removed from parkour. Count: {PlayersInParkour.Count}. Time: {elapsed}"
                    );
                    return true;
                }

                elapsed = TimeSpan.Zero;
                return false;
            }
        }

        public bool IsPlayerInParkour(Player player)
        {
            lock (_parkourLock)
            {
                return PlayersInParkour.FindIndex(x => x.Item1 == player) != -1;
            }
        }

        // Debugging helper
        public void DumpParkourState()
        {
            lock (_parkourLock)
            {
                Plugin.Log.Information($"=== Parkour State ===");
                Plugin.Log.Information($"Route: {Name}");
                Plugin.Log.Information($"Players in parkour: {PlayersInParkour.Count}");

                foreach (var entry in PlayersInParkour)
                {
                    Plugin.Log.Information(
                        $"  Player ID: {entry.Item1.id}, Time: {entry.Item2.ElapsedMilliseconds}ms"
                    );
                }

                Plugin.Log.Information($"===================");
            }
        }
    }
}
