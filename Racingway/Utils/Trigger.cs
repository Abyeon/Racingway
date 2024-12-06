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

        private static uint InactiveColor = 0xFFFF3061;
        private static uint ActiveColor = 0xFF00FF00;

        public uint color = InactiveColor;

        public bool isTouched = false;

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
        }

        public void Left(Player player)
        {
            Plugin.Log.Debug($"{player.actor.Name} left trigger");
            touching--;

            if (touching == 0)
            {
                color = InactiveColor;
            }
        }
    }
}
