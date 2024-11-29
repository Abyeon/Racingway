using ImGuiNET;
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
        private Plugin plugin;
        private ImDrawListPtr drawList;

        public DrawHelper(Plugin plugin, ImDrawListPtr drawListPtr)
        {
            this.plugin = plugin;
            this.drawList = drawListPtr;
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

        public void DrawAABB(Vector3 min, Vector3 max, uint color, float thickness)
        {
            // Bottom rectangle
            Vector3 corner1 = new Vector3(min.X, min.Y, max.Z);
            Vector3 corner2 = new Vector3(max.X, min.Y, max.Z);
            Vector3 corner3 = new Vector3(max.X, min.Y, min.Z);
            // Top rectangle
            Vector3 corner4 = new Vector3(min.X, max.Y, min.Z);
            Vector3 corner5 = new Vector3(min.X, max.Y, max.Z);
            // max goes here
            Vector3 corner6 = new Vector3(max.X, max.Y, min.Z);

            //Draw bottom face
            DrawLine3d(min, corner1, color, thickness);
            DrawLine3d(corner1, corner2, color, thickness);
            DrawLine3d(corner2, corner3, color, thickness);
            DrawLine3d(corner3, min, color, thickness);

            // Draw top face
            DrawLine3d(corner4, corner5, color, thickness);
            DrawLine3d(corner5, max, color, thickness);
            DrawLine3d(max, corner6, color, thickness);
            DrawLine3d(corner6, corner4, color, thickness);

            // Connect top and bottom
            DrawLine3d(min, corner4, color, thickness);
            DrawLine3d(corner1, corner5, color, thickness);
            DrawLine3d(corner2, max, color, thickness);
            DrawLine3d(corner3, corner6, color, thickness);
        }
    }
}
