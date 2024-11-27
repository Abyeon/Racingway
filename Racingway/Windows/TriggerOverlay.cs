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

            Io = ImGui.GetIO();
            ImGui.SetWindowSize(Io.DisplaySize);

            // Loop through players and draw their racing lines (probably very inefficient)
            foreach (var actor in Plugin.trackedPlayers)
            {
                Vector3[] raceLine = actor.raceLine.ToArray();

                for (var i = 1; i < raceLine.Length; i++)
                {
                    if (raceLine[i - 1] == Vector3.Zero) continue;

                    Vector2 screenPos1 = new Vector2();
                    Vector2 screenPos2 = new Vector2();
                    
                    // These methods return true if the positions are in front of the screen.
                    if (Plugin.GameGui.WorldToScreen(raceLine[i - 1], out screenPos1) && Plugin.GameGui.WorldToScreen(raceLine[i], out screenPos2))
                    {
                        drawList.AddLine(screenPos1, screenPos2, 0xFFFFFFFF, 2.0f);
                    }
                }
            }
        }
    }
}
