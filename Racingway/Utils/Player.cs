using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    public class Player
    {
        private Plugin Plugin;

        public uint id;
        public IGameObject actor;
        public Vector3 position = Vector3.Zero;
        public Queue<Vector3> raceLine = new Queue<Vector3>();
        public List<Trigger> touchedTriggers = new List<Trigger>();

        public bool inParkour = false;

        public Player(uint id, IGameObject actor, Plugin plugin)
        {
            this.id = id;
            this.actor = actor;
            this.Plugin = plugin;
        }

        public void Moved(Vector3 pos)
        {
            // Check with colliders before setting position value
            
            this.position = pos;
            raceLine.Enqueue(pos);

            if (raceLine.Count > 100)
            {
                Plugin.Log.Debug($"Player {actor.Name} moved to {pos.ToString()}");
                raceLine.Dequeue();
            }

            CheckCollision(pos);
        }

        public void CheckCollision(Vector3 pos)
        {
            // Later we will sort triggers by player distance and stop when we hit one
            foreach (Trigger trigger in Plugin.triggers)
            {
                Vector3 min = Vector3.Min(trigger.min, trigger.max);
                Vector3 max = Vector3.Max(trigger.min, trigger.max);

                if (pos.X > min.X && pos.Y > min.Y && pos.Z > min.Z && pos.X < max.X && pos.Y < max.Y && pos.Z < max.Z)
                {
                    if (touchedTriggers.Contains(trigger)) continue;

                    touchedTriggers.Add(trigger);
                    trigger.Entered(this);
                } else if(touchedTriggers.Contains(trigger))
                {
                    touchedTriggers.Remove(trigger);
                    trigger.Left(this);
                }
            }
        }
    }
}
