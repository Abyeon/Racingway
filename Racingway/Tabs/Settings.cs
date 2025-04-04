using Dalamud.Interface;
using Dalamud.Interface.Animation;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Interface.ImGuiFontChooserDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Racingway.Tabs
{
    /// <see cref="Racingway.Configuration"/>
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

        private Vector2 spacing = new Vector2(0, 10);

        private void SectionSeparator(string text)
        {
            ImGui.Separator();
            ImGui.TextColored(ImGuiColors.DalamudOrange, text);
            ImGui.Dummy(spacing);
        }

        public void Draw()
        {
            //ImGui.Separator();
            ImGui.TextColored(ImGuiColors.DalamudOrange, "Display Toggles");
            ImGui.Dummy(spacing);

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

            SectionSeparator("Timer Style");

            if (ImGui.Button("Change Font"))
            {
                DisplayFontSelector();
            }

            var size = Plugin.Configuration.TimerSize;
            if (ImGui.DragFloat("Font Size", ref size, 0.01f, 1f, 20f))
            {
                Plugin.Configuration.TimerSize = size;
                Plugin.Configuration.Save();
            }

            var bgColor = Plugin.Configuration.TimerColor;
            if (ImGui.ColorEdit4("Background Color", ref bgColor, ImGuiColorEditFlags.NoInputs)) {
                Plugin.Configuration.TimerColor = bgColor;
                Plugin.Configuration.Save();
            }

            /* Commenting this out for now until I actually implement this feature
            var normalColor = Plugin.Configuration.NormalColor;
            if (ImGui.ColorEdit4("Normal Color", ref normalColor, ImGuiColorEditFlags.NoInputs))
            {
                Plugin.Configuration.NormalColor = normalColor;
                Plugin.Configuration.Save();
            }

            var finishColor = Plugin.Configuration.FinishColor;
            if (ImGui.ColorEdit4("Finish Color", ref finishColor, ImGuiColorEditFlags.NoInputs))
            {
                Plugin.Configuration.FinishColor = finishColor;
                Plugin.Configuration.Save();
            }

            var failColor = Plugin.Configuration.FailColor;
            if (ImGui.ColorEdit4("Fail Color", ref failColor, ImGuiColorEditFlags.NoInputs))
            {
                Plugin.Configuration.FailColor = failColor;
                Plugin.Configuration.Save();
            }*/

            using (_ = ImRaii.ItemWidth(200f))
            {
                SectionSeparator("Timer Behavior");

                bool showInParkour = Plugin.Configuration.ShowWhenInParkour;
                if (ImGui.Checkbox("Display Timer When In Parkour", ref showInParkour))
                {
                    Plugin.Configuration.ShowWhenInParkour = showInParkour;
                    Plugin.Configuration.Save();
                }

                ImGuiComponents.HelpMarker("This will display the timer even if you have it toggled off.", FontAwesomeIcon.ExclamationTriangle);

                int secondsShownAfter = Plugin.Configuration.SecondsShownAfter;
                if (ImGui.InputInt("Hide Delay", ref secondsShownAfter))
                {
                    if (secondsShownAfter < 0) secondsShownAfter = 0;

                    Plugin.Configuration.SecondsShownAfter = secondsShownAfter;
                    Plugin.Configuration.Save();
                }

                ImGuiComponents.HelpMarker("Delay the timer window being hidden by x seconds.");
            }

            SectionSeparator("Chat Output");

            bool announceRoutes = Plugin.Configuration.AnnounceLoadedRoutes;
            if (ImGui.Checkbox("Announce Loaded Routes in Chat", ref announceRoutes))
            {
                Plugin.Configuration.AnnounceLoadedRoutes = announceRoutes;
                Plugin.Configuration.Save();
            }

            bool logStart = Plugin.Configuration.LogStart;
            if(ImGui.Checkbox("Log Starts In Chat", ref logStart))
            {
                Plugin.Configuration.LogStart = logStart;
                Plugin.Configuration.Save();
            }

            ImGuiComponents.HelpMarker("This will likely spam your chat window.", FontAwesomeIcon.ExclamationTriangle);

            bool logFails = Plugin.Configuration.LogFails;
            if (ImGui.Checkbox("Log Fails In Chat", ref logFails))
            {
                Plugin.Configuration.LogFails = logFails;
                Plugin.Configuration.Save();
            }

            ImGuiComponents.HelpMarker("This will likely spam your chat window.", FontAwesomeIcon.ExclamationTriangle);

            bool logFinish = Plugin.Configuration.LogFinish;
            if (ImGui.Checkbox("Log Finishes In Chat", ref logFinish))
            {
                Plugin.Configuration.LogFinish = logFinish;
                Plugin.Configuration.Save();
            }

            SectionSeparator("Misc. Settings");

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

            if (ImGui.Button("Clear racing lines"))
            {
                foreach (var actor in Plugin.trackedPlayers.Values)
                {
                    actor.raceLine.Clear();
                }
            }

#if DEBUG
            if (ImGui.Button("Debug Print Database"))
            {
                var routes = Plugin.Storage.GetRoutes().Query().ToList();

                foreach (var route in routes)
                {
                    try
                    {
                        Plugin.Log.Debug(route.ToString());
                        foreach (var record in route.Records)
                        {
                            try
                            {
                                Plugin.Log.Debug(record.GetCSV());
                            } catch (Exception ex)
                            {
                                Plugin.Log.Error(ex.ToString());
                            }
                        }
                    } catch (Exception ex)
                    {
                        Plugin.Log.Error(ex.ToString());
                    }
                }
            }
#endif
        }

        private void DisplayFontSelector()
        {
            var chooser = SingleFontChooserDialog.CreateAuto((UiBuilder)Plugin.PluginInterface.UiBuilder);
            if (Plugin.Configuration.TimerFont is SingleFontSpec font)
            {
                chooser.SelectedFont = font;
            }
            chooser.SelectedFontSpecChanged += Chooser_SelectedFontSpecChanged;
        }

        private void Chooser_SelectedFontSpecChanged(SingleFontSpec font)
        {
            Plugin.Configuration.TimerFont = font;
            Plugin.FontManager.Handle = Plugin.Configuration.TimerFont.CreateFontHandle(Plugin.PluginInterface.UiBuilder.FontAtlas);
            Plugin.Configuration.Save();
        }
    }
}
