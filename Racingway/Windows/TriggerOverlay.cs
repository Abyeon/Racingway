using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility.Numerics;
using ImGuiNET;
using ImGuizmoNET;
using LiteDB;
using Racingway.Race;
using Racingway.Race.Collision.Triggers;
using Racingway.Race.LineStyles;
using Racingway.Utils;
using Serilog;

namespace Racingway.Windows
{
    public class TriggerOverlay : Window, IDisposable
    {
        private Plugin Plugin { get; }

        private ImGuiIOPtr Io;

        public List<ILineStyle> LineStyles { get; private set; }
        public ILineStyle selectedStyle { get; set; }

        public TriggerOverlay(Plugin plugin)
            : base("Trigger Overlay")
        {
            Flags =
                ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoDocking
                | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.NoNavInputs
                | ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoInputs;

            this.Plugin = plugin;
            this.LineStyles = [new Line(plugin), new Dotted(plugin), new DottedLine(plugin)];

            this.selectedStyle = this.LineStyles.FirstOrDefault(
                x => x.Name == plugin.Configuration.LineStyle,
                this.LineStyles[0]
            );
        }

        public void Dispose() { }

        public override unsafe void Draw()
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
                    if (trigger == null)
                        continue;

                    try
                    {
                        float snapDistance = Plugin.Configuration.UseSnapping ? Plugin.Configuration.SnapDistance : 0f;

                        if (trigger.Equals(Plugin.SelectedTrigger))
                            draw.DrawGizmo(
                                ref trigger.Cube.Position,
                                ref trigger.Cube.Rotation,
                                ref trigger.Cube.Scale,
                                trigger.Id.ToString(),
                                snapDistance);
                    } catch (Exception ex)
                    {
                        Plugin.Log.Error(ex.ToString());
                    }

                    uint color = trigger.Color;
                    if (trigger.Active)
                    {
                        color = Plugin.Configuration.ActivatedColor.ToByteColor().RGBA;
                    }
                    else
                    {
                        switch (trigger)
                        {
                            case Start:
                                color = Plugin.Configuration.StartTriggerColor.ToByteColor().RGBA;
                                break;
                            case Loop:
                                color = Plugin.Configuration.StartTriggerColor.ToByteColor().RGBA;
                                break;
                            case Checkpoint:
                                color = Plugin.Configuration.CheckpointTriggerColor.ToByteColor().RGBA;
                                break;
                            case Fail:
                                color = Plugin.Configuration.FailTriggerColor.ToByteColor().RGBA;
                                break;
                            case Finish:
                                color = Plugin.Configuration.FinishTriggerColor.ToByteColor().RGBA;
                                break;
                        }
                    }

                    draw.DrawCubeFilled(trigger.Cube, color, 5.0f);
                }
            }

            // Display player racing lines
            if (Plugin.Configuration.DrawRacingLines)
            {
                foreach (var actor in Plugin.trackedPlayers.Values)
                {
                    // Use the stable line drawing that never changes
                    TimedVector3[] raceLine = actor.GetLineForDrawing();

                    selectedStyle.Draw(
                        raceLine,
                        Plugin.Configuration.LineColor.ToByteColor().RGBA,
                        draw
                    );
                }
            }

            // Draw the selected Record's line
            if (Plugin.DisplayedRecord != null)
            {
                Record displayedRecord = Plugin.DisplayedRecord;
                TimedVector3[] displayedRecordLine = displayedRecord.Line;

                // Resize the array so we only supply the line up till the time offset we want
                if (Plugin.LocalTimer.IsRunning)
                {
                    int maxIndex = Array.FindIndex(
                        displayedRecordLine,
                        x => x.Offset >= Plugin.LocalTimer.ElapsedMilliseconds
                    );
                    if (maxIndex > 0)
                    {
                        Array.Resize(ref displayedRecordLine, maxIndex - 1);
                    }
                }

                selectedStyle.Draw(
                    displayedRecordLine,
                    Plugin.Configuration.HighlightedLineColor.ToByteColor().RGBA,
                    draw
                );
            }
        }
    }
}
