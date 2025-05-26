using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Racingway.Utils
{
    public static unsafe class Compression
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
            catch (Exception ex)
            {
                Plugin.Log.Error(ex.ToString());
                return string.Empty;
            }
        }

        public static unsafe string ToCompressedBase64(string data) {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                using var compressedStream = new MemoryStream();

                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    zipStream.Write(bytes, 0, bytes.Length);
                }

                string output = Convert.ToBase64String(compressedStream.ToArray());

                Plugin.Log.Debug($"Compressed {data.Length} characters down to {output.Length} characters");

                return output;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex.ToString());
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

        public static byte[]? ByteFromBase64(string compressedBase64)
        {
            try
            {
                var data = Convert.FromBase64String(compressedBase64);
                using var compressedStream = new MemoryStream(data);
                using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                using var resultStream = new MemoryStream();

                zipStream.CopyTo(resultStream);

                return resultStream.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public static unsafe string ToCompressedString(String data)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(data);
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

        public static byte[] Vector3ToByteArray(Vector3 vector)
        {
            byte[] buff = new byte[sizeof(float) * 3];
            Buffer.BlockCopy(BitConverter.GetBytes(vector.X), 0, buff, 0*sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(vector.Y), 0, buff, 1*sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(vector.Z), 0, buff, 2*sizeof(float), sizeof(float));

            return buff;
        }

        public static Vector3 Vector3FromByteArray(byte[] data)
        {
            byte[] buff = data;
            Vector3 vector = new Vector3();
            vector.X = BitConverter.ToSingle(buff, 0*sizeof(float));
            vector.Y = BitConverter.ToSingle(buff, 1*sizeof(float));
            vector.Z = BitConverter.ToSingle(buff, 2*sizeof(float));

            return vector;
        }
    }
}
