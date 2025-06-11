using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using Racingway.Race.Collision;

namespace Racingway.Race
{
    public class Player
    {
        private Plugin? Plugin;

        public uint id;
        public ICharacter actor;

        public string Name;
        public string Homeworld;
        public uint HomeworldRow;
        public bool isClient = false;

        public Vector3 position = Vector3.Zero;

        // Using a bounded collection to limit memory usage
        private const int DEFAULT_MAX_LINE_POINTS = 1000;

        // Store ONLY raw points - no processing
        private List<TimedVector3> _rawPoints = new List<TimedVector3>();

        // Store the finalized line segments that never change
        private List<LineSegment> _finalLine = new List<LineSegment>();

        // Store the last point so we can create a new segment
        private TimedVector3 _lastFinalPoint;
        private bool _hasLastPoint = false;

        // Public property to maintain compatibility with existing code
        public IEnumerable<TimedVector3> RaceLine => _rawPoints;

        public List<TimedCheckpoint> currentSplits = new List<TimedCheckpoint>();

        public Stopwatch timer = new Stopwatch();
        public int lastSeen;

        public int lapsFinished = 0;

        public bool inParkour = false;
        public bool isGrounded = true;
        public bool inMount = false;

        // Tracking last point to avoid unnecessary additions
        private Vector3 lastAddedPosition = Vector3.Zero;
        private const float MIN_DISTANCE_THRESHOLD = 1.5f;
        private const float MIN_LANDING_THRESHOLD = 0.5f;

        public Player(uint id, ICharacter actor, Plugin plugin)
        {
            this.id = id;
            this.actor = actor;

            IPlayerCharacter? playerCharacter = actor as IPlayerCharacter;
            if (playerCharacter != null)
            {
                this.Name = playerCharacter.Name.ToString();
                this.Homeworld = playerCharacter.HomeWorld.Value.Name.ToString();
                this.HomeworldRow = playerCharacter.HomeWorld.Value.RowId;
                this.isClient = playerCharacter.Equals(Plugin.ClientState.LocalPlayer);
            }

            Plugin = plugin;
            lastSeen = 0;
            _lastFinalPoint = new TimedVector3(Vector3.Zero, 0);
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

                bool nowGrounded = !character->IsJumping();

                // Player landed or left ground
                if (this.inParkour && this.isGrounded != nowGrounded)
                {
                    if (lastAddedPosition == Vector3.Zero || Vector3.Distance(lastAddedPosition, this.position) >= MIN_LANDING_THRESHOLD)
                    {
                        // Add a point
                        AddPoint();
                        lastAddedPosition = this.position;
                    }
                }
                
                this.isGrounded = nowGrounded;
                this.inMount = character->IsMounted();
            }
            catch (NullReferenceException e)
            {
                if (Plugin != null)
                {
                    Plugin.Log.Error("Error updating player states. " + e.ToString());
                    Plugin.ChatGui.PrintError("Error updating player states. See /xllog");
                }
            }
        }

        public void Moved(Vector3 pos)
        {
            this.position = pos;

            delayRaceline++;

            if (inParkour && Plugin != null)
            {
                if (delayRaceline >= Plugin.Configuration.LineQuality)
                {
                    // Only add points if we've moved a sufficient distance
                    if (lastAddedPosition == Vector3.Zero || Vector3.Distance(lastAddedPosition, this.position) >= MIN_DISTANCE_THRESHOLD)
                    {
                        AddPoint();
                        lastAddedPosition = this.position;
                    }
                    delayRaceline = 0;
                }
            }

            if (Plugin != null)
            {
                Plugin.CheckCollision(this);
            }
        }

        public void AddPoint()
        {
            // Create the new point
            TimedVector3 newPoint = new TimedVector3(this.position, timer.ElapsedMilliseconds);

            // Add to raw points list (for distance calculation, etc.)
            _rawPoints.Add(newPoint);

            // Limit the raw points list size to prevent memory issues
            int maxPoints = DEFAULT_MAX_LINE_POINTS;
            if (Plugin != null && Plugin.Configuration.MaxLinePoints > 0)
            {
                maxPoints = Plugin.Configuration.MaxLinePoints;
            }

            if (_rawPoints.Count > maxPoints)
            {
                _rawPoints.RemoveAt(0);
            }

            // If this is our first point, just store it
            if (!_hasLastPoint)
            {
                _lastFinalPoint = newPoint;
                _hasLastPoint = true;
                return;
            }

            // Create a new line segment from last point to this point
            // This segment will NEVER change once added
            _finalLine.Add(new LineSegment(_lastFinalPoint, newPoint));

            // Update last point
            _lastFinalPoint = newPoint;

            // Limit total number of line segments if needed
            if (_finalLine.Count > maxPoints - 1)
            {
                _finalLine.RemoveAt(0);

                // If we removed the first segment, update the source point of the next segment
                if (_finalLine.Count > 0)
                {
                    _lastFinalPoint = _finalLine[0].Source;
                }
            }
        }

        public float GetDistanceTraveled()
        {
            float distance = 0;

            for (var i = 1; i < _rawPoints.Count; i++)
            {
                if (_rawPoints[i - 1].asVector() == Vector3.Zero)
                    continue;

                distance += Vector3.Distance(
                    _rawPoints[i - 1].asVector(),
                    _rawPoints[i].asVector()
                );
            }

            return distance;
        }

        // Get the vector array for drawing - returning an array of points for the line
        public TimedVector3[] GetLineForDrawing()
        {
            if (_finalLine.Count == 0 && !_hasLastPoint)
                return new TimedVector3[0];

            // Build list of points from our finalized segments
            List<TimedVector3> points = new List<TimedVector3>();

            // Add first point if we have one
            if (_finalLine.Count > 0)
            {
                points.Add(_finalLine[0].Source);
            }
            else if (_hasLastPoint)
            {
                points.Add(_lastFinalPoint);
                return points.ToArray();
            }

            // Add all destination points from the segments
            foreach (var segment in _finalLine)
            {
                points.Add(segment.Destination);
            }

            // Finally add a point at the players feet to make it "smooth"
            if (inParkour)
                points.Add(new TimedVector3(position, timer.ElapsedMilliseconds));

            return points.ToArray();
        }

        // Clear the line and all tracking data
        public void ClearLine()
        {
            _rawPoints.Clear();
            _finalLine.Clear();
            _hasLastPoint = false;
            lastAddedPosition = Vector3.Zero;
        }
    }

    // A line segment that never changes once created
    public struct LineSegment
    {
        public TimedVector3 Source { get; }
        public TimedVector3 Destination { get; }

        public LineSegment(TimedVector3 source, TimedVector3 destination)
        {
            Source = source;
            Destination = destination;
        }
    }
}
