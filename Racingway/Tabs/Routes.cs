using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using LiteDB;
using Racingway.Race;
using Racingway.Race.Collision.Triggers;
using Racingway.Utils;
using static Dalamud.Interface.Utility.Raii.ImRaii;

namespace Racingway.Tabs
{
    public class Routes : ITab
    {
        public string Name => "Routes";

        private Plugin Plugin { get; }

        private bool hasStart = false;
        private bool hasFinish = false;

        public Routes(Plugin plugin)
        {
            this.Plugin = plugin;
            updateStartFinishBools();
        }

        public void Dispose() { }

        public void Draw()
        {
            if (Plugin.CurrentAddress == null)
            {
                ImGui.TextUnformatted("No address currently loaded!");
                return;
            }

            if (Plugin.ClientState.LocalPlayer == null)
            {
                ImGui.TextUnformatted("Player is currently null!");
                return;
            }

            if (Plugin.ClientState.LocalPlayer != null)
                ImGui.Text($"Current position: {Plugin.ClientState.LocalPlayer.Position.ToString()}");

            int id = 0;

            using (var tree = ImRaii.TreeNode("Routes"))
            {
                if (tree.Success)
                {
                    foreach (Route route in Plugin.LoadedRoutes)
                    {
                        id++;
                        if (
                            ImGui.Selectable(
                                $"{route.Name}##{id}",
                                route.Id == Plugin.SelectedRoute
                            )
                        )
                        {
                            if (route.Id == Plugin.SelectedRoute)
                                return;
                            Plugin.SelectedRoute = route.Id;
                        }
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip(route.Id.ToString());
                        }
                    }
                }
            }

            Route? selectedRoute = Plugin.LoadedRoutes.FirstOrDefault(
                x => x.Id == Plugin.SelectedRoute,
                new Route(
                    string.Empty,
                    Plugin.CurrentAddress,
                    string.Empty,
                    new List<ITrigger>(),
                    new List<Record>()
                )
            );

            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport))
            {
                ShareHelper.ImportRouteFromClipboard(Plugin);
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Import config from clipboard.");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileExport))
            {
                ShareHelper.ExportRouteToClipboard(selectedRoute);
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Export config to clipboard.");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Recycle))
            {
                Plugin.territoryHelper.GetLocationID();
            }

            if (selectedRoute == null)
            {
                ImGui.Text("If you're seeing this text, consider pressing the refresh button.");
                return;
            }

            DrawRouteSettings(ref selectedRoute);

            ImGui.Spacing();

            DrawTriggerSettings(ref id, ref selectedRoute);
        }

        public void DrawRouteSettings(ref Route selectedRoute)
        {
            string name = selectedRoute.Name;
            if (ImGui.InputText("Name", ref name, 64) && name != selectedRoute.Name)
            {
                if (name == string.Empty)
                    return;

                selectedRoute.Name = name;
                updateRoute(selectedRoute);
            }

            string description = selectedRoute.Description;
            if (
                ImGui.InputText("Description", ref description, 256)
                && description != selectedRoute.Description
            )
            {
                selectedRoute.Description = description;
                updateRoute(selectedRoute);
            }

            if (selectedRoute != null && selectedRoute.Id != ObjectId.Empty &&
                ImGui.CollapsingHeader("Behavior Settings"))
            {
                ImGui.Indent();

                bool allowMounts = selectedRoute.AllowMounts;
                if (ImGui.Checkbox("Allow Mounts", ref allowMounts))
                {
                    selectedRoute.AllowMounts = allowMounts;
                    updateRoute(selectedRoute);
                }

                bool requireGroundedStart = selectedRoute.RequireGroundedStart;
                if (ImGui.Checkbox("Start when not grounded", ref requireGroundedStart))
                {
                    selectedRoute.RequireGroundedStart = requireGroundedStart;
                    updateRoute(selectedRoute);
                }

                bool requireGroundedFinish = selectedRoute.RequireGroundedFinish;
                if (ImGui.Checkbox("Finish when grounded", ref requireGroundedFinish))
                {
                    selectedRoute.RequireGroundedFinish = requireGroundedFinish;
                    updateRoute(selectedRoute);
                }

                ImGui.Unindent();
            }

            if (selectedRoute != null)
                DrawAutocleanupSettings(ref selectedRoute);
        }

        public void DrawAutocleanupSettings(ref Route selectedRoute)
        {
            // Add database cleanup section
            if (
                selectedRoute != null
                && selectedRoute.Id != ObjectId.Empty
                && ImGui.CollapsingHeader("Database Cleanup Settings")
            )
            {
                ImGui.Indent();

                bool autoCleanupEnabled = selectedRoute.AutoCleanupEnabled;
                if (ImGui.Checkbox("Enable Automatic Cleanup", ref autoCleanupEnabled))
                {
                    selectedRoute.AutoCleanupEnabled = autoCleanupEnabled;
                    updateRoute(selectedRoute);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        "Automatically clean up old records after each race to maintain database size"
                    );
                }

                if (autoCleanupEnabled)
                {
                    ImGui.Separator();

                    // Number of maximum records to keep
                    int maxRecordsToKeep = selectedRoute.MaxRecordsToKeep;
                    if (ImGui.SliderInt("Maximum Records", ref maxRecordsToKeep, 10, 1000))
                    {
                        selectedRoute.MaxRecordsToKeep = maxRecordsToKeep;
                        updateRoute(selectedRoute);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Maximum number of records to keep for this route");
                    }

                    // Top N records to always keep
                    int keepTopNRecords = selectedRoute.KeepTopNRecords;
                    if (ImGui.SliderInt("Keep Top Records", ref keepTopNRecords, 1, 100))
                    {
                        selectedRoute.KeepTopNRecords = keepTopNRecords;
                        updateRoute(selectedRoute);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(
                            "Number of top records (fastest times) to always keep regardless of age"
                        );
                    }

                    // Keep Personal Bests option (keep at top for importance)
                    bool keepPersonalBests = selectedRoute.KeepPersonalBests;
                    if (ImGui.Checkbox("Keep Personal Best Times", ref keepPersonalBests))
                    {
                        selectedRoute.KeepPersonalBests = keepPersonalBests;
                        updateRoute(selectedRoute);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(
                            "Always keep each player's personal best record, even if it would otherwise be removed by other filters"
                        );
                    }

                    ImGui.Separator();
                    ImGui.Text("Cleanup Filters:");

                    // Delete old records option
                    bool deleteOldRecordsEnabled = selectedRoute.DeleteOldRecordsEnabled;
                    if (ImGui.Checkbox("Delete Records Older Than", ref deleteOldRecordsEnabled))
                    {
                        selectedRoute.DeleteOldRecordsEnabled = deleteOldRecordsEnabled;
                        updateRoute(selectedRoute);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(
                            "Remove records that are older than the specified number of days"
                        );
                    }

                    if (deleteOldRecordsEnabled)
                    {
                        ImGui.SameLine();
                        int maxDaysToKeep = selectedRoute.MaxDaysToKeep;
                        if (ImGui.SliderInt("##DaysToKeep", ref maxDaysToKeep, 1, 365, "%d days"))
                        {
                            selectedRoute.MaxDaysToKeep = maxDaysToKeep;
                            updateRoute(selectedRoute);
                        }
                    }

                    // Time threshold filtering
                    bool filterByTimeEnabled = selectedRoute.FilterByTimeEnabled;
                    if (ImGui.Checkbox("Filter By Time Range", ref filterByTimeEnabled))
                    {
                        selectedRoute.FilterByTimeEnabled = filterByTimeEnabled;
                        updateRoute(selectedRoute);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(
                            "Filter out records based on completion time (useful for removing junk/abandoned or unusually long runs)"
                        );
                    }

                    if (filterByTimeEnabled)
                    {
                        ImGui.Indent();

                        // Minimum time threshold
                        float minTimeThreshold = selectedRoute.MinTimeThreshold;
                        if (
                            ImGui.SliderFloat(
                                "Minimum Time",
                                ref minTimeThreshold,
                                0.0f,
                                60.0f,
                                "%.1f seconds"
                            )
                        )
                        {
                            selectedRoute.MinTimeThreshold = minTimeThreshold;
                            updateRoute(selectedRoute);
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(
                                "Records with time less than this will be removed (good for filtering out abandoned runs)"
                            );
                        }

                        // Maximum time threshold
                        float maxTimeThreshold = selectedRoute.MaxTimeThreshold;
                        if (
                            ImGui.SliderFloat(
                                "Maximum Time",
                                ref maxTimeThreshold,
                                0.0f,
                                3600.0f,
                                maxTimeThreshold > 0 ? "%.1f seconds" : "No Limit"
                            )
                        )
                        {
                            selectedRoute.MaxTimeThreshold = maxTimeThreshold;
                            updateRoute(selectedRoute);
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(
                                "Records with time greater than this will be removed (set to 0 for no upper limit)"
                            );
                        }

                        ImGui.Unindent();
                    }

                    ImGui.Separator();

                    // Run cleanup manually
                    if (ImGui.Button("Run Cleanup Now"))
                    {
                        int removed = selectedRoute.ApplyCleanupRules();
                        updateRoute(selectedRoute);
                        Plugin.ChatGui.Print(
                            $"[RACE] Removed {removed} records from '{selectedRoute.Name}'"
                        );
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Manually clean up records based on above settings");
                    }

                    // Display current record count and stats
                    int recordCount = selectedRoute.Records?.Count ?? 0;
                    ImGui.Text($"Current Record Count: {recordCount}");

                    // Show database stats
                    if (Plugin.Storage != null)
                    {
                        string dbSize = Plugin.Storage.GetFileSizeString();
                        ImGui.Text($"Database Size: {dbSize}");
                    }
                }

                ImGui.Unindent();
            }
        }

        private void DrawTriggerSettings(ref int id, ref Route selectedRoute)
        {
            using (ImRaii.ItemWidth(50))
            {
                if (ImGui.Button("Add Trigger"))
                {
                    // We set the trigger position slightly below the player due to SE position jank.
                    Checkpoint newTrigger = new Checkpoint(
                        selectedRoute,
                        Plugin.ClientState.LocalPlayer.Position - new Vector3(0, 0.1f, 0),
                        Vector3.One,
                        Vector3.Zero
                    );
                    selectedRoute.Triggers.Add(newTrigger);
                    updateRoute(selectedRoute);
                }

                if (Plugin.SelectedTrigger != null)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("Stop Editing Trigger"))
                    {
                        Plugin.SelectedTrigger = null;
                    }
                }

                ImGui.SameLine();
                bool useSnap = Plugin.Configuration.UseSnapping;
                if (ImGui.Checkbox("Use Snap", ref useSnap))
                {
                    Plugin.Configuration.UseSnapping = useSnap;
                    Plugin.Configuration.Save();
                }

                ImGui.SameLine();
                float snapDistance = Plugin.Configuration.SnapDistance;
                if (ImGui.DragFloat("Snap Distance", ref snapDistance, 0.001f))
                {
                    Plugin.Configuration.SnapDistance = snapDistance;
                    Plugin.Configuration.Save();
                }
            }

            ImGui.Separator();

            using (var child = ImRaii.Child("###triggerChildren")) 
            {
                bool hoveredATrigger = false;

                for (int i = 0; i < selectedRoute.Triggers.Count; i++)
                {
                    var cursorPos = ImGui.GetCursorPos();

                    ITrigger trigger = selectedRoute.Triggers[i];

                    var ctrl = ImGui.GetIO().KeyCtrl;

                    // Disable delete button if not holding ctrl
                    using (_ = ImRaii.Disabled(!ctrl))
                    {
                        if (ImGuiComponents.IconButton(id, FontAwesomeIcon.Eraser))
                        {
                            updateStartFinishBools();
                            selectedRoute.Triggers.Remove(trigger);
                            updateRoute(selectedRoute);
                            continue;
                        }
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip("Ctrl + Click to erase this trigger.");
                        }
                    }

                    id++;
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButton(id, FontAwesomeIcon.ArrowsToDot))
                    {
                        // We set the trigger position slightly below the player due to SE position jank.
                        selectedRoute.Triggers[i].Cube.Position =
                            Plugin.ClientState.LocalPlayer.Position - new Vector3(0, 0.1f, 0);
                        Plugin.ChatGui.Print($"[RACE] Trigger position set to {trigger.Cube.Position}");

                        updateRoute(selectedRoute);
                    }
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.SetTooltip("Set trigger position to your characters position.");
                    }

                    id++;
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButton(id, FontAwesomeIcon.RulerCombined))
                    {
                        Plugin.SelectedTrigger = trigger;
                    }
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.SetTooltip("Edit using the gizmo");
                    }

                    using (ImRaii.ItemWidth(50))
                    {
                        id++;
                        ImGui.SameLine();
                        if (ImGui.Selectable($"{trigger.GetType().Name}##{id}"))
                        {
                            ImGui.OpenPopup($"TriggerPopup##{id}");
                        }
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip("Change trigger type");
                        }
                    }

                    using (var popup = ImRaii.Popup($"TriggerPopup##{id}"))
                    {
                        if (popup.Success)
                        {
                            if (ImGui.Selectable("Start", trigger is Start))
                            {
                                if (trigger is Start) return;

                                if (hasStart)
                                {
                                    Plugin.ChatGui.PrintError("[RACE] There is already a start trigger in this route.");
                                }
                                else
                                {
                                    selectedRoute.Triggers[i] = new Start(trigger.Route, trigger.Cube);
                                    updateRoute(selectedRoute);
                                }
                            }

                            if (ImGui.Selectable("Checkpoint", trigger is Checkpoint))
                            {
                                if (trigger is Checkpoint) return;

                                selectedRoute.Triggers[i] = new Checkpoint(trigger.Route, trigger.Cube);
                                Plugin.SelectedTrigger = selectedRoute.Triggers[i];
                                updateRoute(selectedRoute);
                            }

                            if (ImGui.Selectable("Fail", trigger is Fail))
                            {
                                if (trigger is Fail) return;
                                selectedRoute.Triggers[i] = new Fail(trigger.Route, trigger.Cube);
                                Plugin.SelectedTrigger = selectedRoute.Triggers[i];
                                updateRoute(selectedRoute);
                            }

                            if (ImGui.Selectable("Finish", trigger is Finish))
                            {
                                if (trigger is Finish) return;

                                selectedRoute.Triggers[i] = new Finish(trigger.Route, trigger.Cube);
                                Plugin.SelectedTrigger = selectedRoute.Triggers[i];
                                updateRoute(selectedRoute);
                            }

                            if (ImGui.Selectable("Loop", trigger is Loop))
                            {
                                if (trigger is Loop) return;

                                selectedRoute.Triggers[i] = new Loop(trigger.Route, trigger.Cube);
                                Plugin.SelectedTrigger = selectedRoute.Triggers[i];
                                updateRoute(selectedRoute);
                            }
                        }
                    }

                    id++;
                    Vector3 position = trigger.Cube.Position;
                    if (ImGui.DragFloat3($"Position##{id}", ref position, 0.1f))
                    {
                        selectedRoute.Triggers[i].Cube.Position = position;
                        updateRoute(selectedRoute);
                    }

                    // Move trigger UP button
                    if (i > 0)
                    {
                        ImGui.SameLine();
                        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X - 50f, 0f));
                        ImGui.SameLine();
                        id++;

                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                        if (ImGui.ArrowButton($"arrowUp##{id}", ImGuiDir.Up))
                        {
                            ITrigger currTrigger = selectedRoute.Triggers[i];
                            selectedRoute.Triggers.RemoveAt(i);
                            selectedRoute.Triggers.Insert(i - 1, currTrigger);
                            updateRoute(selectedRoute);
                        }
                        ImGui.PopStyleColor();
                    }

                    id++;
                    Vector3 scale = trigger.Cube.Scale;
                    if (ImGui.DragFloat3($"Scale##{id}", ref scale, 0.1f, 0.01f, float.MaxValue))
                    {
                        selectedRoute.Triggers[i].Cube.Scale = scale;
                        selectedRoute.Triggers[i].Cube.UpdateVerts();
                        updateRoute(selectedRoute);
                    }

                    id++;
                    Vector3 rotation = trigger.Cube.Rotation;
                    if (ImGui.DragFloat3($"Rotation##{id}", ref rotation, 0.1f))
                    {
                        selectedRoute.Triggers[i].Cube.Rotation = rotation;
                        updateRoute(selectedRoute);
                    }

                    // Move trigger DOWN button
                    if (i < selectedRoute.Triggers.Count - 1)
                    {
                        ImGui.SameLine();
                        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X - 50f, 0f));
                        ImGui.SameLine();
                        id++;

                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                        if (ImGui.ArrowButton($"arrowDown##{id}", ImGuiDir.Down))
                        {
                            ITrigger currTrigger = selectedRoute.Triggers[i];
                            selectedRoute.Triggers.RemoveAt(i);
                            selectedRoute.Triggers.Insert(i + 1, currTrigger);
                            updateRoute(selectedRoute);
                        }
                        ImGui.PopStyleColor();
                    }

                    var afterPos = ImGui.GetCursorPos();

                    id++;
                    using (_ = ImRaii.PushColor(ImGuiCol.HeaderHovered, new Vector4(1, 1, 1, 0.05f)))
                    {
                        using (_ = ImRaii.PushColor(ImGuiCol.HeaderActive, new Vector4(1, 1, 1, 0.07f)))
                        {
                            ImGui.SetCursorPos(cursorPos);

                            using (ImRaii.Disabled())
                                ImGui.Selectable($"###{id}", false, ImGuiSelectableFlags.AllowItemOverlap, afterPos - cursorPos);

                            ImGui.SetCursorPos(afterPos);
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                Plugin.HoveredTrigger = trigger;
                                hoveredATrigger = true;
                            }
                        }
                    }
                    
                    ImGui.Separator();
                }

                if (!hoveredATrigger)
                {
                    Plugin.HoveredTrigger = null;
                }
            }
        }

        private void updateRoute(Route route)
        {
            if (route == null)
                return;

            int index = Plugin.LoadedRoutes.FindIndex(x => x.Id == Plugin.SelectedRoute);
            if (index == -1)
            {
                index = Plugin.LoadedRoutes.FindIndex(x => x == route);
            }

            if (index != -1)
            {
                Plugin.LoadedRoutes[index] = route;
            }
            else
            {
                Plugin.LoadedRoutes.Add(route);
            }

            Plugin.SelectedRoute = route.Id;

            Plugin.SubscribeToRouteEvents();
            updateStartFinishBools();

            Plugin.DataQueue.QueueDataOperation(async () =>
            {
                await Plugin.Storage.AddRoute(route);
                Plugin.Storage.UpdateRouteCache();
            });
        }

        private void updateStartFinishBools()
        {
            try
            {
                if (Plugin.LoadedRoutes.Count == 0)
                    return;

                Route selectedRoute = Plugin.LoadedRoutes.First(x => x.Id == Plugin.SelectedRoute);

                hasStart = false;
                hasFinish = false;

                foreach (ITrigger trigger in selectedRoute.Triggers)
                {
                    if (trigger is Start)
                    {
                        hasStart = true;
                    }
                    if (trigger is Finish)
                    {
                        hasFinish = true;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e.ToString());
            }
        }
    }
}
