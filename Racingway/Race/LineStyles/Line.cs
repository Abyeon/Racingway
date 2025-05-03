using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Race.LineStyles
{
    public class Line : ILineStyle
    {
        public string Name => "Line";

        public string Description => "The default line style.";

        private Plugin Plugin { get; set; }

        public Line(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void Draw(TimedVector3[] line, uint color, DrawHelper draw)
        {
            if (line.Length == 0) return;

            for (int i = 1; i < line.Length; i++)
            {
                if (line[i - 1].asVector() == Vector3.Zero) continue;

                draw.DrawLine3d(line[i - 1].asVector(), line[i].asVector(), color, Plugin.Configuration.LineThickness);
            }
        }
    }
}
