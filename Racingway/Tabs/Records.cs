using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using LiteDB;
using Racingway.Race;
using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Racingway.Tabs
{
    internal class Records : ITab
    {
        public string Name => "Records";

        private Plugin Plugin { get; }

        public Records(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void Dispose()
        {

        }

        private List<Record> cachedRecords = new List<Record>();

        public void Draw()
        {
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

            if (Plugin.SelectedRoute == null)
            {
                return;
            }

            cachedRecords = Plugin.Storage.RouteCache[Plugin.SelectedRoute.ToString()].Records;
            if (cachedRecords == null) return;

            if (!Plugin.Configuration.AllowDuplicateRecords)
            {
                // Remove duplicates by sort magic
                cachedRecords = cachedRecords.GroupBy(x => x.Name)
                    .Select(g => g.OrderByDescending(x => x.Time).Last())
                    .ToList();
            }

            if (ImGui.Button("Copy CSV to clipboard"))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Date,Name,World,Time,Distance\n");

                foreach (Record record in cachedRecords)
                {
                    sb.Append(record.GetCSV());
                }

                ImGui.SetClipboardText(sb.ToString());
            }

            ImGui.SameLine();
            if (ImGui.Checkbox("Allow Duplicates", ref Plugin.Configuration.AllowDuplicateRecords))
            {
                Plugin.Configuration.Save();
            }

            // Reimplement this later to be route-specific.
            //ImGui.SameLine();
            //if (ImGui.Button("Clear Records"))
            //{
            //    //foreach (Record record in cachedRecords)
            //    //{
            //    //    Plugin.Storage.GetRecords().Delete(record.Id);
            //    //    Plugin.RecordList.Remove(record);
            //    //}

            //    //cachedRecords.Clear();

            //    Plugin.DisplayedRecord = null;
            //}

            using (var table = ImRaii.Table("###race-records", 5, ImGuiTableFlags.Sortable))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortAscending, 100f);
                    ImGui.TableSetupColumn("Distance", ImGuiTableColumnFlags.WidthFixed, 100f);

                    ImGui.TableHeadersRow();

                    ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();

                    // Sort records by ImGui sortspecs.
                    cachedRecords.Sort((record1, record2) =>
                    {
                        short index = sortSpecs.Specs.ColumnIndex; // Sorting column
                        int comparison = 0;

                        switch (index)
                        {
                            case 0: // Date
                                comparison = record1.Date.CompareTo(record2.Date);
                                if (comparison == 0) comparison = record1.Time.CompareTo(record2.Time);
                                break;
                            case 1: // Name
                                comparison = string.Compare(record1.Name, record2.Name);
                                if (comparison == 0) comparison = record1.Time.CompareTo(record2.Time);
                                break;
                            case 2: // World
                                comparison = string.Compare(record1.World, record2.World);
                                if (comparison == 0) comparison = record1.Time.CompareTo(record2.Time);
                                break;
                            case 3: // Time
                                comparison = record1.Time.CompareTo(record2.Time);
                                break;
                            case 4: // Distance
                                comparison = record1.Distance.CompareTo(record2.Distance);
                                if (comparison == 0) comparison = record1.Time.CompareTo(record2.Time);
                                break;
                        }

                        if (comparison != 0)
                        {
                            // Check sort direction and return inverse if descending
                            return sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending ? -comparison : comparison;
                        }

                        return comparison;
                    });

                    for (int i = 0; i < cachedRecords.Count; i++)
                    {
                        Record record = cachedRecords[i];

                        ImGui.PushID(i);

                        // Change color of text if this is the selected record
                        if (Plugin.DisplayedRecord == record)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xFFF58742);
                        } else
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFFFFF);
                        }

                        bool selected = false;

                        ImGui.TableNextColumn();
                        ImGui.Selectable(record.Date.ToLocalTime().ToString("M/dd/yyyy H:mm:ss"), ref selected, ImGuiSelectableFlags.SpanAllColumns);

                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip("Click to display race line.");
                        }

                        // Right-click menu
                        using (var popup = ImRaii.ContextPopupItem(i.ToString()))
                        {
                            if (popup.Success)
                            {
                                if (ImGui.Selectable("Export to Clipboard"))
                                {
                                    Plugin.Log.Debug($"{record.Name}: {Time.PrettyFormatTimeSpan(record.Time)}, {record.Distance}");

                                    // update route hash.. because old records probably have a broken hash.. oops!
                                    record.RouteHash = Plugin.Storage.RouteCache[Plugin.SelectedRoute.ToString()].GetHash();

                                    var doc = BsonMapper.Global.ToDocument(record);
                                    string json = JsonSerializer.Serialize(doc);
                                    string text = Compression.ToCompressedBase64(json);

                                    if (text != string.Empty)
                                    {
                                        ImGui.SetClipboardText(text);
                                    }
                                    else
                                    {
                                        Plugin.ChatGui.PrintError("[RACE] Error exporting record to clipboard.");
                                    }
                                }

                                if (ImGui.Selectable("Delete"))
                                {
                                    Plugin.DataQueue.QueueDataOperation(async () =>
                                    {
                                        Route route = Plugin.Storage.RouteCache[Plugin.SelectedRoute.ToString()];
                                        route.Records.Remove(record);

                                        Plugin.Storage.RouteCache[route.Id.ToString()] = route;
                                        await Plugin.Storage.AddRoute(route);
                                    });
                                }
                            }
                        }

                        // Draw record info
                        ImGui.TableNextColumn();
                        ImGui.Text(record.Name);
                        ImGui.TableNextColumn();
                        ImGui.Text(record.World);
                        ImGui.TableNextColumn();
                        ImGui.Text(Time.PrettyFormatTimeSpan(record.Time));
                        ImGui.TableNextColumn();
                        ImGui.Text(record.Distance.ToString());

                        // If the record was selected, display that record
                        if (selected)
                        {
                            if (Plugin.DisplayedRecord == record)
                            {
                                Plugin.DisplayedRecord = null;
                            }
                            else
                            {
                                Plugin.DisplayedRecord = record;
                            }

                            Plugin.ShowHideOverlay();
                        }

                        ImGui.PopID();
                        ImGui.PopStyleColor();
                    }
                }
            }
        }
    }
}
