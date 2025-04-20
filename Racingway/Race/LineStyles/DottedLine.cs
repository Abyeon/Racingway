using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Race.LineStyles
{
    public class DottedLine : ILineStyle
    {
        public string Name => "Dots and Lines";

        public string Description => "Like the default style, with dots at each point in the line.";

        private Plugin Plugin { get; set; }

        public DottedLine(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void Draw(TimedVector3[] line, uint color, DrawHelper draw)
        {
            draw.DrawPoint3d(line[0].asVector(), color, 3.0f);

            for (int i = 1; i < line.Length; i++)
            {
                if (line[i - 1].asVector() == Vector3.Zero) continue;

                draw.DrawLine3d(line[i - 1].asVector(), line[i].asVector(), color, Plugin.Configuration.LineThickness);
                draw.DrawPoint3d(line[i].asVector(), color, Plugin.Configuration.DotSize);
            }
        }
    }
}
