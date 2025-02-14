using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Tabs
{
    internal class Settings : ITab
    {
        public string Name => "Settings";

        private Plugin Plugin { get; }

        public Settings(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void Dispose()
        {

        }

        public void Draw()
        {
            if (ImGui.Button($"{(Plugin.TriggerOverlay.IsOpen ? "Hide" : "Show" )} Overlay"))
            {
                Plugin.ToggleTriggerUI();
            }

            if (ImGui.Button($"{(Plugin.Configuration.DrawTriggers ? "Disable" : "Enable")} Triggers Display"))
            {
                Plugin.Configuration.DrawTriggers = !Plugin.Configuration.DrawTriggers;
                Plugin.Configuration.Save();
            }

            //ImGui.SameLine();
            if (ImGui.Button($"{(Plugin.Configuration.DrawRacingLines ? "Disable" : "Enable")} Racing Lines Display"))
            {
                Plugin.Configuration.DrawRacingLines = !Plugin.Configuration.DrawRacingLines;
                Plugin.Configuration.Save();
            }

            //ImGui.SameLine();
            if (ImGui.Button("Clear racing lines"))
            {
                foreach (var actor in Plugin.trackedPlayers.Values)
                {
                    actor.raceLine.Clear();
                }
            }

            int quality = Plugin.Configuration.LineQuality;
            if (ImGui.DragInt("Line Quality", ref quality, 0.05f, 0, 50))
            {
                Plugin.Configuration.LineQuality = quality;
                Plugin.Configuration.Save();
            }
        }
    }
}
