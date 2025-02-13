using Dalamud.Game.ClientState.Objects.SubKinds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Racingway.Utils;

namespace Racingway.Collision
{
    public class Trigger
    {
        public Plugin Plugin { get; set; }
        public Cube Cube { get; set; }

        public event EventHandler<Player> Entered;
        public event EventHandler<Player> Left;


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

        public Trigger(Vector3 position, Vector3 scale, Vector3 rotation, Plugin plugin)
        {
            this.Cube = new Cube(position, scale, rotation);
            this.Plugin = plugin;
        }

        public void CheckCollision(Player player)
        {
            var inTrigger = Cube.PointInCube(player.position);

            if (inTrigger && !touchers.Contains(player.id))
            {
                touchers.Add(player.id);
                OnEntered(player);
            }
            else if (!inTrigger && touchers.Contains(player.id))
            {
                touchers.Remove(player.id);
                OnLeft(player);
            }
        }

        protected virtual void OnEntered(Player player)
        {
            touching++;
            color = ActiveColor;

            Entered?.Invoke(this, player);
        }

        protected virtual void OnLeft(Player player)
        {
            touching--;

            if (touching == 0)
            {
                color = InactiveColor;
            }

            Left?.Invoke(this, player);
        }
    }
}
