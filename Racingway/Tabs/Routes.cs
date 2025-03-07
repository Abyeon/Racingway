using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using LiteDB;
//using Newtonsoft.Json;
using Racingway.Race;
using Racingway.Race.Collision;
using Racingway.Race.Collision.Triggers;
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
            updateStartFinishBools();
        }

        public void Dispose()
        {

        }

        public void Draw()
        {
            ImGui.Text($"Current position: {Plugin.ClientState.LocalPlayer.Position.ToString()}");

            int id = 0;

            using (var tree = ImRaii.TreeNode("Routes"))
            {
                if (tree.Success)
                {
                    foreach (Route route in Plugin.Storage.GetRoutes().Query().Where(x => x.Address.LocationId == Plugin.CurrentAddress.LocationId).ToList())
                    {
                        id++;
                        if (ImGui.Selectable($"{route.Name}##{id}", route.Id == Plugin.SelectedRoute))
                        {
                            if (route.Id == Plugin.SelectedRoute) return;
                            Plugin.SelectedRoute = route.Id;
                        }
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip(route.Id.ToString());
                        }
                    }
                }
            }

            Route? selectedRoute = Plugin.LoadedRoutes.FirstOrDefault(x => x.Id == Plugin.SelectedRoute, new Route(string.Empty, Plugin.CurrentAddress, string.Empty, new List<ITrigger>(), new List<Record>()));

            if (ImGui.Button("Create New Route"))
            {
                selectedRoute = new Route(string.Empty, Plugin.CurrentAddress, string.Empty, new List<ITrigger>(), new List<Record>());
                updateRoute(selectedRoute);
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.FileImport))
            {
                try
                {
                    string data = ImGui.GetClipboardText();
                    var Json = Compression.FromCompressedBase64(data);

                    BsonValue bson = JsonSerializer.Deserialize(Json);
                    Route route = BsonMapper.Global.Deserialize<Route>(bson);

                    Plugin.Storage.AddRoute(route);

                    // Only load route if its in our zone
                    if (!Plugin.LoadedRoutes.Contains(route) && Plugin.CurrentAddress.LocationId == route.Address.LocationId)
                    {
                        Plugin.LoadedRoutes.Add(route);
                        Plugin.SelectedRoute = route.Id;
                    }

                    Plugin.ChatGui.Print($"[RACE] Added {route.Name} to routes.");
                }
                catch (Exception ex)
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
                //string text = Compression.ToCompressedBase64(Plugin.Configuration.Triggers);
                //Plugin.Log.Debug(selectedRoute.JsonFriendly().ToString());
                //string input = selectedRoute.GetSerialized().AsDocument.ToString();
                string input = JsonSerializer.Serialize(selectedRoute.GetSerialized());
                string text = Compression.ToCompressedBase64(input);
                if (text != string.Empty)
                {
                    ImGui.SetClipboardText(text);
                } else
                {
                    Plugin.ChatGui.PrintError("[RACE] No route selected.");
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Export config to clipboard.");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Save))
            {
                Route routeWithSameName = Plugin.Storage.GetRoutes().FindOne(x => x.Name == selectedRoute.Name);

                if (selectedRoute.Name == string.Empty)
                {
                    Plugin.ChatGui.PrintError("[RACE] Cannot save a route without a name.");
                    return;
                } else if (selectedRoute.Id != routeWithSameName.Id)
                {
                    Plugin.ChatGui.PrintError("[RACE] Cannot save a route with the same name as another.");
                    return;
                } else if (selectedRoute == null)
                {
                    Plugin.ChatGui.PrintError("[RACE] Cannot save an empty route.. How did we get here?");
                }

                //string text = Compression.ToCompressedBase64(Plugin.Configuration.SelectedRoute);
                //Plugin.Configuration.CompressedRoutes.Add(text);
                updateRoute(selectedRoute);
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Save route to config.");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Trash))
            {
                Plugin.LoadedRoutes.Remove(selectedRoute);

                Plugin.Storage.GetRoutes().Delete(selectedRoute.Id);
                Plugin.Storage.UpdateRouteCache();

                Plugin.SelectedRoute = Plugin.LoadedRoutes.Count == 0 ? null : Plugin.LoadedRoutes[0].Id;
                selectedRoute = Plugin.LoadedRoutes.Count == 0 ? null : Plugin.LoadedRoutes[0];
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Delete route. (IRREVERSIBLE)");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Recycle))
            {
                Parallel.Invoke(() => Plugin.territoryHelper.GetLocationID());
            }

            if (selectedRoute == null)
            {
                ImGui.Text("If you're seeing this text, consider pressing the refresh button.");
                return;
            }

            string name = selectedRoute.Name;
            if (ImGui.InputText("Name", ref name, 64))
            {
                // Something
                if (name == string.Empty) return;
                selectedRoute.Name = name;
                updateRoute(selectedRoute);
            }

            string description = selectedRoute.Description;
            if (ImGui.InputText("Description", ref description, 256))
            {
                selectedRoute.Description = description;
                updateRoute(selectedRoute);
            }

            if (ImGui.Button("Add Trigger"))
            {
                // We set the trigger position slightly below the player due to SE position jank.
                Checkpoint newTrigger = new Checkpoint(selectedRoute, Plugin.ClientState.LocalPlayer.Position - new Vector3(0, 0.1f, 0), Vector3.One, Vector3.Zero);
                selectedRoute.Triggers.Add(newTrigger);
                updateRoute(selectedRoute);
            }

            for (int i = 0; i < selectedRoute.Triggers.Count; i++)
            {
                ITrigger trigger = selectedRoute.Triggers[i];

                ImGui.Separator();
                if (ImGuiComponents.IconButton(id, Dalamud.Interface.FontAwesomeIcon.Eraser))
                {
                    updateStartFinishBools();
                    selectedRoute.Triggers.Remove(trigger);
                    updateRoute(selectedRoute);
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
                    selectedRoute.Triggers[i].Cube.Position = Plugin.ClientState.LocalPlayer.Position - new Vector3(0, 0.1f, 0);
                    Plugin.ChatGui.Print($"[RACE] Trigger position set to {trigger.Cube.Position}");

                    updateRoute(selectedRoute);
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Set trigger position to your characters position.");
                }

                ImGui.SameLine();
                if (ImGui.TreeNode($"Type##{id}"))
                {
                    ImGui.Indent();

                    if (ImGui.Selectable("Start", trigger is Start))
                    {
                        if (trigger is Start) return;

                        if (hasStart)
                        {
                            Plugin.ChatGui.PrintError("[RACE] There is already a start trigger in this route.");
                        } else
                        {
                            selectedRoute.Triggers[i] = new Start(trigger.Route, trigger.Cube);
                            updateRoute(selectedRoute);
                        }
                    }

                    if (ImGui.Selectable("Checkpoint", trigger is Checkpoint))
                    {
                        if (trigger is Checkpoint) return;
                        selectedRoute.Triggers[i] = new Checkpoint(trigger.Route, trigger.Cube);
                        updateRoute(selectedRoute);
                    }

                    if (ImGui.Selectable("Fail", trigger is Fail))
                    {
                        if (trigger is Fail) return;
                        selectedRoute.Triggers[i] = new Fail(trigger.Route, trigger.Cube);
                        updateRoute(selectedRoute);
                    }

                    if (ImGui.Selectable("Finish", trigger is Finish))
                    {
                        if (trigger is Finish) return;

                        if (hasFinish)
                        {
                            Plugin.ChatGui.PrintError("[RACE] There is already a finish trigger in this route.");
                        }
                        else
                        {
                            selectedRoute.Triggers[i] = new Finish(trigger.Route, trigger.Cube);
                            updateRoute(selectedRoute);
                        }
                    }

                    ImGui.Unindent();

                    ImGui.TreePop();
                }

                id++;
                Vector3 position = trigger.Cube.Position;
                if (ImGui.DragFloat3($"Position##{id}", ref position, 0.1f))
                {
                    selectedRoute.Triggers[i].Cube.Position = position;
                    updateRoute(selectedRoute);
                }

                id++;
                Vector3 scale = trigger.Cube.Scale;
                if (ImGui.DragFloat3($"Scale##{id}", ref scale, 0.1f))
                {
                    selectedRoute.Triggers[i].Cube.Scale = scale;
                    selectedRoute.Triggers[i].Cube.UpdateVerts();
                    updateRoute(selectedRoute);
                }

                id++;
                Vector3 rotation = trigger.Cube.Rotation;
                if (ImGui.DragFloat3($"Rotation##{id}", ref rotation, 0.1f))
                {
                    selectedRoute.Triggers[i].Cube.Rotation = rotation;
                    updateRoute(selectedRoute);
                }
            }

            ImGui.Spacing();
        }

        private void updateRoute(Route route)
        {
            if (route == null) return;

            int index = Plugin.LoadedRoutes.FindIndex(x => x.Id == Plugin.SelectedRoute);
            if (index == -1)
            {
                index = Plugin.LoadedRoutes.FindIndex(x => x == route);
            }

            if (index != -1)
            {
                Plugin.LoadedRoutes[index] = route;
            } else
            {
                Plugin.LoadedRoutes.Add(route);
            }

            Plugin.SelectedRoute = route.Id;

            Plugin.SubscribeToRouteEvents();
            updateStartFinishBools();

            Plugin.Storage.AddRoute(route);
            Plugin.Storage.UpdateRouteCache();
        }

        private void updateStartFinishBools()
        {
            try
            {
                if (Plugin.LoadedRoutes.Count == 0) return;

                Route selectedRoute = Plugin.LoadedRoutes.First(x => x.Id == Plugin.SelectedRoute);

                hasStart = false;
                hasFinish = false;

                foreach (ITrigger trigger in selectedRoute.Triggers)
                {
                    if (trigger is Start)
                    {
                        hasStart = true;
                    }
                    if (trigger is Finish)
                    {
                        hasFinish = true;
                    }
                }
            } catch (Exception e)
            {
                Plugin.Log.Error(e.ToString());
            }
        }
    }
}
