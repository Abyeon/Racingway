using ImGuiNET;
using LiteDB;
using Racingway.Race.Collision;
using Racingway.Race.Collision.Triggers;
using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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
        public bool AllowMounts {  get; set; }
        public bool Enabled { get; set; }
        public List<ITrigger> Triggers { get; set; }

        [BsonIgnore] public Record? BestRecord = null;

        [BsonIgnore] public List<(Player, Stopwatch)> PlayersInParkour = new();

        [BsonIgnore] public event EventHandler<Player> OnStarted;
        [BsonIgnore] public event EventHandler<(Player, Record)> OnFinished;
        [BsonIgnore] public event EventHandler<Player> OnFailed;

        public Route(string name, Address address, string description, List<ITrigger> triggers, bool allowMounts = false, bool enabled = true)
        {
            this.Id = ObjectId.NewObjectId();

            this.Name = name;
            this.Address = address;
            this.Description = description;
            this.Triggers = triggers;
            this.AllowMounts = allowMounts;
            this.Enabled = enabled;
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
            } catch (Exception ex)
            {
                Plugin.Log.Error(ex.ToString());
            }

            BsonArray serializedTriggers = new BsonArray();
            Triggers.ForEach(x =>
            {
                serializedTriggers.Add(x.GetSerialized());
            });

            doc["triggers"] = serializedTriggers;

            doc["allowMounts"] = AllowMounts;
            doc["enabled"] = Enabled;

            return doc;
        }

        public string GetHash()
        {
            string input = JsonSerializer.Serialize(this.GetSerialized());
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
            if (Triggers.Count == 0) return;

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
                ITrigger start = Triggers.First(x => x is Start);
                start.CheckCollision(player);
            } else
            {
                // If player is in parkour, check all triggers in this route
                foreach (ITrigger trigger in Triggers)
                {
                    trigger.CheckCollision(player);
                }
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
    }
}
