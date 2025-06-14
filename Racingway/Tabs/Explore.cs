using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using LiteDB;
using Lumina.Extensions;
using Racingway.Race;
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
    internal class Explore : ITab
    {
        public string Name => "Explore";

        private Plugin Plugin { get; set; }

        private ImGuiTextFilterPtr filter;

        public unsafe Explore (Plugin plugin)
        {
            this.Plugin = plugin;

            var filterPtr = ImGuiNative.ImGuiTextFilter_ImGuiTextFilter(null);
            filter = new ImGuiTextFilterPtr(filterPtr);
        }

        public void Dispose()
        {

        }

        private enum Search
        {
            None,
            Loaded
        }

        private Search selectedSearch = Search.None;
        private string searchString = string.Empty;

        public void Draw()
        {
            if (Plugin.Storage == null) return;

            Vector2 contentAvailable = ImGui.GetContentRegionAvail();

            float maxWidth = 175;

            Vector2 size = new Vector2(contentAvailable.X * .33f > maxWidth ? maxWidth : contentAvailable.X * 0.33f, contentAvailable.Y);
            using (var child = ImRaii.Child("###race-exploreL", size, true))
            {
                if (child.Success)
                {
                    if (ImGui.Selectable("All Routes", selectedSearch == Search.None))
                    {
                        selectedSearch = Search.None;
                    }

                    if (ImGui.Selectable("Loaded Routes (" + Plugin.LoadedRoutes.Count.ToString() + ")", selectedSearch == Search.Loaded))
                    {
                        selectedSearch = Search.Loaded;
                    }
                }
            }

            ImGui.SameLine();

            using (var child = ImRaii.Child("###race-exploreR", new Vector2(0, ImGui.GetContentRegionAvail().Y), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                if (child.Success)
                {
                    ImGui.Dummy(new Vector2(0, 2f));
                    if (Plugin.CurrentAddress != null && ImGui.Button("New Route"))
                    {
                        Route newRoute = new Route("New Route#" + Plugin.Storage.RouteCache.Count.ToString(), Plugin.CurrentAddress, string.Empty, new List<ITrigger>(), new List<Record>());
                        Plugin.AddRoute(newRoute);
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Import Route"))
                    {
                        ShareHelper.ImportRouteFromClipboard(Plugin);
                    }
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.SetTooltip("Import route from clipboard.");
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Import Record"))
                    {
                        ShareHelper.ImportPackedRecord(Plugin);
                        //ShareHelper.ImportRecordFromClipboard(Plugin);
                    }
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.SetTooltip("Import record from clipboard, this will automatically get assigned to the route if it exists.");
                    }

                    //ImGui.SameLine();
                    //if (ImGui.Button("Import Packed Record"))
                    //{
                    //    ShareHelper.ImportPackedRecord(Plugin);
                    //}

                    filter.Draw("Filter");

                    List<Route> routes = new List<Route>();

                    switch (selectedSearch)
                    {
                        case Search.None:
                            routes = Plugin.Storage.RouteCache.Values.ToList();
                            break;
                        case Search.Loaded:
                            routes = Plugin.LoadedRoutes;
                            break;
                    }

                    using (var table = ImRaii.Table("###race-exploreTable", 2, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Resizable | ImGuiTableFlags.NoBordersInBody))
                    {
                        ImGui.TableSetupColumn("Route", ImGuiTableColumnFlags.None, 100f);
                        ImGui.TableSetupColumn("Best Times", ImGuiTableColumnFlags.None, 75f);
                        ImGui.TableHeadersRow();

                        for (int i = 0; i < routes.Count; i++)
                        {
                            Route route = routes[i];
                            if (!filter.PassFilter(route.Name) && !filter.PassFilter(route.Address.ReadableName)) continue;

                            ImGui.TableNextColumn();
                            var cursorPos = ImGui.GetCursorPos();

                            ImGui.Text(route.Name);
                            ImGui.TextColored(ImGuiColors.DalamudGrey, route.Address.ReadableName);

                            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
                            ImGui.TextWrapped(route.Description);
                            ImGui.PopStyleColor();
                            var afterPos = ImGui.GetCursorPos();

                            using (_ = ImRaii.PushColor(ImGuiCol.HeaderHovered, new Vector4(1, 1, 1, 0.05f)))
                            {
                                using (_ = ImRaii.PushColor(ImGuiCol.HeaderActive, new Vector4(1, 1, 1, 0.07f)))
                                {
                                    ImGui.SetCursorPos(cursorPos);
                                    ImGui.Selectable($"###{route.Name}{i}", false, ImGuiSelectableFlags.AllowItemOverlap, afterPos - cursorPos);
                                    ImGui.SetCursorPos(afterPos);
                                }
                            }

                            using (var popup = ImRaii.ContextPopupItem($"###{route.Name}{i}"))
                            {
                                if (popup.Success)
                                {
                                    if (ImGui.Selectable("Copy Address"))
                                    {
                                        ImGui.SetClipboardText(route.Address.ReadableName);
                                    }
                                    if (ImGui.Selectable("Set Flag"))
                                    {
                                        var start = route.Triggers.FirstOrDefault(t => t.GetType() == typeof(Start) || t.GetType() == typeof(Start), null);
                                        if (start != null)
                                        {
                                            TerritoryHelper.SetFlagMarkerPosition(start.Cube.Position, route.Address.TerritoryId, route.Address.MapId, route.Name, (uint)start.FlagIcon);
                                        } else
                                        {
                                            Plugin.ChatGui.PrintError("[RACE] There appears to be no start trigger in this route.");
                                        }
                                    }
                                    if (ImGui.Selectable("Export to Clipboard"))
                                    {
                                        ShareHelper.ExportRouteToClipboard(route);
                                    }
                                    if (ImGui.Selectable("Export JSON to Clipboard"))
                                    {
                                        ShareHelper.ExportRouteJsonToClipboard(route);
                                    }

                                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.None))
                                    {
                                        ImGui.SetTooltip("Intended only for creating external route lists at the moment.");
                                    }

                                    if (selectedSearch == Search.Loaded) // Hide this button unless it's a loaded route for now
                                    {
                                        if (ImGui.Selectable("Display Records"))
                                        {
                                            if (Plugin.LoadedRoutes.Contains(route))
                                            {
                                                Plugin.MainWindow.SelectTab("Records");
                                                Plugin.SelectedRoute = route.Id;
                                            }
                                        }
                                    }

                                    if (Plugin.LoadedRoutes.Contains(route) && route.Records.Count > 0)
                                    {
                                        if (ImGui.Selectable("Display Best Time"))
                                        {
                                            Plugin.DisplayedRecord = route.Records[0];
                                        }
                                    }

                                    var ctrl = ImGui.GetIO().KeyCtrl;

                                    // Disable delete button if not holding ctrl
                                    using (_ = ImRaii.Disabled(!ctrl))
                                    {
                                        if (ImGui.Selectable("Delete"))
                                        {
                                            int index = Plugin.LoadedRoutes.FindIndex(r => r.Id == route.Id);
                                            bool exists = Plugin.Storage.RouteCache.ContainsKey(route.Id.ToString());

                                            if (index != -1)
                                            {
                                                Plugin.LoadedRoutes.RemoveAt(index);
                                            }

                                            if (exists)
                                            {
                                                Plugin.Storage.GetRoutes().Delete(route.Id);
                                                Plugin.Storage.UpdateRouteCache();

                                                if (Plugin.SelectedRoute == route.Id)
                                                {
                                                    Plugin.SelectedRoute = Plugin.LoadedRoutes.Count == 0 ? null : Plugin.LoadedRoutes[0].Id;
                                                }
                                            }
                                        }
                                    }

                                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                                    {
                                        ImGui.SetTooltip("Hold ctrl to enable the delete button.");
                                    }
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Right-click to open popup");
                            }

                            ImGui.TableNextColumn();

                            try
                            {
                                if (route.Records == null)
                                {
                                    route.Records = new List<Record>();
                                }

                                List<Record> records = route.Records.GroupBy(x => x.Name).Select(g => g.OrderByDescending(x => x.Time).Last()).ToList();

                                if (records.ElementAtOrDefault(0) != null)
                                {
                                    ImGui.TextColored(new Vector4(1, 0.85f, 0, 1), Time.PrettyFormatTimeSpan(records[0].Time));
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.DalamudGrey, records[0].Name);
                                }

                                if (records.ElementAtOrDefault(1) != null)
                                {
                                    ImGui.TextColored(new Vector4(0.82f, 0.82f, 0.82f, 1), Time.PrettyFormatTimeSpan(records[1].Time));
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.DalamudGrey, records[1].Name);
                                }

                                if (records.ElementAtOrDefault(2) != null)
                                {
                                    ImGui.TextColored(new Vector4(0.84f, 0.49f, 0.078f, 1), Time.PrettyFormatTimeSpan(records[2].Time));
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.DalamudGrey, records[2].Name);
                                }

                                if (route.ClientFails > 0)
                                {
                                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Your Fails:");
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.DalamudRed, route.ClientFails.ToString());
                                }

                                if (route.ClientFinishes > 0)
                                {
                                    if (route.ClientFails > 0) ImGui.SameLine();

                                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Your Finishes:");
                                    ImGui.SameLine();
                                    ImGui.TextColored(ImGuiColors.HealerGreen, route.ClientFinishes.ToString());
                                }
                            }
                            catch (Exception ex)
                            {
                                Plugin.Log.Error(ex.ToString());
                            }

                            
                        }
                    }
                }
            }
        }
    }
}
