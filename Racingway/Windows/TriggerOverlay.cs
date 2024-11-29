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
            DrawHelper draw = new DrawHelper(Plugin, drawList);

            Io = ImGui.GetIO();
            ImGui.SetWindowSize(Io.DisplaySize);

            if (Plugin.Configuration.DrawRacingLines)
            {
                // Loop through players and draw their racing lines (probably very inefficient)
                foreach (var actor in Plugin.trackedPlayers)
                {
                    Vector3[] raceLine = actor.raceLine.ToArray();

                    for (var i = 1; i < raceLine.Length; i++)
                    {
                        if (raceLine[i - 1] == Vector3.Zero) continue;

                        draw.DrawLine3d(raceLine[i - 1], raceLine[i], 0x55FFFFFF, 2.0f);
                    }
                }
            }

            // Loop through triggers and draw them
            foreach (var trigger in Plugin.triggers)
            {
                if (trigger == null) continue;

                draw.DrawAABB(trigger.min, trigger.max, trigger.color, 5.0f);
            }
        }
    }
}
