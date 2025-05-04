using ImGuiNET;
using LiteDB;
using Racingway.Race;
using Racingway.Race.Collision;
using Racingway.Race.Collision.Triggers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< Updated upstream
=======
using System.Timers;
using ImGuiNET;
using LiteDB;
using Racingway.Race;
using Racingway.Race.Collision;
using Racingway.Race.Collision.Triggers;
>>>>>>> Stashed changes

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

        private ConcurrentQueue<(Func<object>, TaskCompletionSource<object>)> _writeQueue =
            new ConcurrentQueue<(Func<object>, TaskCompletionSource<object>)>();
        private readonly int _maxBatchSize = 10;
        private bool _processingQueue = false;
        private readonly System.Timers.Timer _flushTimer;

        private readonly Dictionary<string, List<Record>> _recordCache =
            new Dictionary<string, List<Record>>();
        private bool _recordsCached = false;

        internal LocalDatabase(Plugin plugin, string path)
        {
            Plugin = plugin;

            // Optimize connection parameters for performance
            // - journal=false: Disables journal for better write performance (at the cost of some crash recovery)
            // - cache size=10000: Increases cache size for better read performance
            // - connection=shared: Uses shared connections for better concurrency
            // - flush=false: Reduces immediate disk writes for better performance
            Database = new LiteDatabase(
                $"filename={path};journal=false;cache size=20000;connection=shared;upgrade=true;flush=false"
            );

            dbPath = path;

            _flushTimer = new System.Timers.Timer(500);
            _flushTimer.Elapsed += async (s, e) => await FlushQueueAsync();
            _flushTimer.Start();

            var recordCollection = GetRecords();
            recordCollection.EnsureIndex(r => r.Name);
            recordCollection.EnsureIndex(r => r.World);
            recordCollection.EnsureIndex(r => r.Date);
            recordCollection.EnsureIndex(r => r.Time);
            recordCollection.EnsureIndex(r => r.Distance);
            recordCollection.EnsureIndex(r => r.RouteId);
            recordCollection.EnsureIndex(r => r.IsClient);
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
            _flushTimer.Stop();
            _flushTimer.Dispose();

            // Make sure we flush any pending changes
            FlushQueueAsync().GetAwaiter().GetResult();

            // Clean up resources
            _recordCache.Clear();
            RouteCache.Clear();

            // Compact database before closing
            try
            {
                Database.Rebuild();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error compacting database");
            }

            Database.Dispose();
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
            // Add to cache immediately
            lock (_recordCache)
            {
                if (!_recordCache.ContainsKey(record.RouteId))
                {
                    _recordCache[record.RouteId] = new List<Record>();
                }

                _recordCache[record.RouteId].Add(record);
            }

            // Queue database write
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
                string normalized = data.Normalize();
                string Json = Compression.FromCompressedBase64(normalized);

                BsonValue bson = JsonSerializer.Deserialize(Json);
                Route route = BsonMapper.Global.Deserialize<Route>(bson);

                // If the route is somehow null, lets log the JSON.
                if (route == null)
                {
                    Plugin.Log.Warning("Imported route was null, printing the uncompressed Base64... ");
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
                        throw new Exception("Saved version of route may not match the one this record was made in.");
                    }

                    // Incredibly stupid way to check if a duplicate record exists.. Because my LiteDB implementation was flawed from the start! I might burn it all down..
                    if (!records.Exists(r => r.Name == record.Name && r.World == record.World && r.Time == record.Time))
                    {
                        route.Records.Add(record);
                        await AddRoute(route);
                        return;
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

        internal async Task WriteToDatabase(Func<object> action)
        {
            var tcs = new TaskCompletionSource<object>();
            _writeQueue.Enqueue((action, tcs));

            // Start processing if not already in progress
            if (!_processingQueue)
            {
                _ = Task.Run(ProcessQueueAsync);
            }

            await tcs.Task;
        }

        private async Task ProcessQueueAsync()
        {
            if (_processingQueue)
                return;

            try
            {
                _processingQueue = true;
                await FlushQueueAsync();
            }
            finally
            {
                _processingQueue = false;
            }
        }

        private async Task FlushQueueAsync()
        {
            if (_writeQueue.IsEmpty)
                return;

            // Create a batch of operations
            var batch = new List<(Func<object>, TaskCompletionSource<object>)>();

            // Dequeue items up to the batch size
            while (batch.Count < _maxBatchSize && _writeQueue.TryDequeue(out var item))
            {
                batch.Add(item);
            }

            if (batch.Count == 0)
                return;

            // Execute the batch in a single database operation
            try
            {
                await dbLock.WaitAsync();

                // Process each operation
                foreach (var (action, tcs) in batch)
                {
                    try
                    {
                        var result = action.Invoke();
                        tcs.SetResult(result ?? new object());
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }
            }
            finally
            {
                dbLock.Release();

                // If there are more items in the queue, process them
                if (!_writeQueue.IsEmpty)
                {
                    _ = Task.Run(ProcessQueueAsync);
                }
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

        private void EnsureRecordsCached()
        {
            if (_recordsCached)
                return;

            lock (_recordCache)
            {
                if (_recordsCached)
                    return;

                var allRecords = GetRecords().FindAll().ToList();
                foreach (var record in allRecords)
                {
                    if (!_recordCache.ContainsKey(record.RouteId))
                    {
                        _recordCache[record.RouteId] = new List<Record>();
                    }

                    _recordCache[record.RouteId].Add(record);
                }

                _recordsCached = true;
            }
        }

        internal List<Record> GetRecordsForRoute(string routeId)
        {
            EnsureRecordsCached();

            lock (_recordCache)
            {
                if (_recordCache.ContainsKey(routeId))
                {
                    return _recordCache[routeId].ToList();
                }
            }

            return new List<Record>();
        }

        // Update CompactDatabase method to be more aggressive with cleanup
        internal async Task CompactDatabase()
        {
            await WriteToDatabase(() =>
            {
                try
                {
                    // First ensure we're not holding any connections
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    // Rebuild the database to reclaim space
                    Database.Rebuild();

                    // Run a vacuum operation to reclaim space
                    Database.Execute("VACUUM");

                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Error compacting database");
                    return false;
                }
            });
        }

        // Add this method to handle record cleanup
        internal async Task CleanupRecords(
            float minTimeFilter,
            int maxRecordsPerRoute,
            bool removeNonClientRecords,
            bool keepPersonalBestOnly
        )
        {
            await WriteToDatabase(() =>
            {
                try
                {
                    // Get all routes
                    var routes = GetRoutes().FindAll().ToList();
                    int totalRecordsBefore = 0;
                    int totalRecordsAfter = 0;
                    int routesProcessed = 0;

                    foreach (var route in routes)
                    {
                        if (route.Records == null || route.Records.Count == 0)
                            continue;

                        // Check if this route has its own cleanup settings enabled
                        bool useRouteSettings = route.EnableAutoCleanup;
                        float routeMinTimeFilter = useRouteSettings
                            ? route.MinTimeFilter
                            : minTimeFilter;
                        int routeMaxRecords = useRouteSettings
                            ? route.MaxRecordsToKeep
                            : maxRecordsPerRoute;
                        bool routeRemoveNonClient = useRouteSettings
                            ? route.RemoveNonClientRecords
                            : removeNonClientRecords;
                        bool routeKeepPersonalBest = useRouteSettings
                            ? route.KeepPersonalBestOnly
                            : keepPersonalBestOnly;

                        // Skip this route if neither global nor route-specific auto-cleanup is enabled
                        if (!Plugin.Configuration.EnableAutoCleanup && !route.EnableAutoCleanup)
                            continue;

                        routesProcessed++;
                        totalRecordsBefore += route.Records.Count;

                        // Keep a working copy of the records for filtering
                        var records = new List<Record>(route.Records);

                        // 1. Apply time filter (remove records with completion time less than minTimeFilter)
                        if (routeMinTimeFilter > 0)
                        {
                            records = records
                                .Where(r => r.Time.TotalSeconds >= routeMinTimeFilter)
                                .ToList();
                        }

                        // 2. Apply client-only filter
                        if (routeRemoveNonClient)
                        {
                            records = records.Where(r => r.IsClient).ToList();
                        }

                        // 3. Apply personal best only filter (only keep the best time per player)
                        if (routeKeepPersonalBest)
                        {
                            // Group by player name and keep only the fastest record per player
                            records = records
                                .GroupBy(r => r.Name)
                                .Select(g => g.OrderBy(r => r.Time.TotalMilliseconds).First())
                                .ToList();
                        }

                        // 4. Apply max records per route filter
                        if (routeMaxRecords > 0 && records.Count > routeMaxRecords)
                        {
                            // Sort by time and keep only the best records
                            records = records
                                .OrderBy(r => r.Time.TotalMilliseconds)
                                .Take(routeMaxRecords)
                                .ToList();
                        }

                        // Update the route with the filtered records
                        route.Records = records;
                        totalRecordsAfter += records.Count;

                        // Update route in database
                        GetRoutes().Update(route);

                        // Update route cache
                        if (RouteCache.ContainsKey(route.Id.ToString()))
                        {
                            RouteCache[route.Id.ToString()] = route;
                        }
                    }

                    // Clear record cache to force refresh
                    _recordCache.Clear();
                    _recordsCached = false;

                    // Log results
                    int recordsRemoved = totalRecordsBefore - totalRecordsAfter;
                    Plugin.Log.Information(
                        $"Records cleanup complete: {recordsRemoved} records removed. Routes processed: {routesProcessed}, Records remaining: {totalRecordsAfter}"
                    );

                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Error during records cleanup");
                    return false;
                }
            });
        }

        // Add this method to handle route operations with better error handling and performance
        internal async Task OptimizedDatabaseOperation(Action operation, string errorMessage)
        {
            await WriteToDatabase(() =>
            {
                try
                {
                    operation();
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, errorMessage);
                    return false;
                }
            });
        }
    }
}
