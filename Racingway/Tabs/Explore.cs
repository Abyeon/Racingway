using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility.Numerics;
using ImGuiNET;
using LiteDB;
using Racingway.Race;
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
            using (var child = ImRaii.Child("###race-exploreL", new Vector2(ImGui.GetContentRegionAvail().X * 0.33f, ImGui.GetContentRegionAvail().Y), true))
            {
                if (child.Success)
                {
                    if (ImGui.Selectable("All Routes", selectedSearch == Search.None))
                    {
                        selectedSearch = Search.None;
                    }

                    if (ImGui.Selectable("Loaded Routes", selectedSearch == Search.Loaded))
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

                    using (var table = ImRaii.Table("###race-exploreTable", 2, ImGuiTableFlags.BordersInnerH))
                    {
                        ImGui.TableSetupColumn("Name and Address", ImGuiTableColumnFlags.None, 100f);
                        ImGui.TableSetupColumn("Best Times", ImGuiTableColumnFlags.None, 75f);

                        for (int i = 0; i < routes.Count; i++)
                        {
                            Route route = routes[i];
                            if (!filter.PassFilter(route.Name) && !filter.PassFilter(route.Address.ReadableName)) continue;

                            ImGui.PushID(i);

                            ImGui.TableNextColumn();
                            ImGui.Text(route.Name);
                            ImGui.TextColored(ImGuiColors.DalamudGrey, route.Address.ReadableName);

                            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
                            ImGui.TextWrapped(route.Description);
                            ImGui.PopStyleColor();

                            ImGui.PopID();

                            using (var popup = ImRaii.ContextPopup(i.ToString()))
                            {
                                if (popup.Success)
                                {
                                    if (ImGui.Button("Copy Address"))
                                    {
                                        ImGui.SetClipboardText(route.Address.ReadableName);
                                    }
                                    if (ImGui.Button("Export to Clipboard"))
                                    {
                                        string input = JsonSerializer.Serialize(route.GetSerialized());
                                        string text = Compression.ToCompressedBase64(input);

                                        if (text != string.Empty)
                                        {
                                            ImGui.SetClipboardText(text);
                                        }
                                        else
                                        {
                                            Plugin.ChatGui.PrintError("[RACE] Error exporting route to clipboard.");
                                        }
                                    }

                                    if (Plugin.LoadedRoutes.Contains(route) && route.BestRecord != null)
                                    {
                                        if (ImGui.Button("Display Best Time"))
                                        {
                                            Plugin.DisplayedRecord = route.BestRecord.Id;
                                        }
                                    }

                                    if (ImGui.Button("Delete"))
                                    {
                                        Plugin.LoadedRoutes.Remove(route);

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
                                ImGui.SetTooltip("Right-click to open popup");
                            }

                            ImGui.TableNextColumn();
                            ImGui.Text("Best Time:");
                            if (route.BestRecord != null)
                            {
                                ImGui.TextColored(ImGuiColors.DalamudOrange, Time.PrettyFormatTimeSpan(route.BestRecord.Time));
                                ImGui.TextColored(ImGuiColors.DalamudGrey, $"by {route.BestRecord.Name} {route.BestRecord.World}");
                            } else
                            {
                                ImGui.TextColored(ImGuiColors.DalamudGrey, "No time recorded");
                            }
                        }
                    }
                }
            }
        }
    }
}
