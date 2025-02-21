using Dalamud.Game.ClientState.Objects.Types;
using Racingway.Race.Collision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Racingway.Race
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
            Plugin = plugin;
            lastSeen = 0;
        }

        private int delayRaceline = 0;

        public void Moved(Vector3 pos)
        {
            position = pos;

            delayRaceline++;

            if (inParkour)
            {
                if (delayRaceline >= Plugin.Configuration.LineQuality)
                {
                    AddPoint();
                    delayRaceline = 0;
                }
            }

            Plugin.CheckCollision(this);
        }

        public void AddPoint()
        {
            raceLine.Enqueue(position);
        }

        public float GetDistanceTraveled()
        {
            var arrayLine = raceLine.ToArray();
            float distance = 0;

            for (var i = 1; i < raceLine.Count; i++)
            {
                if (arrayLine[i - 1] == Vector3.Zero) continue;

                distance += Vector3.Distance(arrayLine[i - 1], arrayLine[i]);
            }

            return distance;
        }
    }
}
