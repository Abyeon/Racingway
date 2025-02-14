using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    public class Record
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public DateTime Date {  get; set; }
        public string Name { get; set; }
        public string World {  get; set; }
        public TimeSpan Time { get; set; }
        public float Distance { get; set; }
        public byte[][] Line {  get; set; }


        public Record(DateTime date, string name, string world, TimeSpan time, float distance, Vector3[] line)
        {
            Id = new();

            this.Date = date;
            this.Name = name;
            this.World = world;
            this.Time = time;
            this.Distance = distance;

            byte[][] bytes = new byte[line.Length][];

            for (int i = 0; i < line.Length; i++)
            {
                bytes[i] = Compression.Vector3ToByteArray(line[i]);
            }

            this.Line = bytes;
        }

        public Vector3[] GetLine()
        {
            Vector3[] temp = new Vector3[Line.Length];

            for (int i = 0; i < Line.Length; i++)
            {
                temp[i] = Compression.Vector3FromByteArray(Line[i]);
            }

            return temp;
        }

        public string GetCSV()
        {
            // TODO: Research line simplification algos, or some other way to compress player lines... They BIG!
            //string compressedLine = Compression.ToCompressedBase64(Line);

            return $"{Date.ToLocalTime().ToString("M/dd H:mm:ss")},{Name},{World},{Utils.Time.PrettyFormatTimeSpan(Time)},{Distance.ToString()}\n";
        }
    }
}
