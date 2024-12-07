using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Racingway.Utils
{
    public class Player
    {
        private Plugin Plugin;

        public uint id;
        public IGameObject actor;
        public Vector3 position = Vector3.Zero;
        public Queue<Vector3> raceLine = new Queue<Vector3>();
        public Stopwatch timer = new Stopwatch();

        public int lastSeen;

        public bool inParkour = false;

        public Player(uint id, IGameObject actor, Plugin plugin)
        {
            this.id = id;
            this.actor = actor;
            this.Plugin = plugin;
            this.lastSeen = 0;
        }

        private int delayRaceline = 0;

        public void Moved(Vector3 pos)
        {
            this.position = pos;

            delayRaceline++;

            if (inParkour)
            {
                if (delayRaceline >= 10)
                {
                    raceLine.Enqueue(pos);

                    //if (raceLine.Count > 20)
                    //{
                    //    //Plugin.Log.Debug($"Player {actor.Name} moved to {pos.ToString()}");
                    //    raceLine.Dequeue();
                    //}

                    delayRaceline = 0;
                }
            }

            foreach (Trigger trigger in Plugin.triggers)
            {
                trigger.CheckCollision(this);
            }
        }
    }
}
