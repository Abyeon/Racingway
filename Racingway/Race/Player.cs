using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using Racingway.Race.Collision;
using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Racingway.Race
{
    public class Player
    {
        private Plugin? Plugin;

        public uint id;
        public ICharacter actor;
        public Vector3 position = Vector3.Zero;
        public Queue<TimedVector3> raceLine = new Queue<TimedVector3>();
        public Stopwatch timer = new Stopwatch();

        public int lastSeen;

        public bool inParkour = false;
        public bool isGrounded = true;
        public bool inMount = false;

        public Player(uint id, ICharacter actor, Plugin plugin)
        {
            this.id = id;
            this.actor = actor;
            Plugin = plugin;
            lastSeen = 0;
        }

        private int delayRaceline = 0;

        public unsafe void UpdateState()
        {
            try
            {
                if (!actor.IsValid())
                {
                    throw new NullReferenceException("Actor is not valid in memory.");
                }

                if (actor == null)
                {
                    throw new NullReferenceException("Actor is null.");
                }

                Character* character = (Character*)actor.Address;
                if (character == null)
                {
                    throw new NullReferenceException("Character pointer is null");
                }

                this.isGrounded = !character->IsJumping();
                this.inMount = character->IsMounted();
            } catch (NullReferenceException e)
            {
                Plugin.Log.Error("Error updating player states. " + e.ToString());
                Plugin.ChatGui.PrintError("Error updating player states. See /xllog");
            }
        }

        public void Moved(Vector3 pos)
        {
            this.position = pos;

            delayRaceline++;

            if (inParkour)
            {
                if (delayRaceline >= Plugin.Configuration.LineQuality)
                {
                    AddPoint();
                    delayRaceline = 0;
                }
            }

            UpdateState();
            Plugin.CheckCollision(this);
        }

        public void AddPoint()
        {
            raceLine.Enqueue(new TimedVector3(this.position, timer.ElapsedMilliseconds));
        }

        public float GetDistanceTraveled()
        {
            var arrayLine = raceLine.ToArray();
            float distance = 0;

            for (var i = 1; i < raceLine.Count; i++)
            {
                if (arrayLine[i - 1].asVector() == Vector3.Zero) continue;

                distance += Vector3.Distance(arrayLine[i - 1].asVector(), arrayLine[i].asVector());
            }

            return distance;
        }
    }
}
