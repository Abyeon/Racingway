using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
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

        private List<Record> GetRecords()
        {
            List<Record> records = Plugin.RecordList;

            if (!Plugin.Configuration.AllowDuplicateRecords)
            {
                // Remove duplicates by sort magic
                records = records.GroupBy(x => x.Name)
                    .Select(g => g.OrderByDescending(x => x.Time).Last())
                    .ToList();
            }

            return records;
        }

        public void Draw()
        {
            ImGui.Text("THESE ARE TEMPORARY RECORDS.\nIF YOU QUIT THE GAME OR RELOAD THE PLUGIN, THESE WILL DISAPPEAR!");

            List<Record> tempRecords = GetRecords();

            if (ImGui.Button("Copy CSV to clipboard"))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Date,Name,World,Time\n");

                foreach (Record record in tempRecords)
                {
                    sb.Append($"{record.Date.ToLocalTime().ToString("M/dd H:mm:ss")}, {record.Name}, {record.World}, {Time.PrettyFormatTimeSpan(record.Time)}\n");
                }

                ImGui.SetClipboardText(sb.ToString());
            }

            ImGui.SameLine();
            if (ImGui.Checkbox("Allow Duplicates", ref Plugin.Configuration.AllowDuplicateRecords))
            {
                Plugin.Configuration.Save();
            }

            using (var table = ImRaii.Table("###race-records", 4, ImGuiTableFlags.Sortable))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortAscending, 100f);

                    ImGui.TableHeadersRow();

                    ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();

                    // Sort records by ImGui sortspecs.
                    tempRecords.Sort((record1, record2) =>
                    {
                        short index = sortSpecs.Specs.ColumnIndex; // Sorting column
                        int comparison = 0;

                        switch (index)
                        {
                            case 0: // Date
                                comparison = record1.Date.CompareTo(record2.Date);
                                break;
                            case 1: // Name
                                comparison = string.Compare(record1.Name, record2.Name);
                                break;
                            case 2: // World
                                comparison = string.Compare(record1.World, record2.World);
                                break;
                            case 3: // Time
                                comparison = record1.Time.CompareTo(record2.Time);
                                break;
                        }

                        if (comparison != 0)
                        {
                            // Check sort direction and return inverse if descending
                            return sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending ? -comparison : comparison;
                        }

                        return comparison;
                    });

                    foreach (Record record in tempRecords)
                    {
                        ImGui.TableNextColumn();
                        ImGui.Text(record.Date.ToLocalTime().ToString("M/dd H:mm:ss"));
                        ImGui.TableNextColumn();
                        ImGui.Text(record.Name);
                        ImGui.TableNextColumn();
                        ImGui.Text(record.World);
                        ImGui.TableNextColumn();
                        ImGui.Text(Time.PrettyFormatTimeSpan(record.Time));
                    }
                }
            }
        }
    }
}
