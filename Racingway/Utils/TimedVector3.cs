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

        public Vector3 asVector()
        {
            return new Vector3(this.X, this.Y, this.Z);
        }
    }
}
