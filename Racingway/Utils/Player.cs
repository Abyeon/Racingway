using Dalamud.Game.ClientState.Objects.Types;
using Racingway.Collision;
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
                if (delayRaceline >= 5)
                {
                    raceLine.Enqueue(pos);

                    delayRaceline = 0;
                }
            }

            Plugin.Logic.CheckCollision(this);
        }

        public float GetDistanceTraveled()
        {
            Vector3[] arrayLine = raceLine.ToArray();
            float distance = 0;

            for (int i = 1; i < raceLine.Count; i++)
            {
                if (arrayLine[i - 1] == Vector3.Zero) continue;

                distance += Vector3.Distance(arrayLine[i - 1], arrayLine[i]);
            }

            return distance;
        }
    }
}
