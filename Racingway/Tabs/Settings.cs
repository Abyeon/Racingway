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

        private bool _isPopupOpen = true;

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
                    actor.raceLine.Clear();
                }
            }

            #endregion

            SectionSeparator("Database Management");
            #region Database Management

            // Auto-cleanup settings
            bool enableAutoCleanup = Plugin.Configuration.EnableAutoCleanup;
            if (ImGui.Checkbox("Enable Auto-Cleanup of Records", ref enableAutoCleanup))
            {
                Plugin.Configuration.EnableAutoCleanup = enableAutoCleanup;
                Plugin.Configuration.Save();
            }

            ImGuiComponents.HelpMarker(
                "When enabled, records will be automatically filtered based on the criteria below."
            );

            using (_ = ImRaii.ItemWidth(200f))
            {
                // Min Time Filter
                float minTimeFilter = Plugin.Configuration.MinTimeFilter;
                if (ImGui.DragFloat("Minimum Time (seconds)", ref minTimeFilter, 0.1f, 0f, 120f))
                {
                    Plugin.Configuration.MinTimeFilter = minTimeFilter < 0 ? 0 : minTimeFilter;
                    Plugin.Configuration.Save();
                }
                ImGuiComponents.HelpMarker(
                    "Records with completion time less than this will be removed when cleanup runs. Set to 0 to disable."
                );

                // Max Records per Route
                int maxRecordsPerRoute = Plugin.Configuration.MaxRecordsPerRoute;
                if (ImGui.DragInt("Max Records per Route", ref maxRecordsPerRoute, 1, 0, 1000))
                {
                    Plugin.Configuration.MaxRecordsPerRoute =
                        maxRecordsPerRoute < 0 ? 0 : maxRecordsPerRoute;
                    Plugin.Configuration.Save();
                }
                ImGuiComponents.HelpMarker(
                    "Only keep top N fastest records per route. Set to 0 to keep all records."
                );

                // Remove Non-Client Records
                bool removeNonClientRecords = Plugin.Configuration.RemoveNonClientRecords;
                if (ImGui.Checkbox("Remove Other Players' Records", ref removeNonClientRecords))
                {
                    Plugin.Configuration.RemoveNonClientRecords = removeNonClientRecords;
                    Plugin.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("When enabled, only your own records will be kept.");

                // Keep Personal Best Only
                bool keepPersonalBestOnly = Plugin.Configuration.KeepPersonalBestOnly;
                if (ImGui.Checkbox("Keep Only Personal Bests", ref keepPersonalBestOnly))
                {
                    Plugin.Configuration.KeepPersonalBestOnly = keepPersonalBestOnly;
                    Plugin.Configuration.Save();
                }
                ImGuiComponents.HelpMarker(
                    "When enabled, only your personal best time for each route will be kept."
                );
            }

            ImGui.Separator();
            // Add a section for advanced path settings
            if (ImGui.TreeNode("Advanced Path Settings"))
            {
                using (_ = ImRaii.ItemWidth(200f))
                {
                    int maxPathPoints = Plugin.Configuration.MaxPathSamplingPoints;
                    if (ImGui.DragInt("Max Path Points", ref maxPathPoints, 1, 100, 2000))
                    {
                        Plugin.Configuration.MaxPathSamplingPoints =
                            maxPathPoints < 100 ? 100 : maxPathPoints;
                        Plugin.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker(
                        "Maximum number of points to store for each race path. Higher values = more accurate paths but larger database size."
                    );

                    float simplificationTolerance = Plugin
                        .Configuration
                        .PathSimplificationTolerance;
                    if (
                        ImGui.DragFloat(
                            "Path Simplification Tolerance",
                            ref simplificationTolerance,
                            0.01f,
                            0.05f,
                            2.0f
                        )
                    )
                    {
                        Plugin.Configuration.PathSimplificationTolerance = simplificationTolerance;
                        Plugin.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker(
                        "Lower values = more accurate paths but larger database size."
                    );
                }
                ImGui.TreePop();
            }

            // Add a button to manually run cleanup now
            if (ImGui.Button("Run Cleanup Now"))
            {
                ImGui.OpenPopup("Confirm Cleanup");
            }

            // Confirmation popup
            if (
                ImGui.BeginPopupModal(
                    "Confirm Cleanup",
                    ref _isPopupOpen,
                    ImGuiWindowFlags.AlwaysAutoResize
                )
            )
            {
                ImGui.Text("This will immediately delete records based on your filter settings.");
                ImGui.Text("This action cannot be undone. Are you sure?");
                ImGui.Separator();

                if (ImGui.Button("Confirm", new Vector2(120, 0)))
                {
                    RunDatabaseCleanup();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            // Add a button to compact database
            if (ImGui.Button("Compact Database"))
            {
                ImGui.OpenPopup("Confirm Compact");
            }

            // Confirmation popup for compacting
            if (
                ImGui.BeginPopupModal(
                    "Confirm Compact",
                    ref _isPopupOpen,
                    ImGuiWindowFlags.AlwaysAutoResize
                )
            )
            {
                ImGui.Text("This will rebuild the database to reduce its size.");
                ImGui.Text("This process may take some time. Continue?");
                ImGui.Separator();

                if (ImGui.Button("Confirm", new Vector2(120, 0)))
                {
                    Plugin.DataQueue.QueueDataOperation(async () =>
                    {
                        try
                        {
                            await Plugin.Storage.CompactDatabase();
                            Plugin.ChatGui.Print(
                                "[RACE] Database has been compacted successfully."
                            );
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log.Error(ex, "Error compacting database");
                            Plugin.ChatGui.PrintError(
                                "[RACE] Error compacting database. See logs for details."
                            );
                        }
                    });
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            // Show database file size
            string dbSize = Plugin.Storage.GetFileSizeString();
            if (!string.IsNullOrEmpty(dbSize))
            {
                ImGui.Text($"Current Database Size: {dbSize}");
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

        private void RunDatabaseCleanup()
        {
            // Run the cleanup in a background task
            Plugin.DataQueue.QueueDataOperation(async () =>
            {
                try
                {
                    await Plugin.Storage.CleanupRecords(
                        Plugin.Configuration.MinTimeFilter,
                        Plugin.Configuration.MaxRecordsPerRoute,
                        Plugin.Configuration.RemoveNonClientRecords,
                        Plugin.Configuration.KeepPersonalBestOnly
                    );
                    Plugin.ChatGui.Print("[RACE] Records cleanup completed successfully.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Error cleaning up records");
                    Plugin.ChatGui.PrintError(
                        "[RACE] Error cleaning up records. See logs for details."
                    );
                }
            });
        }
    }
}
