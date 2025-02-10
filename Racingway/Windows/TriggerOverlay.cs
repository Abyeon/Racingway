using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Interface.Utility;
using System.Numerics;
using Racingway.Utils;
using Serilog;
using Dalamud.Game.ClientState.Objects.Types;
using ImGuizmoNET;


namespace Racingway.Windows
{
    public class TriggerOverlay : Window, IDisposable
    {
        private Plugin Plugin;

        private ImGuiIOPtr Io;

        public TriggerOverlay(Plugin plugin) : base("Trigger Overlay")
        {
            Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoNavInputs | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoInputs;
            
            Plugin = plugin;
        }

        public void Dispose()
        {

        }

        public unsafe override void Draw()
        {
            ImGuiHelpers.SetWindowPosRelativeMainViewport("Trigger Overlay", new Vector2(0, 0));
            
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            DrawHelper draw = new DrawHelper(drawList);

            Io = ImGui.GetIO();
            ImGui.SetWindowSize(Io.DisplaySize);

            if (Plugin.Configuration.DrawRacingLines)
            {
                foreach (var npc in Plugin.trackedNPCs)
                {
                    //// Draw a cube around the player for fun
                    if (npc.IsTargetable && npc.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventNpc && npc.Name.ToString() == "Koharu")
                    {
                        var npcBase = Plugin.eNpcBases.GetRowOrDefault(npc.DataId);
                        Plugin.Log.Debug(npcBase.ToString());
                        Vector3 pos = npc.Position;
                        float rotation = npc.Rotation;

                        draw.DrawText3d(npc.Name.ToString(), pos, 0xFFFFFFFF);
                        draw.DrawCubeFilled(new Cube(pos, new Vector3(0.5f, 1, 0.5f), new Vector3(rotation, 0, 0)), 0x22FFFFFF, 2.0f);
                    }
                }

                foreach (var actor in Plugin.trackedPlayers.Values)
                {
                    Vector3[] raceLine = actor.raceLine.ToArray();

                    //// Draw a cube around the player for fun
                    //Vector3 pos = actor.position;
                    //float rotation = actor.actor.Rotation;

                    //draw.DrawText3d(actor.actor.Name.ToString(), pos, 0xFFFFFFFF);
                    //draw.DrawCubeFilled(new Cube(pos, new Vector3(0.5f, 1, 0.5f), new Vector3(rotation, 0, 0)), 0x22FFFFFF, 2.0f);

                    for (var i = 1; i < raceLine.Length; i++)
                    {
                        if (raceLine[i - 1] == Vector3.Zero) continue;

                        draw.DrawLine3d(raceLine[i - 1], raceLine[i], 0x55FFFFFF, 2.0f);
                    }
                }
            }

            // Loop through triggers and draw them
            foreach (var trigger in Plugin.triggers)
            {
                if (trigger == null) continue;

                //draw.DrawCube(trigger.cube, trigger.color, 5.0f);
                draw.DrawCubeFilled(trigger.cube, trigger.color, 5.0f);
            }
        }
    }
}
