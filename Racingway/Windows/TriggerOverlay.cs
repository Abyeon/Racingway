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
using Racingway.Race;
using LiteDB;
using Racingway.Race.Collision.Triggers;


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

            // Display Trigger debug UI
            if (Plugin.Configuration.DrawTriggers)
            {
                List<ITrigger> triggers = Plugin.LoadedRoutes.SelectMany(x => x.Triggers).ToList();

                foreach (var trigger in triggers)
                {
                    if (trigger == null) continue;

                    draw.DrawCubeFilled(trigger.Cube, trigger.Color, 5.0f);
                }
            }

            // Display player racing lines
            if (Plugin.Configuration.DrawRacingLines)
            {
                foreach (var actor in Plugin.trackedPlayers.Values)
                {
                    TimedVector3[] raceLine = actor.raceLine.ToArray();

                    for (var i = 1; i < raceLine.Length; i++)
                    {
                        if (raceLine[i - 1].asVector() == Vector3.Zero) continue;

                        draw.DrawLine3d(raceLine[i - 1].asVector(), raceLine[i].asVector(), 0x55FFFFFF, 2.0f);
                    }
                }
            }

            // Draw the selected Record's line
            if (Plugin.DisplayedRecord != null)
            {
                Record displayedRecord = Plugin.DisplayedRecord;
                TimedVector3[] displayedRecordLine = displayedRecord.Line;

                for (var i = 1; i < displayedRecordLine.Length; i++)
                {
                    if (displayedRecordLine[i - 1].asVector() == Vector3.Zero) continue;

                    // This allows for realtime ghost playback of player records
                    if (!Plugin.LocalTimer.IsRunning || Plugin.LocalTimer.ElapsedMilliseconds >= displayedRecordLine[i].Offset)
                    {
                        //Plugin.Log.Debug(Plugin.LocalTimer.ElapsedMilliseconds.ToString());
                        draw.DrawLine3d(displayedRecordLine[i - 1].asVector(), displayedRecordLine[i].asVector(), 0x55FFCCFF, 2.0f);
                    }
                }
            }

#if DEBUG
            // Debug info
            foreach (var actor in Plugin.trackedPlayers.Values)
            {
                draw.DrawText3d("Grounded: " + actor.isGrounded.ToString(), actor.position, 0xFFFFFFFF);
                draw.DrawText3d("In Mount: " + actor.inMount.ToString(), actor.position + new Vector3(0, 0.5f, 0), 0xFFFFFFFF);
            }
#endif
        }
    }
}
