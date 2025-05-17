using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Interface.ImGuiFontChooserDialog;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Racingway.Race.LineStyles;
using Racingway.Windows;

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

        public void Dispose() { }

        private Vector2 spacing = new Vector2(0, 10);

        private void SectionSeparator(string text)
        {
            ImGui.Separator();
            ImGui.TextColored(ImGuiColors.DalamudOrange, text);
            ImGui.Dummy(spacing);
        }

        public void Draw()
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, "Display Toggles");
            #region Display Toggles
            ImGui.Dummy(spacing);

            if (
                ImGui.Button(
                    $"{(Plugin.Configuration.DrawTriggers ? "Disable" : "Enable")} Triggers Display"
                )
            )
            {
                Plugin.Configuration.DrawTriggers = !Plugin.Configuration.DrawTriggers;
                Plugin.Configuration.Save();
                Plugin.ShowHideOverlay();
            }

            if (
                ImGui.Button(
                    $"{(Plugin.Configuration.DrawRacingLines ? "Disable" : "Enable")} Racing Lines Display"
                )
            )
            {
                Plugin.Configuration.DrawRacingLines = !Plugin.Configuration.DrawRacingLines;
                Plugin.Configuration.Save();
                Plugin.ShowHideOverlay();
            }

            if (
                ImGui.Button(
                    $"{(Plugin.Configuration.DrawTimer ? "Disable" : "Enable")} Timer Display"
                )
            )
            {
                Plugin.Configuration.DrawTimer = !Plugin.Configuration.DrawTimer;
                Plugin.Configuration.Save();
                Plugin.ShowHideOverlay();
            }
            #endregion

            SectionSeparator("Timer Style");
            #region Timer Style

            if (ImGui.Button("Change Font"))
            {
                DisplayFontSelector();
            }

            using (_ = ImRaii.ItemWidth(200f))
            {
                var size = Plugin.Configuration.TimerSize;
                if (ImGui.DragFloat("Font Size", ref size, 0.01f, 1f, 20f))
                {
                    Plugin.Configuration.TimerSize = size;
                    Plugin.Configuration.Save();
                }
            }

            var bgColor = Plugin.Configuration.TimerColor;
            if (ImGui.ColorEdit4("Background Color", ref bgColor, ImGuiColorEditFlags.NoInputs))
            {
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

                ImGuiComponents.HelpMarker(
                    "This will display the timer even if you have it toggled off.",
                    FontAwesomeIcon.ExclamationTriangle
                );

                int secondsShownAfter = Plugin.Configuration.SecondsShownAfter;
                if (ImGui.InputInt("Hide Delay", ref secondsShownAfter))
                {
                    if (secondsShownAfter < 0)
                        secondsShownAfter = 0;

                    Plugin.Configuration.SecondsShownAfter = secondsShownAfter;
                    Plugin.Configuration.Save();
                }

                ImGuiComponents.HelpMarker("Delay the timer window being hidden by x seconds.");
            }
            #endregion

            SectionSeparator("Chat Output");
            #region Chat Output

            bool announceRoutes = Plugin.Configuration.AnnounceLoadedRoutes;
            if (ImGui.Checkbox("Announce Loaded Routes in Chat", ref announceRoutes))
            {
                Plugin.Configuration.AnnounceLoadedRoutes = announceRoutes;
                Plugin.Configuration.Save();
            }

            bool logStart = Plugin.Configuration.LogStart;
            if (ImGui.Checkbox("Log Starts In Chat", ref logStart))
            {
                Plugin.Configuration.LogStart = logStart;
                Plugin.Configuration.Save();
            }

            ImGuiComponents.HelpMarker(
                "This will likely spam your chat window.",
                FontAwesomeIcon.ExclamationTriangle
            );

            bool logFails = Plugin.Configuration.LogFails;
            if (ImGui.Checkbox("Log Fails In Chat", ref logFails))
            {
                Plugin.Configuration.LogFails = logFails;
                Plugin.Configuration.Save();
            }

            ImGuiComponents.HelpMarker(
                "This will likely spam your chat window.",
                FontAwesomeIcon.ExclamationTriangle
            );

            bool logFinish = Plugin.Configuration.LogFinish;
            if (ImGui.Checkbox("Log Finishes In Chat", ref logFinish))
            {
                Plugin.Configuration.LogFinish = logFinish;
                Plugin.Configuration.Save();
            }
            #endregion

            SectionSeparator("Line Style");
            #region Line Style

            using (_ = ImRaii.ItemWidth(200f))
            {
                int quality = Plugin.Configuration.LineQuality;
                if (ImGui.DragInt("Line Quality", ref quality, 0.05f, 1, 50))
                {
                    Plugin.Configuration.LineQuality = quality;
                    Plugin.Configuration.Save();
                }

                ImGuiComponents.HelpMarker(
                    "How many frames between line points. 1 line quality = 1 point per frame."
                );

                // Combo box for selecting the desired line style
                using (var child = ImRaii.Combo("Line Style", Plugin.Configuration.LineStyle))
                {
                    if (child.Success)
                    {
                        foreach (ILineStyle style in Plugin.TriggerOverlay.LineStyles)
                        {
                            if (ImGui.Selectable(style.Name))
                            {
                                Plugin.Configuration.LineStyle = style.Name;
                                Plugin.TriggerOverlay.selectedStyle = style;

                                Plugin.Configuration.Save();
                            }

                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip(style.Description);
                            }
                        }
                    }
                }

                float dotSize = Plugin.Configuration.DotSize;
                if (ImGui.DragFloat("Dot Size", ref dotSize, 0.05f, 1f, 10f))
                {
                    Plugin.Configuration.DotSize = dotSize;
                    Plugin.Configuration.Save();
                }

                float lineThickness = Plugin.Configuration.LineThickness;
                if (ImGui.DragFloat("Line Thickness", ref lineThickness, 0.05f, 1f, 10f))
                {
                    Plugin.Configuration.LineThickness = lineThickness;
                    Plugin.Configuration.Save();
                }

                var lineColor = Plugin.Configuration.LineColor;
                if (ImGui.ColorEdit4("Line Color", ref lineColor, ImGuiColorEditFlags.NoInputs))
                {
                    Plugin.Configuration.LineColor = lineColor;
                    Plugin.Configuration.Save();
                }

                var highlightedLineColor = Plugin.Configuration.HighlightedLineColor;
                if (
                    ImGui.ColorEdit4(
                        "Highlighted Line Color",
                        ref highlightedLineColor,
                        ImGuiColorEditFlags.NoInputs
                    )
                )
                {
                    Plugin.Configuration.HighlightedLineColor = highlightedLineColor;
                    Plugin.Configuration.Save();
                }

                int maxLinePoints = Plugin.Configuration.MaxLinePoints;
                if (ImGui.SliderInt("Max Line Points", ref maxLinePoints, 50, 2000))
                {
                    Plugin.Configuration.MaxLinePoints = maxLinePoints;
                    Plugin.Configuration.Save();
                }
                ImGuiComponents.HelpMarker(
                    "Maximum number of points to store for each player's path. Lower values improve performance."
                );
            }
            #endregion

            SectionSeparator("Misc. Settings");
            #region Misc Settings

            bool trackOthers = Plugin.Configuration.TrackOthers;
            if (ImGui.Checkbox("Track Other Players", ref trackOthers))
            {
                Plugin.Configuration.TrackOthers = trackOthers;
                Plugin.Configuration.Save();
            }

            if (ImGui.Button("Clear racing lines"))
            {
                foreach (var actor in Plugin.trackedPlayers.Values)
                {
                    actor.ClearLine();
                }
            }

            #endregion

#if DEBUG
            if (ImGui.Button("Debug Print Database"))
            {
                var routes = Plugin.Storage.GetRoutes().Query().ToList();

                foreach (var route in routes)
                {
                    try
                    {
                        Plugin.Log.Debug(
                            route.Name + ": " + route.Records.Count.ToString() + " records."
                        );
                        foreach (var record in route.Records)
                        {
                            try
                            {
                                Plugin.Log.Debug(record.GetCSV());
                            }
                            catch (Exception ex)
                            {
                                Plugin.Log.Error(ex.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error(ex.ToString());
                    }
                }
            }
#endif
        }

        private void DisplayFontSelector()
        {
            var chooser = SingleFontChooserDialog.CreateAuto(
                (UiBuilder)Plugin.PluginInterface.UiBuilder
            );
            if (Plugin.Configuration.TimerFont is SingleFontSpec font)
            {
                chooser.SelectedFont = font;
            }
            chooser.SelectedFontSpecChanged += Chooser_SelectedFontSpecChanged;
        }

        private void Chooser_SelectedFontSpecChanged(SingleFontSpec font)
        {
            Plugin.Configuration.TimerFont = font;
            Plugin.FontManager.Handle = Plugin.Configuration.TimerFont.CreateFontHandle(
                Plugin.PluginInterface.UiBuilder.FontAtlas
            );
            Plugin.Configuration.Save();
        }
    }
}
