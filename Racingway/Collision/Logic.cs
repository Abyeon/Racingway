using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Racingway.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Racingway.Collision.Trigger;

namespace Racingway.Collision
{
    public class Logic
    {
        public Plugin Plugin { get; }

        public Logic(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void CheckCollision(Player player)
        {
            foreach (Trigger trigger in Plugin.Configuration.Triggers)
            {
                trigger.CheckCollision(player);
            }
        }

        public void OnEntered(object sender, Player player)
        {
            Trigger trigger = (Trigger)sender;

            var actor = (IPlayerCharacter)player.actor;
            var now = DateTime.UtcNow;

            switch (trigger.selectedType)
            {
                case TriggerType.Start:
                    if (!player.inParkour) break;

                    player.inParkour = false;
                    player.raceLine.Clear();
                    player.timer.Reset();

                    break;
                case TriggerType.Fail:
                    if (!player.inParkour) break;

                    // Player failed parkour
                    player.raceLine.Clear();
                    player.inParkour = false;

                    if (Plugin.Configuration.LogFails)
                    {
                        PayloadedChat(actor, " just failed the parkour.");
                    }

                    player.timer.Reset();
                    break;
                case TriggerType.Checkpoint:
                    // Player hit checkpoint
                    break;
                case TriggerType.Finish:
                    // Player finished parkour
                    if (player.inParkour)
                    {
                        player.inParkour = false;
                        player.AddPoint();

                        var elapsedTime = player.timer.ElapsedMilliseconds;
                        var t = TimeSpan.FromMilliseconds(elapsedTime);

                        var prettyPrint = Time.PrettyFormatTimeSpan(t);

                        var distance = player.GetDistanceTraveled();

                        player.timer.Stop();
                        player.timer.Reset();

                        try
                        {
                            Record record = new Record(now, actor.Name.ToString(), actor.HomeWorld.Value.Name.ToString(), t, distance, player.raceLine.ToArray());
                            Plugin.RecordList.Add(record);
                            Plugin.Storage.AddRecord(record);
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log.Error(ex.ToString());
                        }

                        if (Plugin.Configuration.LogFinish)
                        {
                            PayloadedChat(actor, $" just finished the parkour in {prettyPrint} and {distance} units.");
                        }
                    }
                    break;
            }
        }

        public void OnLeft(object sender, Player player)
        {
            Trigger trigger = (Trigger)sender;

            if (trigger.selectedType == TriggerType.Start)
            {
                // Player started parkour
                player.inParkour = true;
                player.raceLine.Clear();
                player.timer.Reset();
                player.timer.Start();
            }
        }

        public void PayloadedChat(IPlayerCharacter player, string message)
        {
            PlayerPayload payload = new PlayerPayload(player.Name.ToString(), player.HomeWorld.Value.RowId);
            TextPayload text = new TextPayload(message);
            SeString chat = new SeString(new Payload[] { payload, text });

            Plugin.ChatGui.Print(chat);
        }
    }
}
