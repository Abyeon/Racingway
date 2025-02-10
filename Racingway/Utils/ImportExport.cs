using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    public static unsafe class ImportExport
    {
        public static unsafe string ToCompressedBase64<T>(T data)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data, Formatting.None);
                var bytes = Encoding.UTF8.GetBytes(json);
                using var compressedStream = new MemoryStream();
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress)) 
                {
                    zipStream.Write(bytes, 0, bytes.Length);
                }

                return Convert.ToBase64String(compressedStream.ToArray());
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string FromCompressedBase64(string compressedBase64)
        {
            try
            {
                var data = Convert.FromBase64String(compressedBase64);
                using var compressedStream = new MemoryStream(data);
                using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                using var resultStream = new MemoryStream();

                zipStream.CopyTo(resultStream);

                return Encoding.UTF8.GetString(resultStream.ToArray());
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
