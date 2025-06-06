using LiteDB;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Race
{
    [MessagePackObject]
    public class TimedVector3
    {
        [Key(0)] public float X { get; set; }
        [Key(1)] public float Y { get; set; }
        [Key(2)] public float Z { get; set; }
        [Key(3)] public long Offset { get; set; }

        public TimedVector3(float x, float y, float z, long offset)
        {
            X = x;
            Y = y;
            Z = z;

            Offset = offset;
        }

        public TimedVector3(Vector3 position, long offset)
        {
            X = position.X;
            Y = position.Y;
            Z = position.Z;

            Offset = offset;
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
            var totalTime = end.Offset - start.Offset;
            var elapsed = Offset - start.Offset;

            // Calculate lerp factor
            var t = Math.Clamp(elapsed / (float)totalTime, 0f, 1f);

            var lerped = Vector3.Lerp(start.asVector(), end.asVector(), t);
            return new TimedVector3(lerped, Offset);
        }

        public Vector3 asVector()
        {
            return new Vector3(X, Y, Z);
        }
    }
}
