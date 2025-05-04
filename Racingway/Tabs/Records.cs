using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using LiteDB;
using Racingway.Race;
using Racingway.Utils;
using static System.Net.Mime.MediaTypeNames;

namespace Racingway.Tabs
{
    internal class Records : ITab
    {
        public string Name => "Records";

        private Plugin Plugin { get; }

        // Filter state variables
        private bool _showFilters = false;
        private string _nameFilter = "";
        private DateTime? _minDateFilter = null;
        private DateTime? _maxDateFilter = null;
        private TimeSpan? _minTimeFilter = null;
        private TimeSpan? _maxTimeFilter = null;
        private float? _minDistanceFilter = null;
        private float? _maxDistanceFilter = null;

        // Input buffers for time filters
        private string _minTimeInput = "";
        private string _maxTimeInput = "";
        private string _minDistanceInput = "";
        private string _maxDistanceInput = "";

        // Date filter components
        private int _minDay = 1;
        private int _minMonth = 1;
        private int _minYear = DateTime.Now.Year;
        private int _maxDay = DateTime.Now.Day;
        private int _maxMonth = DateTime.Now.Month;
        private int _maxYear = DateTime.Now.Year;

        public Records(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void Dispose() { }

        private List<Record> cachedRecords = new List<Record>();
        private List<Record> filteredRecords = new List<Record>();

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

            if (Plugin.SelectedRoute == null)
            {
                return;
            }

            cachedRecords = Plugin.Storage.RouteCache[Plugin.SelectedRoute.ToString()].Records;
            if (cachedRecords == null)
                return;

            if (!Plugin.Configuration.AllowDuplicateRecords)
            {
                // Remove duplicates by sort magic
                cachedRecords = cachedRecords
                    .GroupBy(x => x.Name)
                    .Select(g => g.OrderByDescending(x => x.Time).Last())
                    .ToList();
            }

            // Apply filters and update filtered records
            filteredRecords = ApplyFilters(cachedRecords);

            if (ImGui.Button("Copy CSV to clipboard"))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Date,Name,World,Time,Distance\n");

                foreach (Record record in filteredRecords)
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

            ImGui.SameLine();
            if (ImGui.Button(_showFilters ? "Hide Filters" : "Show Filters"))
            {
                _showFilters = !_showFilters;
            }

            var ctrl = ImGui.GetIO().KeyCtrl;
            using (ImRaii.Disabled(!ctrl))
            {
                ImGui.SameLine();

                if (ImGui.Button("Purge Duplicates"))
                {
                    Plugin.DataQueue.QueueDataOperation(async () =>
                    {
                        // Get the records
                        Route route = Plugin.Storage.RouteCache[Plugin.SelectedRoute.ToString()];
                        List<Record> records = route.Records;

                        // Sort records by time
                        records.Sort((record1, record2) => record1.Time.CompareTo(record2.Time));

                        // Create an array containing only distinct records
                        List<Record> distinct = records
                            .GroupBy(r => r.Name)
                            .Select(g => g.First())
                            .ToList();

                        // Update the route
                        route.Records = distinct;
                        Plugin.Storage.RouteCache[route.Id.ToString()] = route;
                        await Plugin.Storage.AddRoute(route);
                    });
                }

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Hold Ctrl to enable.");
                }

                ImGui.SameLine();

                if (ImGui.Button("Clear Records"))
                {
                    Plugin.DataQueue.QueueDataOperation(async () =>
                    {
                        Route route = Plugin.Storage.RouteCache[Plugin.SelectedRoute.ToString()];

                        route.Records.Clear();

                        // Update the route
                        Plugin.Storage.RouteCache[route.Id.ToString()] = route;
                        await Plugin.Storage.AddRoute(route);
                    });
                }

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Hold Ctrl to enable.");
                }
            }

            // Display filters if enabled
            if (_showFilters)
            {
                DrawFilters();
            }

            using (var table = ImRaii.Table("###race-records", 5, ImGuiTableFlags.Sortable))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn(
                        "Time",
                        ImGuiTableColumnFlags.WidthFixed
                            | ImGuiTableColumnFlags.DefaultSort
                            | ImGuiTableColumnFlags.PreferSortAscending,
                        100f
                    );
                    ImGui.TableSetupColumn("Distance", ImGuiTableColumnFlags.WidthFixed, 100f);

                    ImGui.TableHeadersRow();

                    ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();

                    // Sort records by ImGui sortspecs.
                    filteredRecords.Sort(
                        (record1, record2) =>
                        {
                            short index = sortSpecs.Specs.ColumnIndex; // Sorting column
                            int comparison = 0;

                            switch (index)
                            {
                                case 0: // Date
                                    comparison = record1.Date.CompareTo(record2.Date);
                                    if (comparison == 0)
                                        comparison = record1.Time.CompareTo(record2.Time);
                                    break;
                                case 1: // Name
                                    comparison = string.Compare(record1.Name, record2.Name);
                                    if (comparison == 0)
                                        comparison = record1.Time.CompareTo(record2.Time);
                                    break;
                                case 2: // World
                                    comparison = string.Compare(record1.World, record2.World);
                                    if (comparison == 0)
                                        comparison = record1.Time.CompareTo(record2.Time);
                                    break;
                                case 3: // Time
                                    comparison = record1.Time.CompareTo(record2.Time);
                                    break;
                                case 4: // Distance
                                    comparison = record1.Distance.CompareTo(record2.Distance);
                                    if (comparison == 0)
                                        comparison = record1.Time.CompareTo(record2.Time);
                                    break;
                            }

                            if (comparison != 0)
                            {
                                // Check sort direction and return inverse if descending
                                return
                                    sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending
                                    ? -comparison
                                    : comparison;
                            }

                            return comparison;
                        }
                    );

                    for (int i = 0; i < filteredRecords.Count; i++)
                    {
                        Record record = filteredRecords[i];

                        ImGui.PushID(i);

                        // Change color of text if this is the selected record
                        if (Plugin.DisplayedRecord == record)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xFFF58742);
                        }
                        else
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFFFFF);
                        }

                        bool selected = false;

                        ImGui.TableNextColumn();
                        ImGui.Selectable(
                            record.Date.ToLocalTime().ToString("M/dd/yyyy H:mm:ss"),
                            ref selected,
                            ImGuiSelectableFlags.SpanAllColumns
                        );

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
                                    Plugin.Log.Debug(
                                        $"{record.Name}: {Time.PrettyFormatTimeSpan(record.Time)}, {record.Distance}"
                                    );

                                    // update route hash.. because old records probably have a broken hash.. oops!
                                    record.RouteHash = Plugin
                                        .Storage.RouteCache[Plugin.SelectedRoute.ToString()]
                                        .GetHash();

                                    var doc = BsonMapper.Global.ToDocument(record);
                                    string json = JsonSerializer.Serialize(doc);
                                    string text = Compression.ToCompressedBase64(json);

                                    if (text != string.Empty)
                                    {
                                        ImGui.SetClipboardText(text);
                                    }
                                    else
                                    {
                                        Plugin.ChatGui.PrintError(
                                            "[RACE] Error exporting record to clipboard."
                                        );
                                    }
                                }

                                if (ImGui.Selectable("Delete"))
                                {
                                    Plugin.DataQueue.QueueDataOperation(async () =>
                                    {
                                        Route route = Plugin.Storage.RouteCache[
                                            Plugin.SelectedRoute.ToString()
                                        ];
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

        private void DrawFilters()
        {
            ImGui.Separator();

            using (var table = ImRaii.Table("###filter-options", 2, ImGuiTableFlags.BordersInnerV))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("Filter", ImGuiTableColumnFlags.WidthFixed, 150f);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

                    // Name filter
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Player Name");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText("##nameFilter", ref _nameFilter, 100);

                    // Date Range
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Date From");
                    ImGui.TableNextColumn();

                    // From date filter
                    bool hasMinDate = _minDateFilter.HasValue;
                    ImGui.SetNextItemWidth(ImGui.GetFontSize() * 2);
                    if (ImGui.Checkbox("##hasMinDate", ref hasMinDate))
                    {
                        if (hasMinDate && !_minDateFilter.HasValue)
                        {
                            _minDateFilter = DateTime.Today;
                            _minDay = _minDateFilter.Value.Day;
                            _minMonth = _minDateFilter.Value.Month;
                            _minYear = _minDateFilter.Value.Year;
                        }
                        else if (!hasMinDate)
                        {
                            _minDateFilter = null;
                        }
                    }

                    if (hasMinDate)
                    {
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 3);
                        if (ImGui.InputInt("D##minDay", ref _minDay, 0))
                        {
                            _minDay = Math.Clamp(
                                _minDay,
                                1,
                                DateTime.DaysInMonth(_minYear, _minMonth)
                            );
                            _minDateFilter = new DateTime(_minYear, _minMonth, _minDay);
                        }

                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 3);
                        if (ImGui.InputInt("M##minMonth", ref _minMonth, 0))
                        {
                            _minMonth = Math.Clamp(_minMonth, 1, 12);
                            _minDay = Math.Min(_minDay, DateTime.DaysInMonth(_minYear, _minMonth));
                            _minDateFilter = new DateTime(_minYear, _minMonth, _minDay);
                        }

                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 5);
                        if (ImGui.InputInt("Y##minYear", ref _minYear, 0))
                        {
                            _minYear = Math.Max(_minYear, 1);
                            _minDay = Math.Min(_minDay, DateTime.DaysInMonth(_minYear, _minMonth));
                            _minDateFilter = new DateTime(_minYear, _minMonth, _minDay);
                        }
                    }

                    // To date filter
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Date To");
                    ImGui.TableNextColumn();

                    bool hasMaxDate = _maxDateFilter.HasValue;
                    ImGui.SetNextItemWidth(ImGui.GetFontSize() * 2);
                    if (ImGui.Checkbox("##hasMaxDate", ref hasMaxDate))
                    {
                        if (hasMaxDate && !_maxDateFilter.HasValue)
                        {
                            _maxDateFilter = DateTime.Today;
                            _maxDay = _maxDateFilter.Value.Day;
                            _maxMonth = _maxDateFilter.Value.Month;
                            _maxYear = _maxDateFilter.Value.Year;
                        }
                        else if (!hasMaxDate)
                        {
                            _maxDateFilter = null;
                        }
                    }

                    if (hasMaxDate)
                    {
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 3);
                        if (ImGui.InputInt("D##maxDay", ref _maxDay, 0))
                        {
                            _maxDay = Math.Clamp(
                                _maxDay,
                                1,
                                DateTime.DaysInMonth(_maxYear, _maxMonth)
                            );
                            _maxDateFilter = new DateTime(_maxYear, _maxMonth, _maxDay);
                        }

                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 3);
                        if (ImGui.InputInt("M##maxMonth", ref _maxMonth, 0))
                        {
                            _maxMonth = Math.Clamp(_maxMonth, 1, 12);
                            _maxDay = Math.Min(_maxDay, DateTime.DaysInMonth(_maxYear, _maxMonth));
                            _maxDateFilter = new DateTime(_maxYear, _maxMonth, _maxDay);
                        }

                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 5);
                        if (ImGui.InputInt("Y##maxYear", ref _maxYear, 0))
                        {
                            _maxYear = Math.Max(_maxYear, 1);
                            _maxDay = Math.Min(_maxDay, DateTime.DaysInMonth(_maxYear, _maxMonth));
                            _maxDateFilter = new DateTime(_maxYear, _maxMonth, _maxDay);
                        }
                    }

                    // Min time filter
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Time Minimum (s)");
                    ImGui.TableNextColumn();

                    bool hasMinTime = _minTimeFilter.HasValue;
                    ImGui.SetNextItemWidth(ImGui.GetFontSize() * 2);
                    if (ImGui.Checkbox("##hasMinTime", ref hasMinTime))
                    {
                        if (hasMinTime && !_minTimeFilter.HasValue)
                        {
                            _minTimeFilter = TimeSpan.Zero;
                            _minTimeInput = "0";
                        }
                        else if (!hasMinTime)
                        {
                            _minTimeFilter = null;
                        }
                    }

                    if (hasMinTime)
                    {
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 7);
                        if (ImGui.InputText("##minTime", ref _minTimeInput, 10))
                        {
                            if (float.TryParse(_minTimeInput, out float seconds) && seconds >= 0)
                            {
                                _minTimeFilter = TimeSpan.FromSeconds(seconds);
                            }
                        }
                    }

                    // Max time filter
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Time Maximum (s)");
                    ImGui.TableNextColumn();

                    bool hasMaxTime = _maxTimeFilter.HasValue;
                    ImGui.SetNextItemWidth(ImGui.GetFontSize() * 2);
                    if (ImGui.Checkbox("##hasMaxTime", ref hasMaxTime))
                    {
                        if (hasMaxTime && !_maxTimeFilter.HasValue)
                        {
                            _maxTimeFilter = TimeSpan.FromSeconds(300); // Default to 5 minutes
                            _maxTimeInput = "300";
                        }
                        else if (!hasMaxTime)
                        {
                            _maxTimeFilter = null;
                        }
                    }

                    if (hasMaxTime)
                    {
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 7);
                        if (ImGui.InputText("##maxTime", ref _maxTimeInput, 10))
                        {
                            if (float.TryParse(_maxTimeInput, out float seconds) && seconds >= 0)
                            {
                                _maxTimeFilter = TimeSpan.FromSeconds(seconds);
                            }
                        }
                    }

                    // Min distance filter
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Distance Minimum");
                    ImGui.TableNextColumn();

                    bool hasMinDistance = _minDistanceFilter.HasValue;
                    ImGui.SetNextItemWidth(ImGui.GetFontSize() * 2);
                    if (ImGui.Checkbox("##hasMinDistance", ref hasMinDistance))
                    {
                        if (hasMinDistance && !_minDistanceFilter.HasValue)
                        {
                            _minDistanceFilter = 0;
                            _minDistanceInput = "0";
                        }
                        else if (!hasMinDistance)
                        {
                            _minDistanceFilter = null;
                        }
                    }

                    if (hasMinDistance)
                    {
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 7);
                        if (ImGui.InputText("##minDistance", ref _minDistanceInput, 10))
                        {
                            if (
                                float.TryParse(_minDistanceInput, out float distance)
                                && distance >= 0
                            )
                            {
                                _minDistanceFilter = distance;
                            }
                        }
                    }

                    // Max distance filter
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Distance Maximum");
                    ImGui.TableNextColumn();

                    bool hasMaxDistance = _maxDistanceFilter.HasValue;
                    ImGui.SetNextItemWidth(ImGui.GetFontSize() * 2);
                    if (ImGui.Checkbox("##hasMaxDistance", ref hasMaxDistance))
                    {
                        if (hasMaxDistance && !_maxDistanceFilter.HasValue)
                        {
                            _maxDistanceFilter = 1000;
                            _maxDistanceInput = "1000";
                        }
                        else if (!hasMaxDistance)
                        {
                            _maxDistanceFilter = null;
                        }
                    }

                    if (hasMaxDistance)
                    {
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 7);
                        if (ImGui.InputText("##maxDistance", ref _maxDistanceInput, 10))
                        {
                            if (
                                float.TryParse(_maxDistanceInput, out float distance)
                                && distance >= 0
                            )
                            {
                                _maxDistanceFilter = distance;
                            }
                        }
                    }
                }
            }

            // Reset filters button
            if (ImGui.Button("Reset Filters"))
            {
                _nameFilter = "";
                _minDateFilter = null;
                _maxDateFilter = null;
                _minTimeFilter = null;
                _maxTimeFilter = null;
                _minTimeInput = "";
                _maxTimeInput = "";
                _minDistanceFilter = null;
                _maxDistanceFilter = null;
                _minDistanceInput = "";
                _maxDistanceInput = "";
            }

            ImGui.Separator();
        }

        private List<Record> ApplyFilters(List<Record> records)
        {
            var result = new List<Record>(records);

            // Filter by name
            if (!string.IsNullOrWhiteSpace(_nameFilter))
            {
                result = result
                    .Where(r => r.Name.Contains(_nameFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Filter by date range
            if (_minDateFilter.HasValue)
            {
                var minDate = _minDateFilter.Value.Date;
                result = result.Where(r => r.Date.Date >= minDate).ToList();
            }

            if (_maxDateFilter.HasValue)
            {
                var maxDate = _maxDateFilter.Value.Date.AddDays(1).AddSeconds(-1); // End of the day
                result = result.Where(r => r.Date.Date <= maxDate).ToList();
            }

            // Filter by time
            if (_minTimeFilter.HasValue)
            {
                result = result.Where(r => r.Time >= _minTimeFilter.Value).ToList();
            }

            if (_maxTimeFilter.HasValue)
            {
                result = result.Where(r => r.Time <= _maxTimeFilter.Value).ToList();
            }

            // Filter by distance
            if (_minDistanceFilter.HasValue)
            {
                result = result.Where(r => r.Distance >= _minDistanceFilter.Value).ToList();
            }

            if (_maxDistanceFilter.HasValue)
            {
                result = result.Where(r => r.Distance <= _maxDistanceFilter.Value).ToList();
            }

            return result;
        }
    }
}
