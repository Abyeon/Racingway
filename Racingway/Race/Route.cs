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

        // Route cleanup settings
        public bool AutoCleanupEnabled { get; set; }
        public int MaxRecordsToKeep { get; set; }
        public int KeepTopNRecords { get; set; }
        public bool DeleteOldRecordsEnabled { get; set; } // Whether to delete records based on age
        public int MaxDaysToKeep { get; set; }
        public bool KeepPersonalBests { get; set; }
        public bool FilterByTimeEnabled { get; set; }
        public float MinTimeThreshold { get; set; } // Minimum time in seconds to keep a record
        public float MaxTimeThreshold { get; set; } // Maximum time in seconds to keep a record, 0 = no limit

        [BsonIgnore]
        public Record? BestRecord = null;

        [BsonIgnore]
        public List<(Player, Stopwatch)> PlayersInParkour = new();

        [BsonIgnore]
        public event EventHandler<Player> OnStarted;

        [BsonIgnore]
        public event EventHandler<(Player, Record)> OnFinished;

        [BsonIgnore]
        public event EventHandler<Player> OnFailed;

        public Route(
            string name,
            Address address,
            string description,
            List<ITrigger> triggers,
            List<Record> records,
            bool allowMounts = false,
            bool enabled = true,
            int clientFails = 0,
            int clientFinishes = 0
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

            // Default cleanup settings
            this.AutoCleanupEnabled = false;
            this.MaxRecordsToKeep = 100;
            this.KeepTopNRecords = 10;
            this.DeleteOldRecordsEnabled = false; // By default, don't delete based on age
            this.MaxDaysToKeep = 30;
            this.KeepPersonalBests = true;
            this.FilterByTimeEnabled = false;
            this.MinTimeThreshold = 5.0f; // Default 5 seconds minimum time
            this.MaxTimeThreshold = 0.0f; // Default no maximum time limit
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

            // Add cleanup settings
            doc["autoCleanupEnabled"] = AutoCleanupEnabled;
            doc["maxRecordsToKeep"] = MaxRecordsToKeep;
            doc["keepTopNRecords"] = KeepTopNRecords;
            doc["deleteOldRecordsEnabled"] = DeleteOldRecordsEnabled;
            doc["maxDaysToKeep"] = MaxDaysToKeep;
            doc["keepPersonalBests"] = KeepPersonalBests;
            doc["filterByTimeEnabled"] = FilterByTimeEnabled;
            doc["minTimeThreshold"] = MinTimeThreshold;
            doc["maxTimeThreshold"] = MaxTimeThreshold;

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

            // Add cleanup settings
            doc["autoCleanupEnabled"] = AutoCleanupEnabled;
            doc["maxRecordsToKeep"] = MaxRecordsToKeep;
            doc["keepTopNRecords"] = KeepTopNRecords;
            doc["deleteOldRecordsEnabled"] = DeleteOldRecordsEnabled;
            doc["maxDaysToKeep"] = MaxDaysToKeep;
            doc["keepPersonalBests"] = KeepPersonalBests;
            doc["filterByTimeEnabled"] = FilterByTimeEnabled;
            doc["minTimeThreshold"] = MinTimeThreshold;
            doc["maxTimeThreshold"] = MaxTimeThreshold;

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
            // If player is not in parkour, only check collision with start trigger
            if (Triggers.Count == 0)
                return;

            int index = PlayersInParkour.FindIndex(x => x.Item1 == player);

            if (!AllowMounts && player.inMount)
            {
                if (index != -1)
                {
                    PlayersInParkour.RemoveAt(index);
                    Failed(player);
                }

                return;
            }

            if (index == -1 && Triggers.Exists(x => x is Start))
            {
                // There shouldnt be more than one start trigger.
                // Only check start trigger if player is not in parkour
                ITrigger start = Triggers.First(x => x is Start);
                start.CheckCollision(player);
            }
            else
            {
                // If player is in parkour, only check relevant triggers
                // First, check fail triggers as they're most important for race integrity
                foreach (ITrigger trigger in Triggers.Where(t => t is Fail))
                {
                    trigger.CheckCollision(player);

                    // If player is no longer in parkour after checking a fail trigger, stop further checks
                    if (PlayersInParkour.FindIndex(x => x.Item1 == player) == -1)
                        return;
                }

                // Then check finish and checkpoint triggers (higher priority)
                foreach (
                    ITrigger trigger in Triggers.Where(t =>
                        t is Finish || t is Checkpoint || t is Loop
                    )
                )
                {
                    trigger.CheckCollision(player);

                    // If player is no longer in parkour after a finish trigger, stop checking
                    if (PlayersInParkour.FindIndex(x => x.Item1 == player) == -1)
                        return;
                }

                // Skip checking start trigger for players already in parkour
            }
        }

        public void Started(Player player)
        {
            OnStarted?.Invoke(this, player);
        }

        public void Finished(Player player, Record record)
        {
            OnFinished?.Invoke(this, (player, record));
        }

        public void Failed(Player player)
        {
            OnFailed?.Invoke(this, player);
        }

        /// <summary>
        /// Applies cleanup rules to this route's records based on configuration
        /// </summary>
        /// <returns>The number of records removed</returns>
        public int ApplyCleanupRules()
        {
            if (!AutoCleanupEnabled || Records == null || Records.Count == 0)
                return 0;

            int originalCount = Records.Count;

            // First, filter out records that don't meet time thresholds (if enabled)
            // This ensures time filtering is applied to ALL records regardless of other criteria
            if (FilterByTimeEnabled)
            {
                List<Record> timeFilteredRecords = new List<Record>();
                foreach (var record in Records)
                {
                    float recordTimeSeconds = (float)record.Time.TotalSeconds;
                    bool aboveMinTime = recordTimeSeconds >= MinTimeThreshold;
                    bool belowMaxTime =
                        MaxTimeThreshold <= 0 || recordTimeSeconds <= MaxTimeThreshold;

                    if (aboveMinTime && belowMaxTime)
                    {
                        timeFilteredRecords.Add(record);
                    }
                }
                Records = timeFilteredRecords;

                // If we've removed all records, return early to avoid issues
                if (Records.Count == 0)
                    return originalCount;
            }

            // Get all personal bests (best time per player)
            Dictionary<string, Record> personalBests = new Dictionary<string, Record>();
            if (KeepPersonalBests)
            {
                foreach (var playerGroup in Records.GroupBy(r => r.Name))
                {
                    Record best = playerGroup.OrderBy(r => r.Time).First();
                    personalBests[playerGroup.Key] = best;
                }
            }

            // Sort by time (ascending)
            Records.Sort((a, b) => a.Time.CompareTo(b.Time));

            // Keep top N records
            HashSet<ObjectId> recordsToKeep = new HashSet<ObjectId>();
            for (int i = 0; i < Math.Min(KeepTopNRecords, Records.Count); i++)
            {
                recordsToKeep.Add(Records[i].Id);
            }

            // Add personal bests to keep list
            if (KeepPersonalBests)
            {
                foreach (var best in personalBests.Values)
                {
                    recordsToKeep.Add(best.Id);
                }
            }

            // Calculate date cutoff only if we're removing old records
            DateTime? cutoffDate = null;
            if (DeleteOldRecordsEnabled)
            {
                cutoffDate = DateTime.Now.AddDays(-MaxDaysToKeep);
            }

            // Apply remaining filters
            List<Record> filteredRecords = new List<Record>();
            foreach (var record in Records)
            {
                // Apply date filtering if enabled
                bool meetsDateThreshold = true;
                if (DeleteOldRecordsEnabled && cutoffDate.HasValue)
                {
                    meetsDateThreshold = record.Date >= cutoffDate.Value;
                }

                // Keep if:
                // 1. It's in our "keep" list (top N or personal best), OR
                // 2. It meets all criteria:
                //    - Meets date threshold (if enabled)
                //    - We haven't exceeded the max records to keep
                if (
                    recordsToKeep.Contains(record.Id)
                    || (meetsDateThreshold && filteredRecords.Count < MaxRecordsToKeep)
                )
                {
                    filteredRecords.Add(record);
                }
            }

            // Update records list
            Records = filteredRecords;

            // Return number of removed records
            return originalCount - Records.Count;
        }
    }
}
