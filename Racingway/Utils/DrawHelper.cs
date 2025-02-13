using ImGuiNET;
using Racingway.Collision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    public class DrawHelper
    {
        private ImDrawListPtr drawList;

        public DrawHelper(ImDrawListPtr drawListPtr)
        {
            this.drawList = drawListPtr;
        }

        public void DrawText3d(string text, Vector3 position, uint color)
        {
            Vector2 screenPos = new Vector2();

            if (Plugin.GameGui.WorldToScreen(position, out screenPos))
            {
                drawList.AddText(screenPos, color, text);
            }
        }

        public void DrawLine3d(Vector3 start, Vector3 end, uint color, float thickness)
        {
            Vector2 screenPos1 = new Vector2();
            Vector2 screenPos2 = new Vector2();

            // These methods return true if the positions are in front of the screen.
            if (Plugin.GameGui.WorldToScreen(start, out screenPos1) && Plugin.GameGui.WorldToScreen(end, out screenPos2))
            {
                drawList.AddLine(screenPos1, screenPos2, color, thickness);
            }
        }

        public void DrawQuadFilled3d(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, uint color)
        {
            bool onScreen = false;

            Vector3[] worldPos = { p1, p2, p3, p4 };
            Vector2[] screenPos = new Vector2[4];

            for (int i = 0; i < 4; i++)
            {
                if (Plugin.GameGui.WorldToScreen(worldPos[i], out screenPos[i]))
                {
                    onScreen = true;
                }
            }

            if (onScreen)
            {
                drawList.AddQuadFilled(screenPos[0], screenPos[1], screenPos[2], screenPos[3], color);
            }
        }

        public void DrawAABB(Vector3 min, Vector3 max, uint color, float thickness)
        {
            // Define points of the bounding box
            Vector3[] points =
            {
                min,
                new Vector3(min.X, min.Y, max.Z),
                new Vector3(max.X, min.Y, max.Z),
                new Vector3(max.X, min.Y, min.Z),
                new Vector3(min.X, max.Y, min.Z),
                new Vector3(min.X, max.Y, max.Z),
                max,
                new Vector3(max.X, max.Y, min.Z)
            };

            // Define line indices
            int[,] lines =
            {
                {0, 1}, {1, 2}, {2, 3}, {3, 0}, // top face
                {4, 5}, {5, 6}, {6, 7}, {7, 4}, // bottom face
                {0, 4}, {1, 5}, {2,6}, {3, 7} // Side edges
            };

            // Go through lines and draw them
            for (int i = 0; i < lines.GetLength(0); i++)
            {
                int start = lines[i, 0];
                int end = lines[i, 1];

                DrawLine3d(points[start], points[end], color, thickness);
            }
        }

        public void DrawCube(Cube cube, uint color, float thickness)
        {
            // Array of indices defining lines for each face of the cube
            int[,] lines =
            {
                {0, 1}, {1, 2}, {2, 3}, {3, 0}, // top face
                {4, 5}, {5, 6}, {6, 7}, {7, 4}, // bottom face
                {0, 4}, {1, 5}, {2,6}, {3, 7} // Side edges
            };

            // Get rotated vertices of cube
            // For some unknown reason this updates the cubes native vertices to be the rotated positions
            Vector3[] points = RotatePointsAroundOrigin(cube.Vertices, Vector3.Zero, cube.Rotation);
            cube.UpdateVerts(); // Revert the cubes vertices back to their axis aligned scaled versions.

            // Go through lines and draw them
            for (int i = 0; i < lines.GetLength(0); i++)
            {
                int start = lines[i, 0];
                int end = lines[i, 1];

                DrawLine3d(points[start] + cube.Position, points[end] + cube.Position, color, thickness);
            }
        }

        public void DrawCubeFilled(Cube cube, uint color, float thickness) 
        {
            // Arrays of indices for each face
            int[,] faces =
            {
                {0, 1, 2, 3}, // top face
                {4, 5, 6, 7}, // bottom face
                {0, 4, 7, 3}, // front face
                {1, 5, 6, 2}, // back face
                {1, 5, 4, 0}, // left face
                {3, 7, 6, 2} // right face
            };

            // Get rotated vertices of cube
            // For some unknown reason this updates the cubes native vertices to be the rotated positions
            Vector3[] points = RotatePointsAroundOrigin(cube.Vertices, Vector3.Zero, cube.Rotation);
            cube.UpdateVerts(); // Revert the cubes vertices back to their axis aligned scaled versions.

            for (int i = 0; i < faces.GetLength(0); i++)
            {
                Vector3 p1 = points[faces[i, 0]];
                Vector3 p2 = points[faces[i, 1]];
                Vector3 p3 = points[faces[i, 2]];
                Vector3 p4 = points[faces[i, 3]];

                DrawQuadFilled3d(p1 + cube.Position, p2 + cube.Position, p3 + cube.Position, p4 + cube.Position, color);
            }
        }

        public Vector3[] RotatePointsAroundOrigin(Vector3[] points, Vector3 origin, Vector3 rotation)
        {
            Vector3[] tempVecs = points;

            for (int i = 0; i < 8; i++)
            {
                Quaternion rotator = Quaternion.CreateFromYawPitchRoll(rotation.X, rotation.Y, rotation.Z);
                Vector3 relativeVector = tempVecs[i] - origin;
                Vector3 rotatedVector = Vector3.Transform(relativeVector, rotator);
                tempVecs[i] = rotatedVector + origin;
            }

            return tempVecs;
        }
    }
}
