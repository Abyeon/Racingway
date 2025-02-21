using LiteDB;
using Racingway.Race;
using Racingway.Race.Collision;
using Racingway.Race.Collision.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    internal class LocalDatabase : IDisposable
    {
        private Plugin Plugin { get; set; }
        private LiteDatabase Database { get; init; }
        private SemaphoreSlim dbLock = new SemaphoreSlim(1, 1);

        private const string RecordTable = "record";
        private const string RouteTable = "route";

        internal LocalDatabase(Plugin plugin, string path)
        {
            this.Plugin = plugin;
            this.Database = new LiteDatabase(path);

            var recordCollection = GetRecords();
            recordCollection.EnsureIndex(r => r.Name);
            recordCollection.EnsureIndex(r => r.World);
            recordCollection.EnsureIndex(r => r.Date);
            recordCollection.EnsureIndex(r => r.Time);
            recordCollection.EnsureIndex(r => r.Distance);
            recordCollection.EnsureIndex(r => r.Line);

            // Incoming Lamda Hell
            try
            {
                BsonMapper.Global.RegisterType<Vector3[]>
                (
                    serialize: (vector) => new BsonArray(vector.Select(x => new BsonValue(x.ToString()))),
                    deserialize: (bson) => {
                        string[] values = bson.AsArray.Select(x => x.ToString()).ToArray();
                        Vector3[] vectors = values.Select(v =>
                        {
                            string trimmed = v.Trim().Substring(2, v.Length - 4);
                            float[] values = trimmed.Trim().Split(',').Select(x => float.Parse(x)).ToArray();
                            return new Vector3(values[0], values[1], values[2]);
                        }).ToArray();

                        return vectors;
                    }
                );
            } catch (Exception ex)
            {
                Plugin.Log.Error(ex.ToString());
            }

            var routeCollection = GetRoutes();
            routeCollection.EnsureIndex(r => r.Name);
            routeCollection.EnsureIndex(r => r.Address);
            routeCollection.EnsureIndex(r => r.Triggers);

            try
            {
                BsonMapper.Global.RegisterType<Route>
                (
                    serialize: (route) => route.GetSerialized(),
                    deserialize: (bson) =>
                    {
                        Route newRoute = new Route(bson["name"], bson["address"], new());

                        BsonArray arrayOfTriggers = (BsonArray)bson["triggers"];
                        foreach (var trigger in arrayOfTriggers)
                        {
                            BsonArray cubeArray = (BsonArray)trigger["Cube"];
                            string type = trigger["Type"];
                            Cube cube = new Cube(
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
                );
            } catch (Exception ex)
            {
                Plugin.Log.Error(ex.ToString());
            }
        }

        public void Dispose()
        {
            Database.Dispose();
        }

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

        internal async Task AddRoute(Route route)
        {
            await WriteToDatabase(() => {
                if (!GetRoutes().Update(route))
                {
                    return GetRoutes().Insert(route);
                } else
                {
                    return true;
                }
            });
        }

        private async Task WriteToDatabase(Func<object> action)
        {
            try
            {
                await dbLock.WaitAsync();
                action.Invoke();
            } finally
            {
                dbLock.Release();
            }
        }
    }
}
