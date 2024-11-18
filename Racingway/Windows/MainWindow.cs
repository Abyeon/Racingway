using System;
using System.Numerics;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace Racingway.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;

    // We give this window a hidden ID using ##
    // So that the user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin)
        : base("Race Setup##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text($"Current position: {Plugin.ClientState.LocalPlayer.Position.ToString()}");

        ///
        /// Start box
        /// 

        if (ImGuiComponents.IconButton(0, Dalamud.Interface.FontAwesomeIcon.LocationArrow))
        {
            //Plugin.ToggleConfigUI();
            //ImGui.Text(Plugin.ClientState.LocalPlayer.Position.ToString());
            Plugin.startBoxMin = Plugin.ClientState.LocalPlayer.Position;
            //Plugin.ChatGui.Print($"Start box min has been set to {Plugin.startBoxMin}");
            Plugin.ChatGui.Print($"Start box min has been set to {Plugin.startBoxMin}");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Set the current position as the minimum for your starting box.");
        }

        ImGui.SameLine();
        ImGui.DragFloat3("Start Box Min", ref Plugin.startBoxMin);

        if (ImGuiComponents.IconButton(1, Dalamud.Interface.FontAwesomeIcon.LocationArrow))
        {
            Plugin.startBoxMax = Plugin.ClientState.LocalPlayer.Position;
            Plugin.ChatGui.Print($"Start box max has been set to {Plugin.startBoxMax}");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Set the current position as the maximum for your starting box.");
        }

        ImGui.SameLine();
        ImGui.DragFloat3("Start Box Max", ref Plugin.startBoxMax);

        if (ImGuiComponents.IconButton(0, Dalamud.Interface.FontAwesomeIcon.LocationArrow))
        {
            //Plugin.ToggleConfigUI();
            //ImGui.Text(Plugin.ClientState.LocalPlayer.Position.ToString());
            Plugin.startBoxMin = Plugin.ClientState.LocalPlayer.Position;
            //Plugin.ChatGui.Print($"Start box min has been set to {Plugin.startBoxMin}");
            Plugin.ChatGui.Print($"Start box min has been set to {Plugin.startBoxMin}");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Set the current position as the minimum for your ending box.");
        }

        ///
        /// End box
        /// 

        ImGui.SameLine();
        ImGui.DragFloat3("End Box Min", ref Plugin.endBoxMin);

        if (ImGuiComponents.IconButton(1, Dalamud.Interface.FontAwesomeIcon.LocationArrow))
        {
            Plugin.endBoxMax = Plugin.ClientState.LocalPlayer.Position;
            Plugin.ChatGui.Print($"End box max has been set to {Plugin.endBoxMax}");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Set the current position as the maximum for your ending box.");
        }

        ImGui.SameLine();
        ImGui.DragFloat3("End Box Max", ref Plugin.endBoxMax);

        ImGui.Spacing();
    }
}
