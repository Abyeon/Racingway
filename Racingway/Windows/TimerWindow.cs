using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Racingway.Utils;

namespace Racingway.Windows
{
    public class TimerWindow : Window, IDisposable
    {
        private Plugin Plugin { get; }

        public TimerWindow(Plugin plugin)
            : base("Race Timer##race")
        {
            Flags =
                ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoDecoration;

            this.Plugin = plugin;
        }

        public void Dispose() { }

        public override void PreDraw()
        {
            base.PreDraw();
            ImGui.PushStyleColor(ImGuiCol.WindowBg, Plugin.Configuration.TimerColor);
            ImGui.PushStyleColor(
                ImGuiCol.FrameBg,
                new Vector4(0, 0, 0, Plugin.Configuration.TimerColor.W)
            );
            Plugin.FontManager.PushFont();
        }

        public override void Draw()
        {
            // Set font size
            ImGui.SetWindowFontScale(Plugin.Configuration.TimerSize);

            // Center window
            Position = ImGuiHelpers.MainViewport.Size / 2 - Size / 2;

            // Display timer
            string time = Time.PrettyFormatTimeSpan(
                TimeSpan.FromMilliseconds(Plugin.LocalTimer.ElapsedMilliseconds)
            );

            // Use green text when running, white when stopped
            if (Plugin.LocalTimer.IsRunning)
            {
                ImGui.TextColored(ImGuiColors.ParsedGreen, time);
            }
            else
            {
                ImGui.Text(time);
            }

            // Add icon buttons on the same line as the timer
            ImGui.SameLine();

            // Small spacing between timer and buttons
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5);

            // Clear lines button (trash icon)
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
            {
                foreach (var actor in Plugin.trackedPlayers.Values)
                {
                    actor.raceLine.Clear();
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Clear racing lines");
            }

            // Open main window button (cog icon)
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
            {
                Plugin.MainWindow.Toggle();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Open main window");
            }
        }

        public override void PostDraw()
        {
            Plugin.FontManager.PopFont();
            ImGui.PopStyleColor(2);
            base.PostDraw();
        }
    }
}
