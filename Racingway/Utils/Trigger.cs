using Dalamud.Game.ClientState.Objects.SubKinds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    public class Trigger
    {
        public Cube cube;

        private static readonly uint InactiveColor = 0x22FF3061;
        private static readonly uint ActiveColor = 0x2200FF00;

        public uint color = InactiveColor;
        public bool isTouched = false;
        public enum TriggerType
        {
            Start,
            Fail,
            Checkpoint,
            Finish
        }

        public TriggerType selectedType = TriggerType.Checkpoint;

        private int touching = 0;
        private List<uint> touchers = new List<uint>();

        public Trigger(Vector3 position, Vector3 scale, Vector3 rotation) 
        {
            this.cube = new Cube(position, scale, rotation);
        }

        public void CheckCollision(Player player)
        {
            bool inTrigger = cube.PointInCube(player.position);

            if (inTrigger && !touchers.Contains(player.id))
            {
                touchers.Add(player.id);
                this.Entered(player);
            }
            else if (!inTrigger && touchers.Contains(player.id))
            {
                touchers.Remove(player.id);
                this.Left(player);
            }
        }

        public void Entered(Player player)
        {
            Plugin.Log.Debug($"{player.actor.Name} entered trigger");
            touching++;
            color = ActiveColor;

            IPlayerCharacter actor = (IPlayerCharacter)player.actor;
            DateTime now = DateTime.UtcNow;

            switch (selectedType)
            {
                case TriggerType.Fail:
                    if (!player.inParkour) break;

                    // Player failed parkour
                    player.raceLine.Clear();
                    player.inParkour = false;
                    Plugin.ChatGui.Print($"{now.ToString("M/dd H:mm:ss")} {actor.Name} {actor.HomeWorld.Value.Name} just failed the parkour.");
                    player.timer.Reset();
                    break;
                case TriggerType.Checkpoint:
                    // Player hit checkpoint
                    break;
                case TriggerType.Finish:
                    // Player finished parkour
                    if (player.inParkour)
                    {
                        player.inParkour = false;

                        long elapsedTime = player.timer.ElapsedMilliseconds;
                        TimeSpan t = TimeSpan.FromMilliseconds(elapsedTime);

                        //String prettyPrint = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                        //    t.Hours,
                        //    t.Minutes,
                        //    t.Seconds,
                        //    t.Milliseconds);

                        String prettyPrint = Time.PrettyFormatTimeSpan(t);

                        float distance = player.GetDistanceTraveled();

                        player.timer.Stop();
                        player.timer.Reset();


                        Plugin.ChatGui.Print($"{now.ToString("M/dd H:mm:ss")} {actor.Name} {actor.HomeWorld.Value.Name} just finished the parkour in {prettyPrint} and {distance} units.");
                    }
                    break;
            }
        }

        public void Left(Player player)
        {
            Plugin.Log.Debug($"{player.actor.Name} left trigger");
            touching--;

            if (touching == 0)
            {
                color = InactiveColor;
            }

            if (selectedType == TriggerType.Start)
            {
                // Player started parkour
                player.inParkour = true;
                player.raceLine.Clear();
                player.timer.Reset();
                player.timer.Start();
            }
        }
    }
}
