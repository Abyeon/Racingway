using System.Collections.Generic;
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
        public uint? FlagIcon { get; set; }

        public void CheckCollision(Player player)
        {
            var inTrigger = Cube.PointInCube(player.position);

            // Quick return if not in trigger and not a toucher
            if (!inTrigger && !Touchers.Contains(player.id))
                return;

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
