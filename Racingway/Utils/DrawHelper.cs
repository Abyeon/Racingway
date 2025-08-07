using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using CameraManager = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager;
using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImGuizmo;
using Racingway.Race.Collision;
using Racingway.Race.Collision.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
// using ImPlotNET; // Not needed in this file

namespace Racingway.Utils
{
    /// <summary>
    /// Helper for drawing different shapes to an ImGUI Drawlist.
    /// </summary>
    public class DrawHelper
    {
        private ImDrawListPtr drawList;
        private unsafe Camera* Camera => &CameraManager.Instance()->GetActiveCamera()->CameraBase.SceneCamera;

        public DrawHelper(ImDrawListPtr drawListPtr)
        {
            this.drawList = drawListPtr;
        }

        // Basically yoinked from https://github.com/LeonBlade/BDTHPlugin
        public unsafe bool DrawGizmo(ref Vector3 pos, ref Vector3 rotation, ref Vector3 scale, string id, float snapDistance)
        {
            ImGuizmo.BeginFrame();

            var cam = Camera->RenderCamera;
            var view = Camera->ViewMatrix;
            var proj = cam->ProjectionMatrix;

            var far = cam->FarPlane;
            var near = cam->NearPlane;
            var clip = far / (far - near);

            proj.M43 = -(clip * near);
            proj.M33 = -((far + near) / (far - near));
            view.M44 = 1.0f;

            ImGuizmo.SetDrawlist();
            ImGuizmo.Enable(true);
            ImGuizmo.SetID((int)ImGui.GetID("Gizmo" + id));
            ImGuizmo.SetOrthographic(false);

            Vector2 windowPos = ImGui.GetWindowPos();
            ImGuiIOPtr Io = ImGui.GetIO();

            ImGuizmo.SetRect(windowPos.X, windowPos.Y, Io.DisplaySize.X, Io.DisplaySize.Y);

            Matrix4x4 matrix = Matrix4x4.Identity;

            ImGuizmo.RecomposeMatrixFromComponents(ref pos.X, ref rotation.X, ref scale.X, ref matrix.M11);

            Vector3 snap = Vector3.One * snapDistance;

            ImGuizmoOperation op = ImGuizmoOperation.Translate;

            if (Io.KeyCtrl)
            {
                op = ImGuizmoOperation.Scale;
            }

            Manipulate(ref view.M11, ref proj.M11, op, ImGuizmoMode.Local, ref matrix.M11, ref snap.X);

            if (ImGuizmo.IsUsing())
            {
                ImGuizmo.DecomposeMatrixToComponents(ref matrix.M11, ref pos.X, ref rotation.X, ref scale.X);
                return true;
            } else
            {
                return false;
            }
        }

        // The new bindings use the ImGuizmo class directly
        private unsafe bool Manipulate(ref float view, ref float proj, ImGuizmoOperation op, ImGuizmoMode mode, ref float matrix, ref float snap)
        {
            fixed (float* native_view = &view)
            {
                fixed (float* native_proj = &proj)
                {
                    fixed (float* native_matrix = &matrix)
                    {
                        fixed (float* native_snap = &snap)
                        {
                            // Use the ImGuizmo.Manipulate method with proper parameters
                            return ImGuizmo.Manipulate(
                                native_view,
                                native_proj,
                                op,
                                mode,
                                native_matrix,
                                null,
                                native_snap
                            );
                        }
                    }
                }
            }
        }

        public void DrawTextWithBackground(string text, uint color)
        {
            drawList.ChannelsSplit(2);
            drawList.ChannelsSetCurrent(1);
            ImGui.TextUnformatted(text);

            drawList.ChannelsSetCurrent(0);
            drawList.AddRectFilled(ImGui.GetItemRectMin() - ImGui.GetStyle().FramePadding * 2, ImGui.GetItemRectMax() + ImGui.GetStyle().FramePadding * 2, color);
            drawList.ChannelsMerge();
        }

        public void DrawText3d(string text, Vector3 position, uint color)
        {
            Vector2 screenPos = new Vector2();

            if (Plugin.GameGui.WorldToScreen(position, out screenPos))
            {
                drawList.AddText(screenPos, color, text);
            }
        }

        public void DrawPoint3d(Vector3 position, uint color, float radius)
        {
            Vector2 screenPos = new Vector2();

            if (Plugin.GameGui.WorldToScreen(position, out screenPos))
            {
                drawList.AddCircleFilled(screenPos, radius, color);
            }
        }

        public void DrawLine3d(Vector3 start, Vector3 end, uint color, float thickness)
        {
            Vector2 screenPos1 = new Vector2();
            Vector2 screenPos2 = new Vector2();
    
            // Always attempt to draw the line, even if points are off-screen
            // This ensures consistency when zooming in/out
            bool startVisible = Plugin.GameGui.WorldToScreen(start, out screenPos1);
            bool endVisible = Plugin.GameGui.WorldToScreen(end, out screenPos2);

            // Draw the line if at least one end is visible or if both are off-screen but might cross the viewport
            if (startVisible || endVisible)
            {
                drawList.AddLine(screenPos1, screenPos2, color, thickness);
            }
        }

        public void DrawPath3d(Vector3[] path, uint color, float thickness)
        {
            if (path.Length == 0)
                return;

            // Skip drawing if too many points - would cause performance issues
            if (path.Length > 1000)
                return;

            // Draw segments directly instead of using PathLineTo/PathStroke, which can cause
            // visual inconsistencies when the camera moves
            for (int i = 1; i < path.Length; i++)
            {
                DrawLine3d(path[i - 1], path[i], color, thickness);
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
                drawList.AddQuadFilled(
                    screenPos[0],
                    screenPos[1],
                    screenPos[2],
                    screenPos[3],
                    color
                );
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
                new Vector3(max.X, max.Y, min.Z),
            };

            // Define line indices
            int[,] lines =
            {
                { 0, 1 },
                { 1, 2 },
                { 2, 3 },
                { 3, 0 }, // top face
                { 4, 5 },
                { 5, 6 },
                { 6, 7 },
                { 7, 4 }, // bottom face
                { 0, 4 },
                { 1, 5 },
                { 2, 6 },
                {
                    3,
                    7,
                } // Side edges
                ,
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
                { 0, 1 },
                { 1, 2 },
                { 2, 3 },
                { 3, 0 }, // top face
                { 4, 5 },
                { 5, 6 },
                { 6, 7 },
                { 7, 4 }, // bottom face
                { 0, 4 },
                { 1, 5 },
                { 2, 6 },
                { 3, 7 } // Side edges
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

                DrawLine3d(
                    points[start] + cube.Position,
                    points[end] + cube.Position,
                    color,
                    thickness
                );
            }
        }

        public void DrawCubeFilled(Cube cube, uint color)
        {
            // Arrays of indices for each face
            int[,] faces =
            {
                { 0, 1, 2, 3 }, // top face
                { 4, 5, 6, 7 }, // bottom face
                { 0, 4, 7, 3 }, // front face
                { 1, 5, 6, 2 }, // back face
                { 1, 5, 4, 0 }, // left face
                { 3, 7, 6, 2,} // right face
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

                DrawQuadFilled3d(
                    p1 + cube.Position,
                    p2 + cube.Position,
                    p3 + cube.Position,
                    p4 + cube.Position,
                    color
                );
            }
        }

        public Vector3[] RotatePointsAroundOrigin(Vector3[] points, Vector3 origin, Vector3 rotation)
        {
            Vector3[] tempVecs = points;
         
            Quaternion rotator = Quaternion.CreateFromYawPitchRoll(
                rotation.X,
                rotation.Y,
                rotation.Z
            );

            for (int i = 0; i < 8; i++)
            {
                Vector3 relativeVector = tempVecs[i] - origin;
                Vector3 rotatedVector = Vector3.Transform(relativeVector, rotator);
                tempVecs[i] = rotatedVector + origin;
            }

            return tempVecs;
        }
    }
}
