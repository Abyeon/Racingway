using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Race.LineStyles
{
    public class Dotted : ILineStyle
    {
        public string Name => "Dotted";

        public string Description => "A dotted line.";

        private Plugin Plugin { get; set; }

        public Dotted(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void Draw(TimedVector3[] line, uint color, DrawHelper draw)
        {
            if (line.Length == 0) return;

            for (int i = 0; i < line.Length; i++)
            {
                draw.DrawPoint3d(line[i].asVector(), color, Plugin.Configuration.DotSize);
            }
        }
    }
}
