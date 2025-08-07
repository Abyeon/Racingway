using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using LiteDB;
using Racingway.Race;
using Racingway.Race.Collision;
using Racingway.Race.Collision.Triggers;

namespace Racingway.Utils.Storage
{
    internal class LocalDatabase : IDisposable
    {
        private Plugin Plugin { get; set; }
        private LiteDatabase Database { get; init; }
        private SemaphoreSlim dbLock = new SemaphoreSlim(1, 1);
        private TaskCompletionSource<bool>? pendingWrites = null;
        private readonly TimeSpan debounceTime = TimeSpan.FromMilliseconds(1000);
        private CancellationTokenSource cancellationTokenSource;

        private const string RecordTable = "record";
        private const string RouteTable = "route";

        private string dbPath = string.Empty;

        public Dictionary<string, Route> RouteCache = new Dictionary<string, Route>();

        internal LocalDatabase(Plugin plugin, string path)
        {
            Plugin = plugin;
            Database = new LiteDatabase($"filename={path};upgrade=true");
            cancellationTokenSource = new CancellationTokenSource();

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
                BsonMapper.Global.RegisterType(
                    serialize: (vector) =>
                        new BsonArray(vector.Select(x => new BsonValue(x.ToString()))),
                    deserialize: (bson) =>
                    {
                        var values = bson.AsArray.Select(x => x.ToString()).ToArray();
                        var vectors = values
                            .Select(v =>
                            {
                                var trimmed = v.Trim().Substring(2, v.Length - 4);
                                var values = trimmed
                                    .Trim()
                                    .Split(',')
                                    .Select(x => float.Parse(x))
                                    .ToArray();
                                return new Vector3(values[0], values[1], values[2]);
                            })
                            .ToArray();

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
                        bson["readableName"]
                    );

                    return newAddress;
                }
            );

            BsonMapper.Global.RegisterType<Route>(
                serialize: (route) => route.GetSerialized(),
                deserialize: (bson) =>
                {
                    try
                    {
                        var address = BsonMapper.Global.Deserialize<Address>(bson["address"]);
                        var newRoute = new Route(
                            bson["name"],
                            address,
                            bson["description"],
                            new(),
                            new(),
                            bson["allowMounts"],
                            bson["enabled"],
                            bson["clientFails"],
                            bson["clientFinishes"]
                        );

                        try
                        {
                            var records = BsonMapper.Global.Deserialize<List<Record>>(
                                bson["records"]
                            );
                            newRoute.Records = records;
                            newRoute.Records.Sort((a, b) => a.Time.CompareTo(b.Time));
                        }
                        catch (Exception e)
                        {
                            e.ToString();
                        }

                        // Load behaviour bools if they exist
                        if (bson.AsDocument.ContainsKey("requireGroundedStart"))
                        {
                            newRoute.RequireGroundedStart = bson["requireGroundedStart"];
                            newRoute.RequireGroundedFinish = bson["requireGroundedFinish"];
                        }

                        if (bson.AsDocument.ContainsKey("requireGroundedCheckpoint"))
                        {
                            newRoute.RequireGroundedCheckpoint = bson["requireGroundedCheckpoint"];

                            if (bson.AsDocument.ContainsKey("requireAllCheckpoints"))
                                newRoute.RequireAllCheckpoints = bson["requireAllCheckpoints"];
                        }

                        if (bson.AsDocument.ContainsKey("laps"))
                        {
                            newRoute.Laps = bson["laps"];
                        }

                        // Load route cleanup settings if they exist
                        if (bson.AsDocument.ContainsKey("autoCleanupEnabled"))
                        {
                            newRoute.AutoCleanupEnabled = bson["autoCleanupEnabled"];
                            newRoute.MaxRecordsToKeep = bson["maxRecordsToKeep"];
                            newRoute.KeepTopNRecords = bson["keepTopNRecords"];

                            // Check for the DeleteOldRecordsEnabled setting
                            if (bson.AsDocument.ContainsKey("deleteOldRecordsEnabled"))
                            {
                                newRoute.DeleteOldRecordsEnabled = bson["deleteOldRecordsEnabled"];
                            }

                            newRoute.MaxDaysToKeep = bson["maxDaysToKeep"];
                            newRoute.KeepPersonalBests = bson["keepPersonalBests"];

                            // Load time threshold settings if they exist
                            if (bson.AsDocument.ContainsKey("filterByTimeEnabled"))
                            {
                                newRoute.FilterByTimeEnabled = bson["filterByTimeEnabled"];
                                newRoute.MinTimeThreshold = (float)(double)bson["minTimeThreshold"];
                                newRoute.MaxTimeThreshold = (float)(double)bson["maxTimeThreshold"];
                            }
                        }

                        var arrayOfTriggers = (BsonArray)bson["triggers"];
                        foreach (var trigger in arrayOfTriggers)
                        {
                            var cubeArray = (BsonArray)trigger["Cube"];
                            string type = trigger["Type"];
                            var cube = new Cube(
                                new Vector3(
                                    float.Parse(cubeArray[0]),
                                    float.Parse(cubeArray[1]),
                                    float.Parse(cubeArray[2])
                                ),
                                new Vector3(
                                    float.Parse(cubeArray[3]),
                                    float.Parse(cubeArray[4]),
                                    float.Parse(cubeArray[5])
                                ),
                                new Vector3(
                                    float.Parse(cubeArray[6]),
                                    float.Parse(cubeArray[7]),
                                    float.Parse(cubeArray[8])
                                )
                            );

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
                                case "Loop":
                                    newRoute.Triggers.Add(new Loop(newRoute, cube));
                                    break;
                                default:
                                    throw new Exception(
                                        "Attempted to add a trigger type that does not exist!"
                                    );
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
            cancellationTokenSource.Cancel();
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

        /// <summary>
        /// Adds a record to the database with automatic cleanup if enabled
        /// </summary>
        internal async Task AddRecord(Record record)
        {
            // Ensure the record's line is simplified before saving
            await record.EnsureLineSimplified();

            await WriteToDatabase(() =>
            {
                var result = GetRecords().Insert(record);

                // Apply auto-cleanup for the route if enabled
                if (
                    RouteCache.TryGetValue(record.RouteId, out Route route)
                    && route.AutoCleanupEnabled
                )
                {
                    route.ApplyCleanupRules();

                    // Update the route with cleaned records
                    GetRoutes().Update(route);
                }

                return result;
            });
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
                if (!routes.Contains(route))
                    RouteCache.Remove(route.Id.ToString());
            }
        }

        /// <summary>
        /// Adds or updates a route with debounced writes to prevent performance impact
        /// </summary>
        internal async Task AddRoute(Route route)
        {
            // Ensure all records have simplified lines before saving
            if (route.Records != null && route.Records.Count > 0)
            {
                var simplificationTasks = route
                    .Records.Select(r => r.EnsureLineSimplified())
                    .ToArray();
                await Task.WhenAll(simplificationTasks);
            }

            await DebouncedWriteToDatabase(() =>
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
            await Task.Run(() =>
            {
                try
                {
                    string normalized = data.Normalize();
                    string Json = Compression.FromCompressedBase64(normalized);

                    BsonValue bson = JsonSerializer.Deserialize(Json);
                    Route route = BsonMapper.Global.Deserialize<Route>(bson);

                    // If the route is somehow null, lets log the JSON.
                    if (route == null)
                    {
                        Plugin.Log.Warning(
                            "Imported route was null, printing the uncompressed Base64... "
                        );
                        Plugin.Log.Warning(Json);
                        throw new NullReferenceException("Route is null. Check /xllog.");
                    }

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
            });
        }

        internal void ImportRecord(Record record)
        {
            try
            {
                bool hasRoute = RouteCache.ContainsKey(record.RouteId.ToString());

                if (hasRoute)
                {
                    //var route1 = GetRoutes().Find(x => x.Id.ToString() == record.RouteId).FirstOrDefault();
                    var route = RouteCache[record.RouteId];
                    var records = route.Records;
                    string hash = route.GetHash();

                    // Check if route matches record's saved hash
                    if (hash != record.RouteHash)
                    {
                        Plugin.Log.Error(hash + " != " + record.RouteHash);
                        throw new Exception(
                            "Saved version of route may not match the one this record was made in."
                        );
                    }

                    // Incredibly stupid way to check if a duplicate record exists.. Because my LiteDB implementation was flawed from the start! I might burn it all down..
                    if (
                        !records.Exists(r =>
                            r.Name == record.Name
                            && r.World == record.World
                            && r.Time == record.Time
                        )
                    )
                    {
                        route.Records.Add(record);
                        Plugin.AddRoute(route);
                        return;
                    }
                    else
                    {
                        throw new Exception("Route already contains this record.");
                    }
                }
                else
                {
                    throw new Exception("Route that record was intended for does not exist.");
                }
            }
            catch (Exception ex)
            {
                Plugin.ChatGui.PrintError($"[RACE] Failed to import record. {ex.Message}");
                Plugin.Log.Error(ex, "Failed to import record");
            }
        }

        internal async Task ImportRecordFromBase64(string data)
        {
            try
            {
                var Json = Compression.FromCompressedBase64(data);

                var bson = JsonSerializer.Deserialize(Json);
                var record = BsonMapper.Global.Deserialize<Record>(bson);

                bool hasRoute = RouteCache.ContainsKey(record.RouteId.ToString());

                if (hasRoute)
                {
                    //var route1 = GetRoutes().Find(x => x.Id.ToString() == record.RouteId).FirstOrDefault();
                    var route = RouteCache[record.RouteId];
                    var records = route.Records;
                    string hash = route.GetHash();

                    // Check if route matches record's saved hash
                    if (hash != record.RouteHash)
                    {
                        Plugin.Log.Error(hash + " != " + record.RouteHash);
                        throw new Exception(
                            "Saved version of route may not match the one this record was made in."
                        );
                    }

                    // Incredibly stupid way to check if a duplicate record exists.. Because my LiteDB implementation was flawed from the start! I might burn it all down..
                    if (
                        !records.Exists(r =>
                            r.Name == record.Name
                            && r.World == record.World
                            && r.Time == record.Time
                        )
                    )
                    {
                        route.Records.Add(record);
                        await AddRoute(route);
                        return;
                    }
                    else
                    {
                        throw new Exception("Route already contains this record.");
                    }
                }
                else
                {
                    throw new Exception("Route that record was intended for does not exist.");
                }
            }
            catch (Exception ex)
            {
                Plugin.ChatGui.PrintError($"[RACE] Failed to import record. {ex.Message}");
                Plugin.Log.Error(ex, "Failed to import record");
            }
        }

        /// <summary>
        /// Execute a database write operation with a semaphore to prevent concurrent access
        /// </summary>
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

        /// <summary>
        /// Debounced database write to reduce FPS impact when finishing a race
        /// </summary>
        private async Task DebouncedWriteToDatabase(Func<object> action)
        {
            if (pendingWrites != null)
            {
                pendingWrites.TrySetResult(true);
            }

            pendingWrites = new TaskCompletionSource<bool>();
            var currentPendingWrites = pendingWrites;

            try
            {
                using var cancellationTokenRegistration = cancellationTokenSource.Token.Register(
                    () => currentPendingWrites.TrySetCanceled()
                );

                // Wait for debounce period
                var delayTask = Task.Delay(debounceTime, cancellationTokenSource.Token);
                var completedTask = await Task.WhenAny(delayTask, currentPendingWrites.Task);

                // If the task was canceled or there's a new pending write, skip this one
                if (
                    completedTask == currentPendingWrites.Task
                    || cancellationTokenSource.Token.IsCancellationRequested
                )
                {
                    return;
                }

                // Execute the write operation with the semaphore on a background thread
                // to avoid blocking the main thread
                await Task.Run(async () =>
                {
                    try
                    {
                        await WriteToDatabase(action);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error($"Error in background database write: {ex}");
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // Handle cancellation
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error in debounced database write: {ex}");
            }
        }

        /// <summary>
        /// Runs auto-cleanup on all routes that have cleanup enabled
        /// </summary>
        internal async Task RunRoutesAutoCleanup()
        {
            await WriteToDatabase(() =>
            {
                int totalRecordsRemoved = 0;

                foreach (var route in RouteCache.Values)
                {
                    if (route.AutoCleanupEnabled)
                    {
                        int recordsRemoved = route.ApplyCleanupRules();
                        if (recordsRemoved > 0)
                        {
                            GetRoutes().Update(route);
                            totalRecordsRemoved += recordsRemoved;
                        }
                    }
                }

                if (totalRecordsRemoved > 0)
                {
                    Plugin.ChatGui.Print(
                        $"[RACE] Auto-cleanup removed {totalRecordsRemoved} records from the database."
                    );
                }

                return totalRecordsRemoved;
            });
        }

        /// <summary>
        /// Get the current size of the database file
        /// </summary>
        public string GetFileSizeString()
        {
            if (string.IsNullOrEmpty(dbPath))
                return "Unknown";

            try
            {
                var info = new FileInfo(dbPath);
                return SizeSuffix(info.Length);
            }
            catch
            {
                return "Error";
            }
        }

        // Size suffix helpers
        static readonly string[] SizeSuffixes =
        {
            "bytes",
            "KB",
            "MB",
            "GB",
            "TB",
            "PB",
            "EB",
            "ZB",
            "YB",
        };

        static string SizeSuffix(long value, int decimalPlaces = 1)
        {
            if (value < 0)
            {
                return "-" + SizeSuffix(-value, decimalPlaces);
            }

            int i = 0;
            decimal dValue = value;
            while (Math.Round(dValue, decimalPlaces) >= 1000)
            {
                dValue /= 1024;
                i++;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}", dValue, SizeSuffixes[i]);
        }
    }
}
