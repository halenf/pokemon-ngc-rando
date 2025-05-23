﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XDCommon.Utility
{
    public static class StreamExtensions
    {
        // helper methods to convert endianess
        public static byte[] GetBytes(this int value)
        {
            var valBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(valBytes);
            }
            return valBytes;
        }
        public static byte[] GetBytes(this uint value)
        {
            var valBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(valBytes);
            }
            return valBytes;
        }
        public static byte[] GetBytes(this ushort value)
        {
            var valBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(valBytes);
            }
            return valBytes;
        }

        public static int GetAlignBytesCount(this int size, int alignment)
        {
            var m = size % alignment;
            return m == 0 ? 0 : alignment - m;
        }

        public static uint GetAlignBytesCount(this uint size, int alignment)
        {
            var m = (uint)(size % alignment);
            return m == 0 ? 0 : (uint)alignment - m;
        }

        /// <summary>
        /// Get a new stream depending on what the value of Configuration.UseMemoryStreams is
        /// If true, use RAM to do processing, else write to disk.
        /// </summary>
        /// <remarks>
        /// Useful for extracting files too.
        /// </remarks>
        /// <param name="fullPath">Full path to file if above is false, else irrelevant</param>
        /// <returns>
        /// An empty open stream.
        /// </returns>
        public static Stream GetNewStream(this string fullPath)
        {
            if (Configuration.UseMemoryStreams)
            {
                return new MemoryStream();
            }
            else
            {
                var path = Path.GetDirectoryName(fullPath);
                if (path != string.Empty && !Directory.Exists(path))
                    Directory.CreateDirectory(path);

                // use create instead of open in cases where we crash and wrote junk the stream
                // if you didn't want this to happen why did you have files named exactly the same
                // and picked the same directory??
                return File.Open(fullPath, FileMode.Create, FileAccess.ReadWrite);
            }
        }

        // helper methods to get data types at offsets
        public static byte[] GetBytesAtOffset(this Stream stream, long offset, int length)
        {
            byte[] bytes = new byte[length];
            if (offset >= 0 && length + offset <= stream.Length)
            {
                stream.Seek(offset, SeekOrigin.Begin);
                var bytesRead = 0;
                while(bytesRead < length)
                {
                    bytesRead = stream.Read(bytes, bytesRead, length - bytesRead);
                }
            }

            if (BitConverter.IsLittleEndian && bytes.Length > 1)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }

        public static byte[] GetNibbleAtOffset(this Stream stream, long offset, int length)
        {
            byte[] bytes = new byte[length];
            if (offset > 0 && length + offset < stream.Length)
            {
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Read(bytes);
            }
            return bytes;
        }
        
        public static byte GetByteAtOffset(this Stream stream, long offset)
        {
            byte[] bytes = GetBytesAtOffset(stream, offset, 1);
            return bytes[0];
        }
        
        public static ushort GetUShortAtOffset(this Stream stream, long offset)
        {
            byte[] bytes = GetBytesAtOffset(stream, offset, 2);
            return BitConverter.ToUInt16(bytes);
        }
        
        public static uint GetUIntAtOffset(this Stream stream, long offset)
        {
            byte[] bytes = GetBytesAtOffset(stream, offset, 4);
            return BitConverter.ToUInt32(bytes);
        }
        
        public static ulong GetULongAtOffset(this Stream stream, long offset)
        {
            byte[] bytes = GetBytesAtOffset(stream, offset, 8);
            return BitConverter.ToUInt64(bytes);
        }
        
        public static sbyte GetSByteAtOffset(this Stream stream, long offset)
        {
            return (sbyte)GetByteAtOffset(stream, offset);
        }

        public static short GetShortAtOffset(this Stream stream, long offset)
        {
            byte[] bytes = GetBytesAtOffset(stream, offset, 2);
            return BitConverter.ToInt16(bytes);
        }
        
        public static int GetIntAtOffset(this Stream stream, long offset)
        {
            byte[] bytes = GetBytesAtOffset(stream, offset, 4);
            return BitConverter.ToInt32(bytes);
        }
        
        public static long GetLongAtOffset(this Stream stream, long offset)
        {
            byte[] bytes = GetBytesAtOffset(stream, offset, 8);
            return BitConverter.ToInt64(bytes);
        }
        
        public static char GetCharAtOffset(this Stream stream, long offset)
        {
            return (char)GetByteAtOffset(stream, offset);
        }

        public static UnicodeString GetStringAtOffset(this Stream stream, long offset)
        {
            ushort currentByte;
            List<IUnicodeCharacters> stringBytes = new List<IUnicodeCharacters>();
            var currentOffset = offset;

            while (true)
            {
                currentByte = GetUShortAtOffset(stream, currentOffset);

                currentOffset += 2;

                if (currentByte == 0) break;

                else if (currentByte == 0xFFFF)
                {
                    var unicodeByte = (SpecialCharacters)GetByteAtOffset(stream, currentOffset);
                    currentOffset += 1;

                    byte[] extraBytes = new byte[unicodeByte.ExtraBytes()];
                    for (int i = 0; i < extraBytes.Length; i++)
                    {
                        extraBytes[i] = GetByteAtOffset(stream, currentOffset + i);
                    }

                    stringBytes.Add(new SpecialUnicodeCharacters(unicodeByte, extraBytes));
                    currentOffset += extraBytes.Length;
                }
                else
                {
                    stringBytes.Add(new UnicodeCharacters(currentByte));
                }
            }

            return new UnicodeString(stringBytes);
        }

        // write helpers
        public static void WriteByteAtOffset(this Stream stream, long offset, byte writeByte)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            stream.WriteByte(writeByte);
        }
        
        public static void WriteBytesAtOffset(this Stream stream, long offset, byte[] writeBytes)
        {
            if (writeBytes.Length == 0)
            {
                return;
            }

            int bytesWrittenTotal = 0;
            stream.Seek(offset, SeekOrigin.Begin);
            do
            {
                var oldPosition = stream.Position;
                var bytesToWrite = Math.Min(writeBytes.Length - bytesWrittenTotal, writeBytes.Length);
                stream.Write(writeBytes, bytesWrittenTotal, bytesToWrite);
                bytesWrittenTotal += (int)(stream.Position - oldPosition);

            } while (bytesWrittenTotal < writeBytes.Length);
        }

        public static void AlignStream(this Stream stream, int alignment)
        {
            var m = stream.Length % alignment;
            if (m != 0)
            {
                stream.Seek(0, SeekOrigin.End);
                stream.Write(new byte[alignment - m]);
            }
        }

        /// <summary>
        /// Copy a chunk of a stream into another.
        /// </summary>
        /// <param name="input">Stream to read from.</param>
        /// <param name="output">Stream to write to.</param>
        /// <param name="start">The offset in the input stream to read at.</param>
        /// <param name="length">Amount of bytes to write.</param>
        public static void CopySubStream(this Stream input, Stream output, long start, long length)
        {
            if (length == 0)
            {
                return;
            }

            var bytesReadTotal = 0;
            var bufferSize = 64 * 1024;
            input.Seek(start, SeekOrigin.Begin);
            do
            {
                var bytesToRead = Math.Min(length - bytesReadTotal, bufferSize);
                var buffer = new byte[bytesToRead];
                var bytesRead = input.Read(buffer);
                if (bytesRead == 0)
                    break;

                output.Write(buffer, 0, bytesRead);
                bytesReadTotal += bytesRead;
            } while (bytesReadTotal < length);
        }

        /// <summary>
        /// Insert bytes into a stream.
        /// Note: this closes the input stream and returns a new stream.
        /// </summary>
        /// <param name="stream">Input stream to read from.</param>
        /// <param name="offset">The offset to insert the data at.</param>
        /// <param name="data">The bytes to insert.</param>
        /// <returns>The new stream with inserted data.</returns>
        public static Stream InsertIntoStream(this Stream stream, long offset, byte[] data)
        {
            if (data.Length == 0)
                return stream;

            // you can't really insert into a stream without pulling it entirely into memory, so cheat a bit
            string streamFileName = stream is FileStream fs
                ? $"{fs.Name}.bak"
                : string.Empty;
            var newStream = streamFileName.GetNewStream();

            // write any pending changes
            // copy old stream into new stream up to offset
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            stream.CopySubStream(newStream, 0, offset);

            // copy our new data
            newStream.Seek(offset, SeekOrigin.Begin);
            newStream.Write(data);

            // copy rest of oldstream
            stream.CopySubStream(newStream, offset, (int)stream.Length - offset);

            // dispose of old stream
            stream.Dispose();
            newStream.Flush();

            if (stream is FileStream f)
            {
                // flush new stream and close it
                newStream.Dispose();
                File.Delete(f.Name);
                File.Move(streamFileName, f.Name);
                // bypass GetNewStream because it'll overwrite files
                return File.Open(f.Name, FileMode.Open, FileAccess.ReadWrite);
            }
            return newStream;
        }

        /// <summary>
        /// Deletes bytes into a stream.
        /// Note: this closes the input stream and returns a new stream.
        /// </summary>
        /// <param name="stream">Input stream to read from.</param>
        /// <param name="offset">The offset to delete the data at.</param>
        /// <param name="length">The amount bytes to delete.</param>
        /// <returns>The new stream with deleted data.</returns>
        public static Stream DeleteFromStream(this Stream stream, long offset, int length)
        {
            if (length == 0)
                return stream;

            // you can't really insert into a stream without pulling it entirely into memory, so cheat a bit
            var streamFileName = stream is FileStream fs ? fs.Name : string.Empty;
            var newStream = $"{streamFileName}.bak".GetNewStream();

            // write any pending changes
            // copy old stream into new stream up to offset
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            stream.CopySubStream(newStream, 0, offset);
            stream.Seek(offset + length, SeekOrigin.Begin);
            stream.CopySubStream(newStream, offset + length, stream.Length - (offset + length));

            // dispose of old stream
            stream.Dispose();
            // flush new stream and close it
            newStream.Flush();

            if (stream is FileStream f)
            {
                newStream.Dispose();
                File.Delete(f.Name);
                File.Move($"{streamFileName}.bak", streamFileName);
                // bypass GetNewStream because it'll overwrite files
                return File.Open(streamFileName, FileMode.Open, FileAccess.ReadWrite);
            }
            return newStream;
        }

        /// <summary>
        /// Get a list of offsets where a given "marker" appears.
        /// </summary>
        /// <param name="stream">Stream to search in.</param>
        /// <param name="marker">The marker to look for.</param>
        /// <returns>List of offsets found.</returns>
        public static IEnumerable<int> OccurencesOfBytes(this Stream stream, int marker)
        {
            var offsets = new List<int>();
            for (int i = 0; i < stream.Length; i++)
            {
                var checkBytes = stream.GetIntAtOffset(i);
                if (checkBytes == marker)
                {
                    offsets.Add(i);
                    i += 4;
                }
            }

            return offsets;
        }
    }
}
