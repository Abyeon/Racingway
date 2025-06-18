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
using Lumina.Extensions;
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
        }

        public List<long> splits = new List<long>();

        public void Dispose() { }

        public override void Draw()
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();

            uint color = Plugin.Configuration.TimerColor.ToByteColor().RGBA;

            // Fancy way of drawing behind text
            drawList.ChannelsSplit(2);

            Vector2 start = Vector2.Zero;
            if (Plugin.Configuration.DrawTimerButtons)
            {
                drawList.ChannelsSetCurrent(1);

                using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero))
                {
                    using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 5f))
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

                        start = ImGui.GetItemRectMin();

                        ImGui.SameLine();

                        // Clear racing lines button
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

                        ImGui.SameLine();

                        // Leave race button
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowRightFromBracket))
                        {
                            Plugin.KickClientFromParkour();
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Quit parkour");
                        }
                    }
                }

                //ImGui.Spacing();
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

                // Add text
                drawList.ChannelsSetCurrent(1);
                ImGui.TextUnformatted(timerText);

                drawList.ChannelsSetCurrent(0);

                if (start == Vector2.Zero)
                {
                    start = ImGui.GetItemRectMin();
                }

                // Add the background color
                drawList.AddRectFilled(start - ImGui.GetStyle().FramePadding * 2, ImGui.GetItemRectMax() + ImGui.GetStyle().FramePadding * 2, color);
                Vector2 lastpos = ImGui.GetItemRectMax() + ImGui.GetStyle().FramePadding * 2;

                Plugin.FontManager.PopFont();

                // Display the latest splits
                foreach (var split in splits)
                {
                    drawList.ChannelsSetCurrent(1);

                    StringBuilder sb = new StringBuilder();

                    // Set color of text (green = faster split, red = slower)
                    Vector4 splitCol = ImGuiColors.DalamudRed;
                    if (split < 0)
                        splitCol = ImGuiColors.HealerGreen;

                    // Add the time
                    sb.Append(Time.PrettyFormatTimeSpanSigned(TimeSpan.FromMilliseconds(split)));

                    // Display the split
                    ImGui.TextColored(splitCol, sb.ToString());

                    drawList.ChannelsSetCurrent(0);

                    // Avoid clipping
                    Vector2 startRect = new Vector2(ImGui.GetItemRectMin().X - ImGui.GetStyle().FramePadding.X * 2, MathF.Max(ImGui.GetItemRectMin().Y - ImGui.GetStyle().FramePadding.Y * 2, lastpos.Y));

                    // Add background to split
                    drawList.AddRectFilled(startRect, ImGui.GetItemRectMax() + ImGui.GetStyle().FramePadding * 2, color);
                    lastpos = ImGui.GetItemRectMax() + ImGui.GetStyle().FramePadding * 2;
                }

            }

            drawList.ChannelsMerge();
        }
    }
}
