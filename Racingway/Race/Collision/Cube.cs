using LiteDB;
using MessagePack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Race.Collision
{
    [MessagePackObject]
    public class Cube
    {
        [Key(0)] public Vector3 Position;
        [Key(1)] public Vector3 Scale;
        [Key(2)] public Vector3 Rotation;
        [IgnoreMember] public Vector3[] Vertices { get; internal set; }

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

        /// <summary>
        /// Check if a point is within the cube
        /// </summary>
        /// <param name="position"></param>
        /// <returns>true if the point is within the cube</returns>
        public bool PointInCube(Vector3 position)
        {
            // Since cubes are AABB's at their heart, rotate the point around the cube's origin in the opposite direction of the cube's rotation
            var rotator = Quaternion.CreateFromYawPitchRoll(-Rotation.X, -Rotation.Y, -Rotation.Z);
            var relativeVector = position - Position;
            var rotatedVector = Vector3.Transform(relativeVector, rotator) + Position;

            // Get the cube's vertices in the relative space
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
