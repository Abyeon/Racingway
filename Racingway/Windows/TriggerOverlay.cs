using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Interface.Utility;
using System.Numerics;
using Racingway.Utils;
using Serilog;
using Dalamud.Game.ClientState.Objects.Types;
using ImGuizmoNET;


namespace Racingway.Windows
{
    public class TriggerOverlay : Window, IDisposable
    {
        private Plugin Plugin;

        private ImGuiIOPtr Io;

        public TriggerOverlay(Plugin plugin) : base("Trigger Overlay")
        {
            Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoNavInputs | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoInputs;
            
            Plugin = plugin;
        }

        public void Dispose()
        {

        }

        public unsafe override void Draw()
        {
            ImGuiHelpers.SetWindowPosRelativeMainViewport("Trigger Overlay", new Vector2(0, 0));
            
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            DrawHelper draw = new DrawHelper(drawList);

            Io = ImGui.GetIO();
            ImGui.SetWindowSize(Io.DisplaySize);

            // Display player racing lines
            if (Plugin.Configuration.DrawRacingLines)
            {
                foreach (var actor in Plugin.trackedPlayers.Values)
                {
                    Vector3[] raceLine = actor.raceLine.ToArray();

                    for (var i = 1; i < raceLine.Length; i++)
                    {
                        if (raceLine[i - 1] == Vector3.Zero) continue;

                        draw.DrawLine3d(raceLine[i - 1], raceLine[i], 0x55FFFFFF, 2.0f);
                    }
                }

                // Draw the selected Record's line
                for (var i = 1; i < Plugin.DisplayedRecord.Line.Length; i++)
                {
                    if (Plugin.DisplayedRecord.Line[i - 1] == Vector3.Zero) continue;

                    draw.DrawLine3d(Plugin.DisplayedRecord.Line[i - 1], Plugin.DisplayedRecord.Line[i], 0x55FFCCFF, 2.0f);
                }
            }

            // Display Trigger debug UI.
            if (Plugin.Configuration.DrawTriggers)
            {
                foreach (var trigger in Plugin.Configuration.Triggers)
                {
                    if (trigger == null) continue;

                    //draw.DrawCube(trigger.cube, trigger.color, 5.0f);
                    draw.DrawCubeFilled(trigger.Cube, trigger.color, 5.0f);
                }
            }
        }
    }
}
