using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Dalamud.Utility.Numerics;
using ImGuiNET;
using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Windows
{
    public class TimerWindow : Window, IDisposable
    {
        private Plugin Plugin { get; }

        public TimerWindow(Plugin Plugin) : base("Race Timer##race")
        {
            Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDecoration;

            this.Plugin = Plugin;
        }

        public void Dispose()
        {

        }

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

            Vector2 endPos = ImGui.GetCursorPos();

            //ImGui.SetCursorPos(startPos);

            //ImDrawListPtr drawList = ImGui.GetBackgroundDrawList();

            //drawList.AddRectFilled(startPos, endPos, Plugin.Configuration.TimerColor));

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
