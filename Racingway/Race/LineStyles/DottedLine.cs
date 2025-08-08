using System.Numerics;
using Racingway.Utils;

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
            if (line.Length == 0)
                return;

            draw.DrawPoint3d(line[0].asVector(), color, Plugin.Configuration.DotSize);

            for (int i = 1; i < line.Length; i++)
            {
                if (line[i - 1].asVector() == Vector3.Zero)
                    continue;

                draw.DrawLine3d(
                    line[i - 1].asVector(),
                    line[i].asVector(),
                    color,
                    Plugin.Configuration.LineThickness
                );
                draw.DrawPoint3d(line[i].asVector(), color, Plugin.Configuration.DotSize);
            }
        }
    }
}
