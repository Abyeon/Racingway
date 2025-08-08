using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using LiteDB;
using Racingway.Race;
using Racingway.Utils;
using ZLinq;

namespace Racingway.Tabs
{
    internal class Records : ITab
    {
        public string Name => "Records";

        private Plugin Plugin { get; }

        // Filter settings
        private bool showFilters = false;
        private DateTime? filterStartDate = null;
        private DateTime? filterEndDate = null;
        private string filterPlayerName = string.Empty;
        private float? filterTimeMin = null;
        private float? filterTimeMax = null;
        private float? filterDistanceMin = null;
        private float? filterDistanceMax = null;

        public Records(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void Dispose() { }

        private List<Record> cachedRecords = new List<Record>();
        private List<Record> filteredRecords = new List<Record>();

        private bool IsRecordMatchingFilters(Record record)
        {
            // Date filter
            if (filterStartDate.HasValue && record.Date < filterStartDate.Value)
                return false;

            if (filterEndDate.HasValue && record.Date > filterEndDate.Value)
                return false;

            // Player name filter (case insensitive)
            if (
                !string.IsNullOrEmpty(filterPlayerName)
                && !record.Name.ToLower().Contains(filterPlayerName.ToLower())
            )
                return false;

            // Time filter (in seconds)
            if (filterTimeMin.HasValue && record.Time.TotalSeconds < filterTimeMin.Value)
                return false;

            if (filterTimeMax.HasValue && record.Time.TotalSeconds > filterTimeMax.Value)
                return false;

            // Distance filter
            if (filterDistanceMin.HasValue && record.Distance < filterDistanceMin.Value)
                return false;

            if (filterDistanceMax.HasValue && record.Distance > filterDistanceMax.Value)
                return false;

            return true;
        }

        private void ApplyFilters()
        {
            filteredRecords = cachedRecords.AsValueEnumerable().Where(IsRecordMatchingFilters).ToList();
        }

        private void DrawFilterPopup()
        {
            if (!showFilters)
                return;

            ImGui.SetNextWindowSize(new Vector2(350, 0));
            if (ImGui.BeginPopup("FiltersPopup"))
            {
                ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "Filter Records");
                ImGui.Separator();

                // Date range filter
                if (ImGui.CollapsingHeader("Date Range", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    // Start date
                    bool hasStartDate = filterStartDate.HasValue;
                    if (ImGui.Checkbox("Start Date", ref hasStartDate))
                    {
                        filterStartDate = hasStartDate ? DateTime.Today.AddDays(-7) : null;
                    }

                    if (hasStartDate && filterStartDate.HasValue)
                    {
                        ImGui.SameLine();
                        int startDay = filterStartDate.Value.Day;
                        int startMonth = filterStartDate.Value.Month;
                        int startYear = filterStartDate.Value.Year;

                        ImGui.SetNextItemWidth(40);
                        if (ImGui.DragInt("Day##StartDay", ref startDay, 0.1f, 1, 31))
                        {
                            startDay = Math.Clamp(
                                startDay,
                                1,
                                DateTime.DaysInMonth(startYear, startMonth)
                            );
                            filterStartDate = new DateTime(startYear, startMonth, startDay);
                        }

                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(50);
                        if (ImGui.DragInt("Month##StartMonth", ref startMonth, 0.1f, 1, 12))
                        {
                            startMonth = Math.Clamp(startMonth, 1, 12);
                            startDay = Math.Min(
                                startDay,
                                DateTime.DaysInMonth(startYear, startMonth)
                            );
                            filterStartDate = new DateTime(startYear, startMonth, startDay);
                        }

                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(60);
                        if (ImGui.DragInt("Year##StartYear", ref startYear, 0.1f, 2000, 2100))
                        {
                            startYear = Math.Max(startYear, 2000);
                            startDay = Math.Min(
                                startDay,
                                DateTime.DaysInMonth(startYear, startMonth)
                            );
                            filterStartDate = new DateTime(startYear, startMonth, startDay);
                        }
                    }

                    // End date
                    bool hasEndDate = filterEndDate.HasValue;
                    if (ImGui.Checkbox("End Date", ref hasEndDate))
                    {
                        filterEndDate = hasEndDate ? DateTime.Today : null;
                    }

                    if (hasEndDate && filterEndDate.HasValue)
                    {
                        ImGui.SameLine();
                        int endDay = filterEndDate.Value.Day;
                        int endMonth = filterEndDate.Value.Month;
                        int endYear = filterEndDate.Value.Year;

                        ImGui.SetNextItemWidth(40);
                        if (ImGui.DragInt("Day##EndDay", ref endDay, 0.1f, 1, 31))
                        {
                            endDay = Math.Clamp(endDay, 1, DateTime.DaysInMonth(endYear, endMonth));
                            filterEndDate = new DateTime(endYear, endMonth, endDay);
                        }

                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(50);
                        if (ImGui.DragInt("Month##EndMonth", ref endMonth, 0.1f, 1, 12))
                        {
                            endMonth = Math.Clamp(endMonth, 1, 12);
                            endDay = Math.Min(endDay, DateTime.DaysInMonth(endYear, endMonth));
                            filterEndDate = new DateTime(endYear, endMonth, endDay);
                        }

                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(60);
                        if (ImGui.DragInt("Year##EndYear", ref endYear, 0.1f, 2000, 2100))
                        {
                            endYear = Math.Max(endYear, 2000);
                            endDay = Math.Min(endDay, DateTime.DaysInMonth(endYear, endMonth));
                            filterEndDate = new DateTime(endYear, endMonth, endDay);
                        }
                    }
                }

                // Player name filter
                if (ImGui.CollapsingHeader("Player Name", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SetNextItemWidth(200);
                    ImGui.InputText("Contains", ref filterPlayerName, 100);
                }

                // Time filter
                if (ImGui.CollapsingHeader("Time (seconds)", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    // Min Time
                    bool hasMinTime = filterTimeMin.HasValue;
                    if (ImGui.Checkbox("Minimum Time", ref hasMinTime))
                    {
                        filterTimeMin = hasMinTime ? 0 : null;
                    }

                    if (hasMinTime && filterTimeMin.HasValue)
                    {
                        ImGui.SameLine();
                        float minTime = filterTimeMin.Value;
                        ImGui.SetNextItemWidth(120);
                        if (ImGui.InputFloat("##MinTime", ref minTime, 1.0f, 10.0f, "%.1f s"))
                        {
                            filterTimeMin = Math.Max(0, minTime);
                        }
                    }

                    // Max Time
                    bool hasMaxTime = filterTimeMax.HasValue;
                    if (ImGui.Checkbox("Maximum Time", ref hasMaxTime))
                    {
                        filterTimeMax = hasMaxTime ? 300 : null; // Default 5 minutes
                    }

                    if (hasMaxTime && filterTimeMax.HasValue)
                    {
                        ImGui.SameLine();
                        float maxTime = filterTimeMax.Value;
                        ImGui.SetNextItemWidth(120);
                        if (ImGui.InputFloat("##MaxTime", ref maxTime, 1.0f, 10.0f, "%.1f s"))
                        {
                            filterTimeMax = Math.Max(0, maxTime);
                        }
                    }
                }

                // Distance filter
                if (ImGui.CollapsingHeader("Distance", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    // Min Distance
                    bool hasMinDistance = filterDistanceMin.HasValue;
                    if (ImGui.Checkbox("Minimum Distance", ref hasMinDistance))
                    {
                        filterDistanceMin = hasMinDistance ? 0 : null;
                    }

                    if (hasMinDistance && filterDistanceMin.HasValue)
                    {
                        ImGui.SameLine();
                        float minDist = filterDistanceMin.Value;
                        ImGui.SetNextItemWidth(120);
                        if (ImGui.InputFloat("##MinDistance", ref minDist, 1.0f, 10.0f, "%.1f"))
                        {
                            filterDistanceMin = Math.Max(0, minDist);
                        }
                    }

                    // Max Distance
                    bool hasMaxDistance = filterDistanceMax.HasValue;
                    if (ImGui.Checkbox("Maximum Distance", ref hasMaxDistance))
                    {
                        filterDistanceMax = hasMaxDistance ? 1000 : null;
                    }

                    if (hasMaxDistance && filterDistanceMax.HasValue)
                    {
                        ImGui.SameLine();
                        float maxDist = filterDistanceMax.Value;
                        ImGui.SetNextItemWidth(120);
                        if (ImGui.InputFloat("##MaxDistance", ref maxDist, 1.0f, 10.0f, "%.1f"))
                        {
                            filterDistanceMax = Math.Max(0, maxDist);
                        }
                    }
                }

                ImGui.Separator();

                // Action buttons
                if (ImGui.Button("Apply Filters", new Vector2(150, 0)))
                {
                    ApplyFilters();
                }

                ImGui.SameLine();

                if (ImGui.Button("Clear Filters", new Vector2(150, 0)))
                {
                    // Reset all filters
                    filterStartDate = null;
                    filterEndDate = null;
                    filterPlayerName = string.Empty;
                    filterTimeMin = null;
                    filterTimeMax = null;
                    filterDistanceMin = null;
                    filterDistanceMax = null;

                    // Reset filtered records to show all records
                    filteredRecords = cachedRecords;
                }

                ImGui.EndPopup();
            }
            else
            {
                showFilters = false;
            }
        }

        public void Draw()
        {
            if (Plugin.Storage == null) return;
            if (Plugin.LoadedRoutes.Count == 0)
            {
                ImGui.TextUnformatted("No routes loaded for this area.");
                return;
            }

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

            // Initialize filteredRecords if it's empty
            if (
                filteredRecords.Count == 0
                || filteredRecords.Count != cachedRecords.Count
                    && !filterStartDate.HasValue
                    && !filterEndDate.HasValue
                    && string.IsNullOrEmpty(filterPlayerName)
                    && !filterTimeMin.HasValue
                    && !filterTimeMax.HasValue
                    && !filterDistanceMin.HasValue
                    && !filterDistanceMax.HasValue
            )
            {
                filteredRecords = cachedRecords;
            }

            if (!Plugin.Configuration.AllowDuplicateRecords)
            {
                // Remove duplicates by sort magic
                cachedRecords = cachedRecords
                    .GroupBy(x => x.Name)
                    .Select(g => g.OrderByDescending(x => x.Time).Last())
                    .ToList();

                // Re-apply filters after removing duplicates
                ApplyFilters();
            }

            if (ImGui.Button("Copy CSV to clipboard"))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Date,Name,World,Time,Distance\n");

                // Use filteredRecords instead of cachedRecords
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
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Filter))
            {
                showFilters = true;
                ImGui.OpenPopup("FiltersPopup");
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Filter Records");
            }

            // Draw filter popup
            DrawFilterPopup();

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

            // Show active filter indicator
            bool hasActiveFilters =
                filterStartDate.HasValue
                || filterEndDate.HasValue
                || !string.IsNullOrEmpty(filterPlayerName)
                || filterTimeMin.HasValue
                || filterTimeMax.HasValue
                || filterDistanceMin.HasValue
                || filterDistanceMax.HasValue;

            if (hasActiveFilters)
            {
                ImGui.SameLine();
                ImGui.TextColored(
                    new Vector4(1, 0.8f, 0, 1),
                    $"({filteredRecords.Count} / {cachedRecords.Count})"
                );

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Active Filters:");
                    if (filterStartDate.HasValue)
                        ImGui.Text($"- After: {filterStartDate.Value.ToShortDateString()}");
                    if (filterEndDate.HasValue)
                        ImGui.Text($"- Before: {filterEndDate.Value.ToShortDateString()}");
                    if (!string.IsNullOrEmpty(filterPlayerName))
                        ImGui.Text($"- Player: {filterPlayerName}");
                    if (filterTimeMin.HasValue)
                        ImGui.Text($"- Min Time: {filterTimeMin.Value} s");
                    if (filterTimeMax.HasValue)
                        ImGui.Text($"- Max Time: {filterTimeMax.Value} s");
                    if (filterDistanceMin.HasValue)
                        ImGui.Text($"- Min Distance: {filterDistanceMin.Value}");
                    if (filterDistanceMax.HasValue)
                        ImGui.Text($"- Max Distance: {filterDistanceMax.Value}");
                    ImGui.EndTooltip();
                }
            }

            using (var table = ImRaii.Table("###race-records", 5, ImGuiTableFlags.Sortable | ImGuiTableFlags.Resizable))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthStretch, 100f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 100f);
                    ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthStretch, 100f);
                    ImGui.TableSetupColumn(
                        "Time",
                        ImGuiTableColumnFlags.WidthStretch
                            | ImGuiTableColumnFlags.DefaultSort
                            | ImGuiTableColumnFlags.PreferSortAscending,
                        100f
                    );
                    ImGui.TableSetupColumn("Distance", ImGuiTableColumnFlags.WidthStretch, 100f);

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
                                    //ShareHelper.ExportRecordToClipboard(record, Plugin);
                                    ShareHelper.PackRecordToClipboard(record, Plugin);
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
    }
}
