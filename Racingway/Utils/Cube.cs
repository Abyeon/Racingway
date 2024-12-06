using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    public class Cube
    {
        public Vector3 Position;
        public Vector3 Scale;
        public Vector3 Rotation;

        public Vector3[] Vertices { get; internal set; }

        public Cube(Vector3 position, Vector3 scale, Vector3 rotation)
        {
            this.Position = position;
            this.Scale = scale;
            this.Rotation = rotation;

            this.Vertices = [
                new Vector3(-1, 0, -1) * scale,
                new Vector3(-1, 0, 1) * scale,
                new Vector3(1, 0, 1) * scale,
                new Vector3(1, 0, -1) * scale,
                new Vector3(-1, 1, -1) * scale,
                new Vector3(-1, 1, 1) * scale,
                new Vector3(1, 1, 1) * scale,
                new Vector3(1, 1, -1) * scale
            ];
        }

        public void UpdateVerts()
        {
            this.Vertices = [
                new Vector3(-1, 0, -1) * this.Scale,
                new Vector3(-1, 0, 1) * this.Scale,
                new Vector3(1, 0, 1) * this.Scale,
                new Vector3(1, 0, -1) * this.Scale,
                new Vector3(-1, 1, -1) * this.Scale,
                new Vector3(-1, 1, 1) * this.Scale,
                new Vector3(1, 1, 1) * this.Scale,
                new Vector3(1, 1, -1) * this.Scale
            ];
        }

        public bool PointInCube(Vector3 position)
        {
            Quaternion rotator = Quaternion.CreateFromYawPitchRoll(-this.Rotation.X, -this.Rotation.Y, -this.Rotation.Z);
            Vector3 relativeVector = position - this.Position;
            Vector3 rotatedVector = (Vector3.Transform(relativeVector, rotator) + this.Position);

            Vector3[] moved = GetMovedVertices();

            return
                rotatedVector.X >= moved[0].X &&
                rotatedVector.X <= moved[6].X &&
                rotatedVector.Y >= moved[0].Y &&
                rotatedVector.Y <= moved[6].Y &&
                rotatedVector.Z >= moved[0].Z &&
                rotatedVector.Z <= moved[6].Z;
        }

        public Vector3[] GetMovedVertices()
        {
            Vector3[] temp = new Vector3[8];

            for (int i = 0; i < 8; i++) 
            {
                temp[i] = this.Vertices[i] + this.Position;
            }

            return temp;
        }
    }
}
