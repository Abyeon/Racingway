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
        if (Plugin.ClientState.LocalPlayer != null)
        {
            ImGui.Text($"Current position: {Plugin.ClientState.LocalPlayer.Position.ToString()}");
        }

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
            foreach(var actor in Plugin.trackedPlayers.Values)
            {
                actor.raceLine.Clear();
            }
        }

        if (ImGui.Button("Add Trigger"))
        {
            Plugin.triggers.Add(new Trigger(Plugin.ClientState.LocalPlayer.Position, Vector3.One, Vector3.Zero));
        }

        int id = 0;

        foreach (Trigger trigger in Plugin.triggers)
        {
            ImGui.Separator();
            if (ImGuiComponents.IconButton(id, Dalamud.Interface.FontAwesomeIcon.Eraser))
            {
                Plugin.triggers.Remove(trigger);
                continue;
            }

            id++;
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(id, Dalamud.Interface.FontAwesomeIcon.LocationArrow))
            {
                trigger.cube.Position = Plugin.ClientState.LocalPlayer.Position;
                Plugin.ChatGui.Print($"Trigger position set to {trigger.cube.Position}");
            }

            ImGui.SameLine();
            if (ImGui.TreeNode($"Type##{id}"))
            {
                int selected = -1;
                for (int i = 0; i < 5; i++)
                {
                    ImGui.Indent();
                    if (ImGui.Selectable($"Object {i}", selected == i))
                    {
                        selected = i;
                    }
                    ImGui.Unindent();
                }

                ImGui.TreePop();
            }

            id++;
            ImGui.DragFloat3($"Position##{id}", ref trigger.cube.Position, 0.1f);

            id++;
            if (ImGui.DragFloat3($"Scale##{id}", ref trigger.cube.Scale, 0.1f))
            {
                trigger.cube.UpdateVerts();
            }

            id++;
            ImGui.DragFloat3($"Rotation##{id}", ref trigger.cube.Rotation, 0.1f);
            foreach (Vector3 v in trigger.cube.Vertices) {
                ImGui.Text(v.ToString());
            }
        }

        ImGui.Spacing();
    }
}
