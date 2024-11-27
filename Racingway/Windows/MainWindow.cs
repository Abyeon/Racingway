using System;
using System.Numerics;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using ImGuizmoNET;
using Racingway.Utils;

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

        if (ImGui.Button("Show Trigger Overlay"))
        {
            Plugin.ToggleTriggerUI();
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear racing lines"))
        {
            foreach(var actor in Plugin.trackedPlayers)
            {
                actor.raceLine.Clear();
            }
        }

        if (ImGui.Button("Add Trigger"))
        {
            Plugin.triggers.Add(new Utils.Trigger(Plugin));
            // Handle making a list of triggers
        }

        foreach(Trigger trigger in Plugin.triggers)
        {
            if (ImGuiComponents.IconButton(0, Dalamud.Interface.FontAwesomeIcon.LocationArrow))
            {
                trigger.min = Plugin.ClientState.LocalPlayer.Position;
                Plugin.ChatGui.Print($"Box min has been set to {trigger.min}");
            }

            ImGui.SameLine();
            ImGui.DragFloat3("Trigger min", ref trigger.min);

            if (ImGuiComponents.IconButton(0, Dalamud.Interface.FontAwesomeIcon.LocationArrow))
            {
                trigger.max = Plugin.ClientState.LocalPlayer.Position;
                Plugin.ChatGui.Print($"Box max has been set to {trigger.max}");
            }

            ImGui.SameLine();
            ImGui.DragFloat3("Trigger max", ref trigger.max);
        }

        ///
        /// Start box
        /// 

        //if (ImGuiComponents.IconButton(0, Dalamud.Interface.FontAwesomeIcon.LocationArrow))
        //{
        //    //Plugin.ToggleConfigUI();
        //    //ImGui.Text(Plugin.ClientState.LocalPlayer.Position.ToString());
        //    Plugin.startBoxMin = Plugin.ClientState.LocalPlayer.Position;
        //    //Plugin.ChatGui.Print($"Start box min has been set to {Plugin.startBoxMin}");
        //    Plugin.ChatGui.Print($"Start box min has been set to {Plugin.startBoxMin}");
        //}

        //if (ImGui.IsItemHovered())
        //{
        //    ImGui.SetTooltip("Set the current position as the minimum for your starting box.");
        //}

        ImGui.Spacing();
    }
}
