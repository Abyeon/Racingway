using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    public class TimedVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public long Offset { get; set; }

        public TimedVector3(float x, float y, float z, long offset)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;

            this.Offset = offset;
        }

        public TimedVector3(Vector3 position, long offset)
        {
            this.X = position.X;
            this.Y = position.Y;
            this.Z = position.Z;

            this.Offset = offset;
        }

        /// <summary>
        /// Calculate a point between two TimedVector3s
        /// </summary>
        /// <param name="start">Start point</param>
        /// <param name="end">End point</param>
        /// <param name="Offset">Current elapsed time</param>
        /// <returns>TimedVector3 at the new Lerped position</returns>
        public static TimedVector3 LerpBetweenPoints(TimedVector3 start, TimedVector3 end, long Offset)
        {
            // Subtract the start offset
            long totalTime = end.Offset - start.Offset;
            long elapsed = Offset - start.Offset;

            // Calculate lerp factor
            float t = Math.Clamp((float)elapsed / (float)totalTime, 0f, 1f);

            Vector3 lerped = Vector3.Lerp(start.asVector(), end.asVector(), t);
            return new TimedVector3(lerped, Offset);
        }

        public Vector3 asVector()
        {
            return new Vector3(this.X, this.Y, this.Z);
        }
    }
}
