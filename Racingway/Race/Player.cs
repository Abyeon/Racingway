using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using Racingway.Race.Collision;
using Racingway.Utils;

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

        private DateTime _lastMoveCheck = DateTime.MinValue;
        private static readonly TimeSpan MOVE_THROTTLE = TimeSpan.FromMilliseconds(20);
        private readonly object _moveLock = new object();

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
            }
            catch (NullReferenceException e)
            {
                Plugin.Log.Error("Error updating player states. " + e.ToString());
                Plugin.ChatGui.PrintError("Error updating player states. See /xllog");
            }
        }

        public void Moved(Vector3 pos)
        {
            try
            {
                // Throttle frequent position updates to avoid overwhelming the system
                DateTime now = DateTime.Now;
                if ((now - _lastMoveCheck) < MOVE_THROTTLE)
                {
                    // Skip this update if it's too soon after the last one
                    return;
                }

                // Use a lock to prevent concurrent access issues
                lock (_moveLock)
                {
                    _lastMoveCheck = now;
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

                    // Only check collisions if absolutely necessary to improve performance
                    try
                    {
                        Plugin.CheckCollision(this);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error(ex, "Error checking collision in Moved method");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in Player.Moved");
            }
        }

        public void AddPoint()
        {
            // Add point to race line with timestamp
            raceLine.Enqueue(new TimedVector3(this.position, timer.ElapsedMilliseconds));

            // Limit memory usage by keeping only the most recent 2000 points
            // (this won't affect distance calculations but prevents excessive memory usage)
            if (raceLine.Count > 2000)
            {
                raceLine.Dequeue();
            }
        }

        public float GetDistanceTraveled()
        {
            var arrayLine = raceLine.ToArray();
            float distance = 0;

            for (var i = 1; i < raceLine.Count; i++)
            {
                if (arrayLine[i - 1].asVector() == Vector3.Zero)
                    continue;

                distance += Vector3.Distance(arrayLine[i - 1].asVector(), arrayLine[i].asVector());
            }

            return distance;
        }

        // Convert TimedVector3 queue to standard Vector3 array for database storage
        public Vector3[] GetRaceLineAsVectorArray()
        {
            return raceLine.Select(tv => tv.asVector()).ToArray();
        }
    }
}
