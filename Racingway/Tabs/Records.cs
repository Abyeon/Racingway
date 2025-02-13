using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public void Draw()
        {
            ImGui.Text("THESE ARE TEMPORARY RECORDS.\nIF YOU QUIT THE GAME OR RELOAD THE PLUGIN, THESE WILL DISAPPEAR!");
            using (var table = ImRaii.Table("###race-records", 4, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip | ImGuiTableFlags.NoSavedSettings))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 100f);

                    foreach (Record record in Plugin.RecordList)
                    {
                        ImGui.TableNextRow();
                        ImGui.Text(record.Date.ToString("M/dd H:mm:ss"));
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
