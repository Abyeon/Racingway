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
        : base("Race Setup##abe")
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

        ImGui.Text($"{Plugin.TriggerOverlay.IsOpen.ToString()}");
        if (ImGui.Button("Show Trigger Overlay"))
        {
            Plugin.ToggleTriggerUI();
        }

        ImGui.SameLine();
        if (ImGui.Button("Toggle Racing Lines"))
        {
            Plugin.Configuration.DrawRacingLines = !Plugin.Configuration.DrawRacingLines;
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

        int id = 0;
        foreach(Trigger trigger in Plugin.triggers)
        {
            ImGui.Separator();
            if (ImGuiComponents.IconButton(id, Dalamud.Interface.FontAwesomeIcon.Eraser))
            {
                Plugin.triggers.Remove(trigger);
                continue;
            }

            id++;

            if (ImGuiComponents.IconButton(id, Dalamud.Interface.FontAwesomeIcon.LocationArrow))
            {
                trigger.min = Plugin.ClientState.LocalPlayer.Position;
                Plugin.ChatGui.Print($"Box min has been set to {trigger.min}");
            }

            id++;

            ImGui.SameLine();
            ImGui.DragFloat3($"Min##{id}", ref trigger.min, 0.1f);

            if (ImGuiComponents.IconButton(id, Dalamud.Interface.FontAwesomeIcon.LocationArrow))
            {
                trigger.max = Plugin.ClientState.LocalPlayer.Position;
                Plugin.ChatGui.Print($"Box max has been set to {trigger.max}");
            }

            id++;

            ImGui.SameLine();
            ImGui.DragFloat3($"Max##{id}", ref trigger.max, 0.1f);
        }

        ImGui.Spacing();
    }
}
