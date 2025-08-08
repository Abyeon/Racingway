using Dalamud.Bindings.ImGui;
using LiteDB;
using MessagePack;
using MessagePack.Resolvers;
using Newtonsoft.Json.Linq;
using Racingway.Race;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

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
        /// Get the serialized JSON version of a route and put it in the user's clipboard.
        /// </summary>
        /// <param name="route"></param>
        public static void ExportRouteJsonToClipboard(Route route, bool pretty = true)
        {
            string text = JsonSerializer.Serialize(route.GetEmptySerialized(), pretty);

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
        /// Use MessagePack to export a smaller version of the record to the user's clipboard.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="plugin"></param>
        public static void PackRecordToClipboard(Record record, Plugin plugin)
        {
            try
            {
                // Init compression options
                var lz4Options = MessagePackSerializerOptions.Standard
                    .WithResolver(StandardResolver.Instance)
                    .WithCompression(MessagePackCompression.Lz4BlockArray);

                // Serialize and convert to base64
                byte[] data = MessagePackSerializer.Serialize(record, lz4Options);
                string text = Convert.ToBase64String(data);

                Plugin.Log.Debug($"Packing record to {text.Length} characters.");

                // Throw the data to the clipboard.
                ImGui.SetClipboardText(text);
            }
            catch (Exception e)
            {
                Plugin.ChatGui.PrintError("[RACE] Error exporting record to clipboard.");
                Plugin.Log.Error(e.ToString());
            }
        }

        /// <summary>
        /// Use MessagePack to import user's clipboard data.
        /// </summary>
        /// <param name="plugin"></param>
        public static void ImportPackedRecord(Plugin plugin)
        {
            if (plugin.Storage == null) return;

            string data = ImGui.GetClipboardText();
            byte[] uncompressed = Convert.FromBase64String(data);

            plugin.DataQueue.QueueDataOperation(async () =>
            {
                try
                {
                    // Init compression options
                    var lz4Options = MessagePackSerializerOptions.Standard
                        .WithResolver(StandardResolver.Instance)
                        .WithCompression(MessagePackCompression.Lz4BlockArray);

                    Record record = MessagePackSerializer.Deserialize<Record>(uncompressed, lz4Options);
                    plugin.Storage.ImportRecord(record);
                } catch (Exception e)
                {
                    Plugin.ChatGui.PrintError("[RACE] Error importing record from clipboard.");
                    Plugin.Log.Error(e.ToString());
                }
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
            record.RouteHash = plugin.Storage.RouteCache[plugin.SelectedRoute.ToString()].GetHash();

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

        /// <summary>
        /// Try to import routes from a given URL
        /// </summary>
        /// <param name="url"></param>
        public static async void ImportRoutesFromURL(string url, Plugin plugin)
        {
            if (plugin.Storage == null) return;

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var json = await httpClient.GetStringAsync(url);
                    JToken[] array = JArray.Parse(json).Children().ToArray();
                    List<Route> routes = new List<Route>();

                    foreach (var jsonRoute in array)
                    {
                        string? name = jsonRoute["name"].Value<string>();
                        if (name != null)
                        {
                            Plugin.Log.Debug(name);
                        }

                        BsonValue bson = JsonSerializer.Deserialize(jsonRoute.ToString());
                        Route route = BsonMapper.Global.Deserialize<Route>(bson);

                        // If the route is null, lets log the JSON.
                        if (route == null)
                        {
                            throw new NullReferenceException("Route is null.");
                        } else
                        {
                            routes.Add(route);
                        }
                    }

                    plugin.AddRoutes(routes);
                }
            } catch (Exception ex)
            {
                Plugin.Log.Error(ex.ToString());
            }
        }
    }
}
