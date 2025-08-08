using Racingway.Utils;

namespace Racingway.Race.LineStyles
{
    public interface ILineStyle
    {
        string Name { get; }
        string Description { get; }

        /// <summary>
        /// Draws the given race line.
        /// </summary>
        /// <param name="line">Any race line</param>
        /// <param name="color">Color for the line</param>
        /// <param name="draw">Reference to the draw helper</param>
        void Draw(TimedVector3[] line, uint color, DrawHelper draw);
    }
}
