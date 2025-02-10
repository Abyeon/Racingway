using System;
using System.Net.NetworkInformation;
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

    private bool hasStart = false;
    private bool hasFinish = false;

    public override void Draw()
    {
        if (Plugin.ClientState == null) return;

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
            foreach(var actor in Plugin.trackedPlayers.Values)
            {
                actor.raceLine.Clear();
            }
        }

        if (ImGui.Button("Add Trigger"))
        {
            // We set the trigger position slightly below the player due to SE position jank.
            Plugin.triggers.Add(new Trigger(Plugin.ClientState.LocalPlayer.Position - new Vector3(0, 0.1f, 0), Vector3.One, Vector3.Zero));
        }

        int id = 0;

        foreach (Trigger trigger in Plugin.triggers)
        {
            switch (trigger.selectedType)
            {
                case Trigger.TriggerType.Start:
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFF60F542);
                    break;
                case Trigger.TriggerType.Fail:
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFF425AF5);
                    break;
                case Trigger.TriggerType.Finish:
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFFF58742);
                    break;
                default:
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFFFFF);
                    break;
            }

            ImGui.Separator();
            if (ImGuiComponents.IconButton(id, Dalamud.Interface.FontAwesomeIcon.Eraser))
            {
                updateStartFinishBools(trigger.selectedType);
                Plugin.triggers.Remove(trigger);
                continue;
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Erase this trigger.");
            }

            id++;
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(id, Dalamud.Interface.FontAwesomeIcon.ArrowsToDot))
            {
                // We set the trigger position slightly below the player due to SE position jank.
                trigger.cube.Position = Plugin.ClientState.LocalPlayer.Position - new Vector3(0, 0.1f, 0);
                Plugin.ChatGui.Print($"[RACE] Trigger position set to {trigger.cube.Position}");
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Set trigger position to your characters position.");
            }

            ImGui.SameLine();
            if (ImGui.TreeNode($"Type##{id}"))
            {
                ImGui.Indent();

                // Super dumb way to get all the types of triggers.. Dont judge me.
                foreach (Trigger.TriggerType triggerType in (Trigger.TriggerType[]) Enum.GetValues(typeof(Trigger.TriggerType)))
                {
                    if (ImGui.Selectable(triggerType.ToString(), triggerType == trigger.selectedType))
                    {
                        if (triggerType == trigger.selectedType) continue;

                        switch (triggerType)
                        {
                            case Trigger.TriggerType.Start:
                                if (hasStart)
                                {
                                    Plugin.ChatGui.PrintError("[RACE] There is already a start trigger in the list!");
                                    continue;
                                } else
                                {
                                    hasStart = true;
                                }
                                break;
                            case Trigger.TriggerType.Finish:
                                if (hasFinish)
                                {
                                    Plugin.ChatGui.PrintError("[RACE] There is already a finish trigger in the list!");
                                    continue;
                                }
                                else
                                {
                                    hasFinish = true;
                                }
                                break;
                            default:
                                updateStartFinishBools(trigger.selectedType);
                                break;
                        }

                        trigger.selectedType = triggerType;
                    }
                }
                ImGui.Unindent();

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

            ImGui.PopStyleColor();
        }

        ImGui.Spacing();
    }

    private void updateStartFinishBools(Trigger.TriggerType selectedType)
    {
        switch (selectedType)
        {
            case Trigger.TriggerType.Start:
                hasStart = false;
                break;
            case Trigger.TriggerType.Finish:
                hasFinish = false;
                break;
            default:
                return;
        }
    }
}
