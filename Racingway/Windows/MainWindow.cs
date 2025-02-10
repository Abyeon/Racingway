using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Numerics;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using ImGuizmoNET;
using Newtonsoft.Json;
using Racingway.Utils;

namespace Racingway.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;

    private bool hasStart = false;
    private bool hasFinish = false;

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

        foreach (Trigger trigger in plugin.Configuration.triggers)
        {
            if (trigger.selectedType == Trigger.TriggerType.Start)
            {
                hasStart = true;
            }
            if (trigger.selectedType == Trigger.TriggerType.Finish)
            {
                hasFinish = true;
            }
        }
    }

    public void Dispose() { }

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
            Plugin.Configuration.Save();
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
            Plugin.Configuration.triggers.Add(new Trigger(Plugin.ClientState.LocalPlayer.Position - new Vector3(0, 0.1f, 0), Vector3.One, Vector3.Zero));
            Plugin.Configuration.Save();
        }

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.FileImport))
        {
            try
            {
                string data = ImGui.GetClipboardText();
                var Json = ImportExport.FromCompressedBase64(data);

                List<Trigger>? import = null;

                import = JsonConvert.DeserializeObject<List<Trigger>>(Json);

                if (import != null)
                {
                    Plugin.Configuration.triggers = import;
                    Plugin.Configuration.Save();
                }
            }
            catch (JsonReaderException ex)
            {
                Plugin.ChatGui.PrintError($"[RACE] Failed to import setup. {ex.Message}");
                Plugin.Log.Error(ex, "Failed to import setup");
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip("Import config from clipboard.");
        }

        ImGui.SameLine();
        if(ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.FileExport))
        {
            string text = ImportExport.ToCompressedBase64(Plugin.Configuration.triggers);
            ImGui.SetClipboardText(text);
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip("Export config to clipboard.");
        }

        int id = 0;

        foreach (Trigger trigger in Plugin.Configuration.triggers)
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
                Plugin.Configuration.triggers.Remove(trigger);
                Plugin.Configuration.Save();
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
                Plugin.Configuration.Save();
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
                                break;
                        }

                        updateStartFinishBools(trigger.selectedType);

                        trigger.selectedType = triggerType;
                        Plugin.Configuration.Save();
                    }
                }
                ImGui.Unindent();

                ImGui.TreePop();
            }

            id++;
            if (ImGui.DragFloat3($"Position##{id}", ref trigger.cube.Position, 0.1f))
            {
                Plugin.Configuration.Save();
            }

            id++;
            if (ImGui.DragFloat3($"Scale##{id}", ref trigger.cube.Scale, 0.1f))
            {
                trigger.cube.UpdateVerts();
                Plugin.Configuration.Save();
            }

            id++;
            if (ImGui.DragFloat3($"Rotation##{id}", ref trigger.cube.Rotation, 0.1f))
            {
                Plugin.Configuration.Save();
            }

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
