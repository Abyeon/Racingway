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
            if (ImGui.Button($"{(Plugin.Configuration.DrawTriggers ? "Disable" : "Enable")} Triggers Display"))
            {
                Plugin.Configuration.DrawTriggers = !Plugin.Configuration.DrawTriggers;
                Plugin.Configuration.Save();
                Plugin.ShowHideOverlay();
            }

            if (ImGui.Button($"{(Plugin.Configuration.DrawRacingLines ? "Disable" : "Enable")} Racing Lines Display"))
            {
                Plugin.Configuration.DrawRacingLines = !Plugin.Configuration.DrawRacingLines;
                Plugin.Configuration.Save();
                Plugin.ShowHideOverlay();
            }

            if (ImGui.Button($"{(Plugin.Configuration.DrawTimer ? "Disable" : "Enable")} Timer Display"))
            {
                Plugin.Configuration.DrawTimer = !Plugin.Configuration.DrawTimer;
                Plugin.Configuration.Save();
                Plugin.ShowHideOverlay();
            }

            var size = Plugin.Configuration.TimerSize;
            if (ImGui.DragFloat("Timer Font Size", ref size, 0.01f, 1f, 20f))
            {
                Plugin.Configuration.TimerSize = size;
                Plugin.Configuration.Save();
            }

            var color = Plugin.Configuration.TimerColor;
            if (ImGui.ColorEdit4("Timer Background Color", ref color)) {
                Plugin.Configuration.TimerColor = color;
                Plugin.Configuration.Save();
            }
            

            if (ImGui.Button("Clear racing lines"))
            {
                foreach (var actor in Plugin.trackedPlayers.Values)
                {
                    actor.raceLine.Clear();
                }
            }

            bool announceRoutes = Plugin.Configuration.AnnounceLoadedRoutes;
            if (ImGui.Checkbox("Announce Loaded Routes in Chat", ref announceRoutes))
            {
                Plugin.Configuration.AnnounceLoadedRoutes = announceRoutes;
                Plugin.Configuration.Save();
            }

            bool logFails = Plugin.Configuration.LogFails;
            if (ImGui.Checkbox("Log Fails In Chat", ref logFails))
            {
                Plugin.Configuration.LogFails = logFails;
                Plugin.Configuration.Save();
            }

            bool logFinish = Plugin.Configuration.LogFinish;
            if (ImGui.Checkbox("Log Finishes In Chat", ref logFinish))
            {
                Plugin.Configuration.LogFinish = logFinish;
                Plugin.Configuration.Save();
            }

            bool trackOthers = Plugin.Configuration.TrackOthers;
            if (ImGui.Checkbox("Track Other Players", ref trackOthers))
            {
                Plugin.Configuration.TrackOthers = trackOthers;
                Plugin.Configuration.Save();
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
