using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Tabs
{
    internal class About : ITab
    {
        public string Name => "About";

        private Plugin Plugin { get; set; }

        public About(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void Dispose()
        {

        }

        public void Draw()
        {
            try
            {
                ImGui.TextColored(ImGuiColors.DalamudRed, $"{Plugin.PluginInterface.Manifest.Name.ToString()} v{Plugin.PluginInterface.Manifest.AssemblyVersion.ToString()}");

                ImGui.SameLine();
                ImGui.Text("by Abyeon");

                if (Plugin.PluginInterface.Manifest.Changelog != null)
                {
                    ImGui.Dummy(new Vector2(0, 10));
                    ImGui.Text("Changelog: ");
                    ImGui.TextWrapped(Plugin.PluginInterface.Manifest.Changelog.ToString());
                }

                string fileSize = Plugin.Storage.GetFileSizeString();
                if (fileSize != string.Empty)
                {
                    ImGui.TextColored(ImGuiColors.DalamudGrey, $"Size on disk: {fileSize}");
                }

                ImGui.Dummy(new Vector2(0, 10));

                ImGui.Text("Triggers:");

                using (var child = ImRaii.TreeNode("Start", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (child.Success) ImGui.TextWrapped("The player's timer begins when the player either leaves this trigger or jumps!" +
                        " The idea is to have this right before the first jump, so your timer starts when you jump." +
                        " There can only be one of these at a time in a route." +
                        " This technically triggers a fail when a player re-enters it, so that will get logged to the chat if Log Fails is enabled.");
                }

                using (var child = ImRaii.TreeNode("Checkpoint", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (child.Success) ImGui.TextWrapped("This currently has no function. However it will be used for splits in the future.");
                }

                using (var child = ImRaii.TreeNode("Fail", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (child.Success) ImGui.TextWrapped("As soon as a player enters this trigger, they get kicked out of the race!");
                }

                using (var child = ImRaii.TreeNode("Finish", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (child.Success) ImGui.TextWrapped("The timer will end as soon as a player touches the ground inside this trigger." +
                        " Make sure to cover anywhere a player could feasibly land at the end of your route!");
                }

                using (var child = ImRaii.TreeNode("Loop", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (child.Success) ImGui.TextWrapped("Timer starts when the player exits and ends when they enter again");
                }
                
                ImGui.Dummy(new Vector2(0, 10));

                if (ImGui.Button("GitHub"))
                {
                    Util.OpenLink("https://github.com/Abyeon/Racingway");
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Wanna make fun of my code? Have a look!");
                }

                ImGui.SameLine();
                if (ImGui.Button("Strange Housing"))
                {
                    Util.OpenLink("https://strangehousing.ju.mp/");
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Want to experience more jump puzzles? Check out Strange Housing!");
                }

                ImGui.SameLine();
                using (_ = ImRaii.PushFont(UiBuilder.IconFont))
                {
                    using (_ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                    {
                        if (ImGui.Button($"{FontAwesomeIcon.Heart.ToIconString()}"))
                        {
                            Util.OpenLink("https://ko-fi.com/abyeon");
                        }
                    }
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Support the dev <3");
                }

            } catch (Exception ex)
            {
                Plugin.Log.Error(ex.ToString());
            }
            
        }
    }
}
