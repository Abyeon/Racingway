using Dalamud.Interface.Components;
using ImGuiNET;
using Newtonsoft.Json;
using Racingway.Collision;
using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Tabs
{
    public class Routes : ITab
    {
        public string Name => "Routes";

        private Plugin Plugin { get; }

        private bool hasStart = false;
        private bool hasFinish = false;

        public Routes(Plugin plugin)
        {
            this.Plugin = plugin;

            foreach (Trigger trigger in plugin.Configuration.Triggers)
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

        public void Dispose()
        {

        }

        public void Draw()
        {
            ImGui.Text($"Current position: {Plugin.ClientState.LocalPlayer.Position.ToString()}");

            if (ImGui.Button("Add Trigger"))
            {
                // We set the trigger position slightly below the player due to SE position jank.
                Trigger newTrigger = new Trigger(Plugin.ClientState.LocalPlayer.Position - new Vector3(0, 0.1f, 0), Vector3.One, Vector3.Zero);
                Plugin.Configuration.Triggers.Add(newTrigger);
                Plugin.SubscribeToTriggers();

                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.FileImport))
            {
                try
                {
                    string data = ImGui.GetClipboardText();
                    var Json = Compression.FromCompressedBase64(data);

                    List<Trigger>? import = null;

                    import = JsonConvert.DeserializeObject<List<Trigger>>(Json);

                    if (import != null)
                    {
                        Plugin.Configuration.Triggers = import;
                        Plugin.SubscribeToTriggers();
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
            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.FileExport))
            {
                string text = Compression.ToCompressedBase64(Plugin.Configuration.Triggers);
                ImGui.SetClipboardText(text);
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Export config to clipboard.");
            }

            int id = 0;

            foreach (Trigger trigger in Plugin.Configuration.Triggers)
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
                    Plugin.Configuration.Triggers.Remove(trigger);
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
                    trigger.Cube.Position = Plugin.ClientState.LocalPlayer.Position - new Vector3(0, 0.1f, 0);
                    Plugin.ChatGui.Print($"[RACE] Trigger position set to {trigger.Cube.Position}");
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
                    foreach (Trigger.TriggerType triggerType in (Trigger.TriggerType[])Enum.GetValues(typeof(Trigger.TriggerType)))
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
                                    }
                                    else
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
                if (ImGui.DragFloat3($"Position##{id}", ref trigger.Cube.Position, 0.1f))
                {
                    Plugin.Configuration.Save();
                }

                id++;
                if (ImGui.DragFloat3($"Scale##{id}", ref trigger.Cube.Scale, 0.1f))
                {
                    trigger.Cube.UpdateVerts();
                    Plugin.Configuration.Save();
                }

                id++;
                if (ImGui.DragFloat3($"Rotation##{id}", ref trigger.Cube.Rotation, 0.1f))
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
}
