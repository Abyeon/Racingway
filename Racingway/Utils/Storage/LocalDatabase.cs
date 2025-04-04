using ImGuiNET;
using LiteDB;
using Racingway.Race;
using Racingway.Race.Collision;
using Racingway.Race.Collision.Triggers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Racingway.Utils.Storage
{
    internal class LocalDatabase : IDisposable
    {
        private Plugin Plugin { get; set; }
        private LiteDatabase Database { get; init; }
        private SemaphoreSlim dbLock = new SemaphoreSlim(1, 1);

        private const string RecordTable = "record";
        private const string RouteTable = "route";

        private string dbPath = string.Empty;

        public Dictionary<string, Route> RouteCache = new Dictionary<string, Route>();

        internal LocalDatabase(Plugin plugin, string path)
        {
            Plugin = plugin;
            Database = new LiteDatabase($"filename={path};upgrade=true");

            dbPath = path;

            var recordCollection = GetRecords();
            recordCollection.EnsureIndex(r => r.Name);
            recordCollection.EnsureIndex(r => r.World);
            recordCollection.EnsureIndex(r => r.Date);
            recordCollection.EnsureIndex(r => r.Time);
            recordCollection.EnsureIndex(r => r.Distance);
            //recordCollection.EnsureIndex(r => r.Line);

            try
            {
                BsonMapper.Global.RegisterType
                (
                    serialize: (vector) => new BsonArray(vector.Select(x => new BsonValue(x.ToString()))),
                    deserialize: (bson) =>
                    {
                        var values = bson.AsArray.Select(x => x.ToString()).ToArray();
                        var vectors = values.Select(v =>
                        {
                            var trimmed = v.Trim().Substring(2, v.Length - 4);
                            var values = trimmed.Trim().Split(',').Select(x => float.Parse(x)).ToArray();
                            return new Vector3(values[0], values[1], values[2]);
                        }).ToArray();

                        return vectors;
                    }
                );
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex.ToString());
            }

            var routeCollection = GetRoutes();
            routeCollection.EnsureIndex(r => r.Name);
            //routeCollection.EnsureIndex(r => r.LocationId);
            routeCollection.EnsureIndex(r => r.Triggers);
            routeCollection.EnsureIndex(r => r.Address);

            BsonMapper.Global.RegisterType(
                serialize: (address) => address.GetSerialized(),
                deserialize: (bson) =>
                {
                    var newAddress = new Address(
                        uint.Parse(bson["territoryId"]),
                        uint.Parse(bson["mapId"]),
                        bson["locationId"],
                        bson["readableName"]);

                    return newAddress;
                }
            );

            BsonMapper.Global.RegisterType<Route>
            (
                serialize: (route) => route.GetSerialized(),
                deserialize: (bson) =>
                {
                    try
                    {
                        var address = BsonMapper.Global.Deserialize<Address>(bson["address"]);
                        var newRoute = new Route(bson["name"], address, bson["description"], new(), new(), bson["allowMounts"], bson["enabled"], bson["clientFails"], bson["clientFinishes"]);

                        try
                        {
                            var records = BsonMapper.Global.Deserialize<List<Record>>(bson["records"]);
                            newRoute.Records = records;
                            newRoute.Records.Sort((a, b) => a.Time.CompareTo(b.Time));
                        }
                        catch (Exception e)
                        {
                            e.ToString();
                        }

                        //newRoute.AllowMounts = bson["allowMounts"];
                        //newRoute.Enabled = bson["enabled"];

                        var arrayOfTriggers = (BsonArray)bson["triggers"];
                        foreach (var trigger in arrayOfTriggers)
                        {
                            var cubeArray = (BsonArray)trigger["Cube"];
                            string type = trigger["Type"];
                            var cube = new Cube(
                                new Vector3(float.Parse(cubeArray[0]), float.Parse(cubeArray[1]), float.Parse(cubeArray[2])),
                                new Vector3(float.Parse(cubeArray[3]), float.Parse(cubeArray[4]), float.Parse(cubeArray[5])),
                                new Vector3(float.Parse(cubeArray[6]), float.Parse(cubeArray[7]), float.Parse(cubeArray[8])));

                            switch (type)
                            {
                                case "Start":
                                    newRoute.Triggers.Add(new Start(newRoute, cube));
                                    break;
                                case "Checkpoint":
                                    newRoute.Triggers.Add(new Checkpoint(newRoute, cube));
                                    break;
                                case "Fail":
                                    newRoute.Triggers.Add(new Fail(newRoute, cube));
                                    break;
                                case "Finish":
                                    newRoute.Triggers.Add(new Finish(newRoute, cube));
                                    break;
                                default:
                                    throw new Exception("Attempted to add a trigger type that does not exist!");
                            }
                        }

                        newRoute.Id = bson["_id"];

                        return newRoute;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.Error(e.ToString());
                    }

                    return null;
                }
            );
        }

        public void Dispose()
        {
            Database.Dispose();
            RouteCache.Clear();
        }

        //// Making this to save people's legacy routes.
        //internal void ExportRoutesToFile(string Path)
        //{
        //    Database.Execute($"select $ into $file({Path}) from {RouteTable}");
        //}

        internal ILiteCollection<Record> GetRecords()
        {
            return Database.GetCollection<Record>(RecordTable);
        }

        internal async Task AddRecord(Record record)
        {
            await WriteToDatabase(() => GetRecords().Insert(record));
        }

        internal ILiteCollection<Route> GetRoutes()
        {
            return Database.GetCollection<Route>(RouteTable);
        }

        //private Record? GetBestRecord(Route route)
        //{
        //    var routeRecords = GetRecords().Query().Where(r => r.RouteId == route.Id.ToString()).ToList();

        //    if (routeRecords.Count > 0)
        //    {
        //        return routeRecords.OrderBy(r => r.Time.TotalNanoseconds).First();
        //    }

        //    return null;
        //}

        internal void UpdateRouteCache()
        {
            var routes = GetRoutes().Query().ToList();

            foreach (var route in routes)
            {
                if (route.Records == null)
                {
                    route.Records = new List<Record>();
                }

                // Get the best time for this record ???? This should never have even worked tbh.
                //var record = GetBestRecord(route);
                //if (record != null) route.BestRecord = record;

                if (RouteCache.ContainsKey(route.Id.ToString()))
                {
                    RouteCache[route.Id.ToString()] = route;
                }
                else
                {
                    RouteCache.Add(route.Id.ToString(), route);
                }
            }

            foreach (var route in RouteCache.Values)
            {
                if (!routes.Contains(route)) RouteCache.Remove(route.Id.ToString());
            }
        }

        internal async Task AddRoute(Route route)
        {
            await WriteToDatabase(() =>
            {
                if (!GetRoutes().Update(route))
                {
                    UpdateRouteCache();
                    return GetRoutes().Insert(route);
                }
                else
                {
                    UpdateRouteCache();
                    return true;
                }
            });
        }

        internal async Task ImportRouteFromBase64(string data)
        {
            try
            {
                var Json = Compression.FromCompressedBase64(data);

                var bson = JsonSerializer.Deserialize(Json);
                var route = BsonMapper.Global.Deserialize<Route>(bson);

                route.Records = new List<Record>();

                bool hasRoute = Plugin.Storage.RouteCache.ContainsKey(route.Id.ToString());

                if (!hasRoute)
                {
                    Plugin.AddRoute(route);
                    Plugin.ChatGui.Print($"[RACE] Added {route.Name} to routes.");
                }
                else
                {
                    Plugin.ChatGui.PrintError("[RACE] Route already saved!");
                }
            }
            catch (Exception ex)
            {
                Plugin.ChatGui.PrintError($"[RACE] Failed to import route. {ex.Message}");
                Plugin.Log.Error(ex, "Failed to import route");
            }
        }

        internal async Task ImportRecordFromBase64(string data)
        {
            try
            {
                var Json = Compression.FromCompressedBase64(data);

                var bson = JsonSerializer.Deserialize(Json);
                var record = BsonMapper.Global.Deserialize<Record>(bson);

                bool hasRoute = Plugin.Storage.RouteCache.ContainsKey(record.RouteId.ToString());

                if (hasRoute)
                {
                    var route = Plugin.Storage.RouteCache[record.RouteId];
                    string hash = route.GetHash();

                    // Check if route matches record's saved hash
                    if (hash != record.RouteHash)
                    {
                        Plugin.Log.Error(hash + " != " + record.RouteHash);
                        throw new Exception("Saved version of route may not match the one this record was made in.");
                    }

                    if (!route.Records.Contains(record))
                    {
                        route.Records.Add(record);
                        AddRoute(route);
                    } else
                    {
                        throw new Exception("Route already contains this record.");
                    }
                } else
                {
                    throw new Exception("Route that record was intended for does not exist.");
                }
            } catch (Exception ex)
            {
                Plugin.ChatGui.PrintError($"[RACE] Failed to import record. {ex.Message}");
                Plugin.Log.Error(ex, "Failed to import record");
            }
        }

        private async Task WriteToDatabase(Func<object> action)
        {
            try
            {
                await dbLock.WaitAsync();
                action.Invoke();
            }
            finally
            {
                dbLock.Release();
            }
        }

        // Grabbed from https://stackoverflow.com/a/14488941
        static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        static string SizeSuffix(long value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            var mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            var adjustedSize = (decimal)value / (1L << mag * 10);

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }

        // Return the size of the db file in a string format
        public string GetFileSizeString()
        {
            var fi = new FileInfo(dbPath);

            if (fi.Exists)
            {
                return SizeSuffix(fi.Length);
            }

            return string.Empty;
        }
    }
}
