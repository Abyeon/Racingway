using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LiteDB;

namespace Racingway.Race.Collision.Triggers
{
    public interface ITrigger
    {
        public ObjectId Id { get; set; }
        public Route Route { get; set; }
        public Cube Cube { get; set; }
        public uint Color { get; set; }
        public bool Active { get; set; }
        public List<uint> Touchers { get; set; }

        public void CheckCollision(Player player)
        {
            var inTrigger = Cube.PointInCube(player.position);
            if (inTrigger && !Touchers.Contains(player.id))
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

        public BsonDocument GetSerialized();
        public void OnEntered(Player player);
        public void OnLeft(Player player);
    }
}
