using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Dalamud.Utility.Numerics;
using ImGuiNET;
using Racingway.Race;
using Racingway.Utils;
using static System.Net.Mime.MediaTypeNames;

namespace Racingway.Windows
{
    public class TimerWindow : Window, IDisposable
    {
        private Plugin Plugin { get; }

        public TimerWindow(Plugin Plugin)
            : base("Race Timer##race")
        {
            Flags =
                ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoDecoration
                | ImGuiWindowFlags.NoBackground;

            this.Plugin = Plugin;
            //this.Size = Vector2.Zero;
        }

        public List<long> splits = new List<long>();

        public void Dispose() { }

        public override void Draw()
        {
            //ImGui.SetWindowFontScale(Plugin.Configuration.TimerSize);
            //Vector2 startPos = ImGui.GetCursorPos();

            if (Plugin.Configuration.DrawTimerButtons)
            {
                // Main window button
                if (ImGuiComponents.IconButton("##openMain", FontAwesomeIcon.ExternalLinkAlt))
                {
                    Plugin.ToggleMainUI();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Open Main Window");
                }

                // Clear racing lines button
                ImGui.SameLine(0);

                if (ImGuiComponents.IconButton("##clearLines", FontAwesomeIcon.Trash))
                {
                    foreach (var actor in Plugin.trackedPlayers.Values)
                    {
                        actor.ClearLine();
                    }
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Clear Racing Lines");
                }
            }

            if (Plugin.FontManager.FontPushed && !Plugin.FontManager.FontReady)
            {
                ImGui.Text("Loading font..");
            }
            else
            {
                TimeSpan span = TimeSpan.FromMilliseconds(Plugin.LocalTimer.ElapsedMilliseconds);
                string timerText = Time.PrettyFormatTimeSpan(span);

                Plugin.FontManager.PushFont();

                ImDrawListPtr drawList = ImGui.GetWindowDrawList();

                uint color = Plugin.Configuration.TimerColor.ToByteColor().RGBA;

                drawList.ChannelsSplit(2);
                drawList.ChannelsSetCurrent(1);
                ImGui.TextUnformatted(timerText);

                drawList.ChannelsSetCurrent(0);
                drawList.AddRectFilled(ImGui.GetItemRectMin() - ImGui.GetStyle().FramePadding * 2, ImGui.GetItemRectMax() + ImGui.GetStyle().FramePadding * 2, color);

                Plugin.FontManager.PopFont();

                foreach (var split in splits)
                {
                    drawList.ChannelsSetCurrent(1);

                    StringBuilder sb = new StringBuilder();

                    Vector4 splitCol = ImGuiColors.DalamudRed;
                    if (split < 0)
                        splitCol = ImGuiColors.HealerGreen;

                    sb.Append(Time.PrettyFormatTimeSpanSigned(TimeSpan.FromMilliseconds(split)));

                    string pretty = sb.ToString();

                    ImGui.TextColored(splitCol, pretty);

                    drawList.ChannelsSetCurrent(0);
                    drawList.AddRectFilled(ImGui.GetItemRectMin() - ImGui.GetStyle().FramePadding * 2, ImGui.GetItemRectMax() + ImGui.GetStyle().FramePadding * 2, color);
                }

                drawList.ChannelsMerge();
            }
        }
    }
}
