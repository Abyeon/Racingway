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
        private Plugin plugin;

        public Vector3 min = Vector3.Zero;
        public Vector3 max = Vector3.Zero;

        public Trigger(Plugin plugin) 
        {
            this.plugin = plugin;
        }

        //public Trigger(Plugin plugin...)
        //{

        //}
    }
}
