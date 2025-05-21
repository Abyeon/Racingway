using ImGuiNET;
using LiteDB;
using Racingway.Race;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    public class ShareHelper
    {
        /// <summary>
        /// Get the serialized and compressed version of a route and put it in the user's clipboard.
        /// </summary>
        /// <param name="route"></param>
        public static void ExportRouteToClipboard(Route route)
        {
            string input = JsonSerializer.Serialize(route.GetEmptySerialized());
            string text = Compression.ToCompressedBase64(input);

            if (text != string.Empty)
            {
                ImGui.SetClipboardText(text);
            }
            else
            {
                Plugin.ChatGui.PrintError("[RACE] Error exporting route to clipboard.");
            }
        }

        /// <summary>
        /// Import a Route from the user's clipboard.
        /// </summary>
        /// <param name="plugin"></param>
        public static void ImportRouteFromClipboard(Plugin plugin)
        {
            string data = ImGui.GetClipboardText();

            plugin.DataQueue.QueueDataOperation(async () =>
            {
                await plugin.Storage.ImportRouteFromBase64(data);
            });
        }

        /// <summary>
        /// Export a record to the user's clipboard.
        /// </summary>
        /// <param name="record">The record to export</param>
        /// <param name="plugin"></param>
        public static void ExportRecordToClipboard(Record record, Plugin plugin)
        {
            Plugin.Log.Debug(
                $"{record.Name}: {Time.PrettyFormatTimeSpan(record.Time)}, {record.Distance}"
            );

            if (plugin.SelectedRoute == null)
            {
                Plugin.ChatGui.PrintError("[RACE] Selected route was null.");
                return;
            }

            // update route hash.. because old records probably have a broken hash.. oops!
            record.RouteHash = plugin
                .Storage.RouteCache[plugin.SelectedRoute.ToString()]
                .GetHash();

            var doc = BsonMapper.Global.ToDocument(record);
            string json = JsonSerializer.Serialize(doc);
            string text = Compression.ToCompressedBase64(json);

            if (text != string.Empty)
            {
                ImGui.SetClipboardText(text);
            }
            else
            {
                Plugin.ChatGui.PrintError(
                    "[RACE] Error exporting record to clipboard."
                );
            }
        }

        /// <summary>
        /// Import a record from the user's clipboard.
        /// </summary>
        /// <param name="plugin"></param>
        public static void ImportRecordFromClipboard(Plugin plugin)
        {
            string data = ImGui.GetClipboardText();

            plugin.DataQueue.QueueDataOperation(async () =>
            {
                try
                {
                    await plugin.Storage.ImportRecordFromBase64(data);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex.ToString());
                }
            });
        }
    }
}
