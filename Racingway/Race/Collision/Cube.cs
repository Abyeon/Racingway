using LiteDB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Race.Collision
{
    public class Cube
    {
        public Vector3 Position;
        public Vector3 Scale;
        public Vector3 Rotation;
        public Vector3[] Vertices { get; internal set; }

        public Cube(Vector3 position, Vector3 scale, Vector3 rotation)
        {
            Position = position;
            Scale = scale;
            Rotation = rotation;

            Vertices = [
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
            Vertices = [
                new Vector3(-1, 0, -1) * Scale,
                new Vector3(-1, 0, 1) * Scale,
                new Vector3(1, 0, 1) * Scale,
                new Vector3(1, 0, -1) * Scale,
                new Vector3(-1, 1, -1) * Scale,
                new Vector3(-1, 1, 1) * Scale,
                new Vector3(1, 1, 1) * Scale,
                new Vector3(1, 1, -1) * Scale
            ];
        }

        public bool PointInCube(Vector3 position)
        {
            var rotator = Quaternion.CreateFromYawPitchRoll(-Rotation.X, -Rotation.Y, -Rotation.Z);
            var relativeVector = position - Position;
            var rotatedVector = Vector3.Transform(relativeVector, rotator) + Position;

            var moved = GetMovedVertices();

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
            var temp = new Vector3[8];

            for (var i = 0; i < 8; i++)
            {
                temp[i] = Vertices[i] + Position;
            }

            return temp;
        }
    }
}
