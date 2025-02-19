using LiteDB;
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
