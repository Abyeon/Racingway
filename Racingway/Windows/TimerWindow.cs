using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Dalamud.Utility.Numerics;
using ImGuiNET;
using Racingway.Utils;

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
                | ImGuiWindowFlags.NoDecoration;

            this.Plugin = Plugin;
        }

        public void Dispose() { }

        public override void PreDraw()
        {
            base.PreDraw();
            ImGui.PushStyleColor(ImGuiCol.WindowBg, Plugin.Configuration.TimerColor);

            Vector4 color;
            unsafe
            {
                color = *ImGui.GetStyleColorVec4(ImGuiCol.FrameBg);
                color.W = Plugin.Configuration.TimerColor.W;
            }

            ImGui.PushStyleColor(ImGuiCol.FrameBg, color);
            Plugin.FontManager.PushFont();
        }

        public override void Draw()
        {
            ImGui.SetWindowFontScale(Plugin.Configuration.TimerSize);

            if (Plugin.FontManager.FontPushed && !Plugin.FontManager.FontReady)
            {
                ImGui.Text("Loading font, please wait...");
                return;
            }

            TimeSpan span = TimeSpan.FromMilliseconds(Plugin.LocalTimer.ElapsedMilliseconds);
            string timerText = Time.PrettyFormatTimeSpan(span);
            Vector2 startPos = ImGui.GetCursorPos();

            ImGui.Text(timerText);

            if (Plugin.Configuration.DrawTimerButtons)
            {
                // Add better spacing between timer and buttons
                ImGui.SameLine(0, 12);

                // Add padding to ensure enough space between timer and buttons
                float btnSize = 22; // Fixed size for buttons

                // Vertical alignment adjustment
                ImGui.SetCursorPosY(startPos.Y + (ImGui.GetTextLineHeight() - btnSize) * 0.5f);

                // Apply consistent button styling
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3.0f);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.3f, 0.3f, 0.6f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.4f, 0.4f, 0.8f));

                // Main window button
                if (ImGuiComponents.IconButton("##openMain", FontAwesomeIcon.ExternalLinkAlt, new Vector2(btnSize, btnSize)))
                {
                    Plugin.ToggleMainUI();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Open Main Window");
                }

                // Clear racing lines button
                ImGui.SameLine(0, 25); // Increased spacing between buttons from 4 to 9 pixels

                // Adjust vertical position for trash button (15% lower)
                float trashButtonOffset = ImGui.GetTextLineHeight() * 0.05f;
                ImGui.SetCursorPosY(startPos.Y + (ImGui.GetTextLineHeight() - btnSize) * 0.5f + trashButtonOffset);

                if (ImGuiComponents.IconButton("##clearLines", FontAwesomeIcon.Trash, new Vector2(btnSize, btnSize)))
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

                // Restore original style
                ImGui.PopStyleColor(2);
                ImGui.PopStyleVar();
            }

            ImGui.SetWindowFontScale(1.0f);
        }

        public override void PostDraw()
        {
            Plugin.FontManager.PopFont();
            base.PostDraw();
            ImGui.PopStyleColor();
            ImGui.PopStyleColor();
        }
    }
}
