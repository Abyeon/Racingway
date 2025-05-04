using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using LiteDB;
//using Newtonsoft.Json;
using Racingway.Race;
using Racingway.Race.Collision;
using Racingway.Race.Collision.Triggers;
using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Tabs
{
    public class Routes : ITab
    {
        public string Name => "Routes";

        private Plugin Plugin { get; }

        private bool hasStart = false;
        private bool hasFinish = false;
        private bool _isPopupOpen = true;

        public Routes(Plugin plugin)
        {
            this.Plugin = plugin;
            updateStartFinishBools();
        }

        public void Dispose()
        {

        }

        public void Draw()
        {
            ImGui.Text($"Current position: {Plugin.ClientState.LocalPlayer.Position.ToString()}");

            int id = 0;

            using (var tree = ImRaii.TreeNode("Routes"))
            {
                if (tree.Success)
                {
                    foreach (Route route in Plugin.LoadedRoutes)
                    {
                        id++;
                        if (ImGui.Selectable($"{route.Name}##{id}", route.Id == Plugin.SelectedRoute))
                        {
                            if (route.Id == Plugin.SelectedRoute) return;
                            Plugin.SelectedRoute = route.Id;
                        }
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip(route.Id.ToString());
                        }
                    }
                }
            }

            Route? selectedRoute = Plugin.LoadedRoutes.FirstOrDefault(x => x.Id == Plugin.SelectedRoute, new Route(string.Empty, Plugin.CurrentAddress, string.Empty, new List<ITrigger>(), new List<Record>()));

            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.FileImport))
            {
                string data = ImGui.GetClipboardText();

                Plugin.DataQueue.QueueDataOperation(async () =>
                {
                    await Plugin.Storage.ImportRouteFromBase64(data);
                });
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Import config from clipboard.");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.FileExport))
            {
                string input = JsonSerializer.Serialize(selectedRoute.GetEmptySerialized());
                string text = Compression.ToCompressedBase64(input);
                if (text != string.Empty)
                {
                    ImGui.SetClipboardText(text);
                } else
                {
                    Plugin.ChatGui.PrintError("[RACE] No route selected.");
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Export config to clipboard.");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Recycle))
            {
                Plugin.territoryHelper.GetLocationID();
            }

            if (selectedRoute == null)
            {
                ImGui.Text("If you're seeing this text, consider pressing the refresh button.");
                return;
            }

            string name = selectedRoute.Name;
            if (ImGui.InputText("Name", ref name, 64) && name != selectedRoute.Name)
            {
                // Something
                if (name == string.Empty) return;
                selectedRoute.Name = name;
                updateRoute(selectedRoute);
            }

            string description = selectedRoute.Description;
            if (ImGui.InputText("Description", ref description, 256) && description != selectedRoute.Description)
            {
                selectedRoute.Description = description;
                updateRoute(selectedRoute);
            }

            // Add Auto-Cleanup Settings section
            if (ImGui.TreeNode("Auto-Cleanup Settings"))
            {
                bool enableAutoCleanup = selectedRoute.EnableAutoCleanup;
                if (ImGui.Checkbox("Enable Auto-Cleanup for this Route", ref enableAutoCleanup))
                {
                    selectedRoute.EnableAutoCleanup = enableAutoCleanup;
                    updateRoute(selectedRoute);
                }
                ImGuiComponents.HelpMarker(
                    "When enabled, records for this route will be automatically filtered based on the criteria below."
                );

                using (_ = ImRaii.ItemWidth(200f))
                {
                    // Min Time Filter
                    float minTimeFilter = selectedRoute.MinTimeFilter;
                    if (
                        ImGui.DragFloat("Minimum Time (seconds)", ref minTimeFilter, 0.1f, 0f, 120f)
                    )
                    {
                        selectedRoute.MinTimeFilter = minTimeFilter < 0 ? 0 : minTimeFilter;
                        updateRoute(selectedRoute);
                    }
                    ImGuiComponents.HelpMarker(
                        "Records with completion time less than this will be removed when cleanup runs. Set to 0 to disable."
                    );

                    // Max Records to Keep
                    int maxRecordsToKeep = selectedRoute.MaxRecordsToKeep;
                    if (ImGui.DragInt("Max Records to Keep", ref maxRecordsToKeep, 1, 0, 1000))
                    {
                        selectedRoute.MaxRecordsToKeep =
                            maxRecordsToKeep < 0 ? 0 : maxRecordsToKeep;
                        updateRoute(selectedRoute);
                    }
                    ImGuiComponents.HelpMarker(
                        "Only keep top N fastest records for this route. Set to 0 to keep all records."
                    );

                    // Remove Non-Client Records
                    bool removeNonClientRecords = selectedRoute.RemoveNonClientRecords;
                    if (ImGui.Checkbox("Remove Other Players' Records", ref removeNonClientRecords))
                    {
                        selectedRoute.RemoveNonClientRecords = removeNonClientRecords;
                        updateRoute(selectedRoute);
                    }
                    ImGuiComponents.HelpMarker(
                        "When enabled, only your own records will be kept for this route."
                    );

                    // Keep Personal Best Only
                    bool keepPersonalBestOnly = selectedRoute.KeepPersonalBestOnly;
                    if (ImGui.Checkbox("Keep Only Personal Bests", ref keepPersonalBestOnly))
                    {
                        selectedRoute.KeepPersonalBestOnly = keepPersonalBestOnly;
                        updateRoute(selectedRoute);
                    }
                    ImGuiComponents.HelpMarker(
                        "When enabled, only personal best time for each player will be kept for this route."
                    );

                    // Add a button to manually run cleanup just for this route
                    if (ImGui.Button("Clean Up Records Now"))
                    {
                        ImGui.OpenPopup("Confirm Route Cleanup");
                    }

                    // Confirmation popup
                    if (
                        ImGui.BeginPopupModal(
                            "Confirm Route Cleanup",
                            ref _isPopupOpen,
                            ImGuiWindowFlags.AlwaysAutoResize
                        )
                    )
                    {
                        ImGui.Text(
                            "This will immediately delete records for this route based on your filter settings."
                        );
                        ImGui.Text("This action cannot be undone. Are you sure?");
                        ImGui.Separator();

                        if (ImGui.Button("Confirm", new Vector2(120, 0)))
                        {
                            RunRouteCleanup(selectedRoute);
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Cancel", new Vector2(120, 0)))
                        {
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.EndPopup();
                    }
                }
                ImGui.TreePop();
            }

            if (ImGui.Button("Add Trigger"))
            {
                // We set the trigger position slightly below the player due to SE position jank.
                Checkpoint newTrigger = new Checkpoint(selectedRoute, Plugin.ClientState.LocalPlayer.Position - new Vector3(0, 0.1f, 0), Vector3.One, Vector3.Zero);
                selectedRoute.Triggers.Add(newTrigger);
                updateRoute(selectedRoute);
            }

            for (int i = 0; i < selectedRoute.Triggers.Count; i++)
            {
                ITrigger trigger = selectedRoute.Triggers[i];

                ImGui.Separator();
                if (ImGuiComponents.IconButton(id, Dalamud.Interface.FontAwesomeIcon.Eraser))
                {
                    updateStartFinishBools();
                    selectedRoute.Triggers.Remove(trigger);
                    updateRoute(selectedRoute);
                    continue;
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Erase this trigger.");
                }

                id++;
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(id, Dalamud.Interface.FontAwesomeIcon.ArrowsToDot))
                {
                    // We set the trigger position slightly below the player due to SE position jank.
                    selectedRoute.Triggers[i].Cube.Position = Plugin.ClientState.LocalPlayer.Position - new Vector3(0, 0.1f, 0);
                    Plugin.ChatGui.Print($"[RACE] Trigger position set to {trigger.Cube.Position}");

                    updateRoute(selectedRoute);
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Set trigger position to your characters position.");
                }

                ImGui.SameLine();
                if (ImGui.TreeNode($"Type##{id}"))
                {
                    ImGui.Indent();

                    if (ImGui.Selectable("Start", trigger is Start))
                    {
                        if (trigger is Start) return;

                        if (hasStart)
                        {
                            Plugin.ChatGui.PrintError("[RACE] There is already a start trigger in this route.");
                        } else
                        {
                            selectedRoute.Triggers[i] = new Start(trigger.Route, trigger.Cube);
                            updateRoute(selectedRoute);
                        }
                    }

                    if (ImGui.Selectable("Checkpoint", trigger is Checkpoint))
                    {
                        if (trigger is Checkpoint) return;
                        selectedRoute.Triggers[i] = new Checkpoint(trigger.Route, trigger.Cube);
                        updateRoute(selectedRoute);
                    }

                    if (ImGui.Selectable("Fail", trigger is Fail))
                    {
                        if (trigger is Fail) return;
                        selectedRoute.Triggers[i] = new Fail(trigger.Route, trigger.Cube);
                        updateRoute(selectedRoute);
                    }

                    if (ImGui.Selectable("Finish", trigger is Finish))
                    {
                        if (trigger is Finish) return;

                        if (hasFinish)
                        {
                            Plugin.ChatGui.PrintError("[RACE] There is already a finish trigger in this route.");
                        }
                        else
                        {
                            selectedRoute.Triggers[i] = new Finish(trigger.Route, trigger.Cube);
                            updateRoute(selectedRoute);
                        }
                    }

                    ImGui.Unindent();

                    ImGui.TreePop();
                }

                id++;
                Vector3 position = trigger.Cube.Position;
                if (ImGui.DragFloat3($"Position##{id}", ref position, 0.1f))
                {
                    selectedRoute.Triggers[i].Cube.Position = position;
                    updateRoute(selectedRoute);
                }

                id++;
                Vector3 scale = trigger.Cube.Scale;
                if (ImGui.DragFloat3($"Scale##{id}", ref scale, 0.1f))
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
            }

            ImGui.Spacing();
        }

        private void updateRoute(Route route)
        {
            if (route == null) return;

            int index = Plugin.LoadedRoutes.FindIndex(x => x.Id == Plugin.SelectedRoute);
            if (index == -1)
            {
                index = Plugin.LoadedRoutes.FindIndex(x => x == route);
            }

            if (index != -1)
            {
                Plugin.LoadedRoutes[index] = route;
            } else
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
                if (Plugin.LoadedRoutes.Count == 0) return;

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
            } catch (Exception e)
            {
                Plugin.Log.Error(e.ToString());
            }
        }

        private void RunRouteCleanup(Route route)
        {
            // Run the cleanup for just this route in a background task
            Plugin.DataQueue.QueueDataOperation(async () =>
            {
                try
                {
                    int recordsBefore = route.Records.Count;

                    // Apply filtering based on route's specific settings
                    var filteredRecords = new List<Record>(route.Records);

                    // 1. Apply time filter
                    if (route.MinTimeFilter > 0)
                    {
                        filteredRecords = filteredRecords
                            .Where(r => r.Time.TotalSeconds >= route.MinTimeFilter)
                            .ToList();
                    }

                    // 2. Apply client-only filter
                    if (route.RemoveNonClientRecords)
                    {
                        filteredRecords = filteredRecords.Where(r => r.IsClient).ToList();
                    }

                    // 3. Apply personal best only filter
                    if (route.KeepPersonalBestOnly)
                    {
                        // Group by player name and keep only the fastest record per player
                        filteredRecords = filteredRecords
                            .GroupBy(r => r.Name)
                            .Select(g => g.OrderBy(r => r.Time.TotalMilliseconds).First())
                            .ToList();
                    }

                    // 4. Apply max records filter
                    if (
                        route.MaxRecordsToKeep > 0
                        && filteredRecords.Count > route.MaxRecordsToKeep
                    )
                    {
                        filteredRecords = filteredRecords
                            .OrderBy(r => r.Time.TotalMilliseconds)
                            .Take(route.MaxRecordsToKeep)
                            .ToList();
                    }

                    // Update the route with filtered records
                    route.Records = filteredRecords;
                    route.InvalidateRecordCache();
                    int recordsAfter = route.Records.Count;
                    int recordsRemoved = recordsBefore - recordsAfter;

                    // Update route in database
                    await Plugin.Storage.AddRoute(route);

                    // Update route in memory
                    int index = Plugin.LoadedRoutes.FindIndex(x => x.Id == route.Id);
                    if (index != -1)
                    {
                        Plugin.LoadedRoutes[index] = route;
                    }

                    Plugin.ChatGui.Print(
                        $"[RACE] Route cleanup completed: {recordsRemoved} records removed from '{route.Name}'."
                    );
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Error cleaning up route records");
                    Plugin.ChatGui.PrintError(
                        "[RACE] Error cleaning up route records. See logs for details."
                    );
                }
            });
        }
    }
}
