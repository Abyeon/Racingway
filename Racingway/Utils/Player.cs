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
                raceLine.Dequeue();
            }
        }

        public void CheckCollision(Vector3 prevPos, Vector3 currPos)
        {
            // Do magic
        }
    }
}
