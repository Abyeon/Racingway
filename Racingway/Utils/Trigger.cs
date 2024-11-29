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
        private Plugin Plugin;

        public Vector3 min = Vector3.Zero;
        public Vector3 max = Vector3.Zero;

        private static uint InactiveColor = 0xFFFF3061;
        private static uint ActiveColor = 0xFF00FF00;

        public uint color = InactiveColor;

        public bool isTouched = false;

        private int touching = 0;

        public Trigger(Plugin plugin) 
        {
            this.Plugin = plugin;
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
