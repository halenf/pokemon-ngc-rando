﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using XDCommon.Contracts;

namespace XDCommon.Utility
{
    public class FSysDetailsHeader
    {
        static byte[] MysteryBytes = new byte[] { 0x80, 0, 0, 0 };

        public string FileName { get; set; }
        public ushort Identifier { get; set; }
        public FileTypes Filetype { get; set; }

        public uint NameOffset { get; set; }
        public uint StartOffset { get; set; }
        public uint UncompressedSize { get; set; }
        public uint CompressedSize { get; set; }
        public uint FileFormatIndexOffset { get; set; }
        public bool IsCompressed => CompressedSize != UncompressedSize;

        Game game;
        public FSysDetailsHeader(Game game)
        {
            this.game = game;
        }

        public byte[] Encode(int detailsHeaderSize)
        {
            var header = new List<byte>();

            header.AddRange(Identifier.GetBytes());
            header.Add((byte)Filetype);
            header.Add(0);

            header.AddRange(StartOffset.GetBytes());
            header.AddRange(UncompressedSize.GetBytes());
            // don't know what this is for...
            header.AddRange(MysteryBytes);
            header.AddRange(0.GetBytes());

            header.AddRange(CompressedSize.GetBytes());
            header.AddRange(0.GetBytes());
            header.AddRange(0.GetBytes());

            header.AddRange(FileFormatIndexOffset.GetBytes());
            header.AddRange(NameOffset.GetBytes());

            header.AddRange(new byte[0xC]);

            if (game == Game.Colosseum)
            {
                header.AddRange(Enumerable.Repeat<byte>(0x11, 0xC));
            }

            if (header.Count < detailsHeaderSize)
            {
                header.AddRange(new byte[detailsHeaderSize - header.Count]);
            }
            return header.ToArray();
        }
    }

    public class FSys : BaseExtractedFile
    {
        const byte FSYSGroupIDOffset = 0x08;
        const byte NumberOfEntriesOffset = 0x0C;
        const byte FSYSFileSizeOffset = 0x20;
        const byte FirstFileNamePointerOffset = 0x44;
        const byte FirstFileOffset = 0x48;
        const byte FirstFileDetailsPointerOffset = 0x60;
        const byte FirstFileNameOffset = 0x70;

        const byte FileIdentifierOffset = 0x00; // 3rd byte is the file format, 1st half is an arbitrary identifier
        const byte FileFormatOffset = 0x02;
        const byte FileStartPointerOffset = 0x04;
        const byte UncompressedSizeOffset = 0x08;
        const byte CompressedSizeOffset = 0x14;
        const byte FileDetailsFullFilenameOffset = 0x1C; // includes file extension. Not always used.
        const byte FileFormatIndexOffset = 0x20; // half of value in byte 3
        const byte FileDetailsFilenameOffset = 0x24;

        const uint TCODbytes = 0x54434F44;
        const uint FSYSbytes = 0x46535953;
        const ushort USbytes = 0x5553;
        const ushort JPbytes = 0x4A50;

        public Dictionary<string, IExtractedFile> ExtractedEntries = new Dictionary<string, IExtractedFile>();


        public int GroupID
        {
            get
            {
                return ExtractedFile.GetIntAtOffset(FSYSGroupIDOffset);
            }
            set
            {
                ExtractedFile.Seek(FSYSGroupIDOffset, SeekOrigin.Begin);
                ExtractedFile.Write(value.GetBytes());
            }
        }

        public int NumberOfEntries
        {
            get
            {
                return ExtractedFile.GetIntAtOffset(NumberOfEntriesOffset);
            }
            set
            {
                ExtractedFile.Seek(NumberOfEntriesOffset, SeekOrigin.Begin);
                ExtractedFile.Write(value.GetBytes());
            }
        }

        List<FSysDetailsHeader> fSysDetailsHeaders = new List<FSysDetailsHeader>();

        public override FileTypes FileType => FileTypes.FSYS;

        public int Offset { get; private set; }
        public int Size { get; private set; }

        public bool UsesFileExtensions => ExtractedFile.GetByteAtOffset(0x13) == 1;

        private int sizeOfFileDetails;
        public FSys(FSTFileEntry fileEntry, ISO iso)
        {
            FileName = fileEntry.Name.ToString();
            Path = iso.Path;

            Offset = (int)fileEntry.FileDataOffset;
            Size = (int)fileEntry.Size;

            if (Configuration.Verbose)
            {
                Console.WriteLine($"Extracting {FileName}");
            }

            ExtractedFile = $"{Path}/{FileName}".GetNewStream();
            iso.ExtractedFile.CopySubStream(ExtractedFile, Offset, Size);

            sizeOfFileDetails = iso.Game == Game.XD ? 0x70 : 0x50;
            LoadFileDetails(iso.Game);
        }

        void LoadFileDetails(Game game)
        {
            for (int i = 0; i < NumberOfEntries; i++)
            {
                var start = GetStartOffsetForFileDetails(i);
                var fileDetails = new FSysDetailsHeader(game)
                {
                    Identifier = ExtractedFile.GetUShortAtOffset(start),
                    Filetype = (FileTypes)ExtractedFile.GetByteAtOffset(start + FileFormatOffset),
                    StartOffset = ExtractedFile.GetUIntAtOffset(start + FileStartPointerOffset),
                    UncompressedSize = ExtractedFile.GetUIntAtOffset(start + UncompressedSizeOffset),
                    CompressedSize = ExtractedFile.GetUIntAtOffset(start + CompressedSizeOffset),
                    FileFormatIndexOffset = ExtractedFile.GetUIntAtOffset(start + FileFormatIndexOffset),
                    NameOffset = ExtractedFile.GetUIntAtOffset(start + FileDetailsFilenameOffset)
                };

                fileDetails.FileName = FST.GetFSTString(ExtractedFile, fileDetails.NameOffset);
                fSysDetailsHeaders.Add(fileDetails);
            }
        }

        public int GetStartOffsetForFileDetails(int index)
        {
            return ExtractedFile.GetIntAtOffset(FirstFileDetailsPointerOffset + (index * 4));
        }

        public FSysDetailsHeader GetDetailsForFile(int index)
        {
            if (index >= 0 && index < fSysDetailsHeaders.Count)
                return fSysDetailsHeaders[index];
            return null;
        }

        public int GetIndexForFileName(string fileName)
        {
            for (int i = 0; i < NumberOfEntries; i++)
            {
                var detailsName = fSysDetailsHeaders[i].FileName.ToString();
                var detailsType = fSysDetailsHeaders[i].Filetype;
                if (fileName == detailsName || (fileName.RemoveFileExtensions() == detailsName && fileName.EndsWith(detailsType.FileTypeName())))
                {
                    return i;
                }
            }
            return -1;
        }

        public IExtractedFile GetEntryByFileName(string filename)
        {
            var index = GetIndexForFileName(filename);
            var details = GetDetailsForFile(index);
            if (ExtractedEntries.ContainsKey($"{details.FileName}.{details.Filetype}".ToLower()))
            {
                return ExtractedEntries[filename];
            }
            else
            {
                var entry = FSysFileEntry.ExtractFromFSys(this, index);
                if (ExtractedEntries.ContainsKey(entry.FileName))
                {
                    return ExtractedEntries[entry.FileName];
                }
                else
                {
                    ExtractedEntries.Add(entry.FileName, entry);
                    return entry;
                }
            }
        }

        public IExtractedFile GetEntryByIndex(int index)
        {
            if (index < 0 || index > NumberOfEntries)
                return null;

            return GetEntryByFileName(GetDetailsForFile(index).FileName.ToString());
        }

        public override Stream Encode(bool _ = false)
        {
            Stream fSysStream = $"{Path}/{FileName}.repak".GetNewStream();

            if (ExtractedEntries.Count == 0)
            {
                // nothing extracted, nothing changed
                // just copy the existing stream back
                ExtractedFile.Seek(0, SeekOrigin.Begin);
                ExtractedFile.CopyTo(fSysStream);
                return fSysStream;
            }

            // unpack the entire fsys archive for easier repacking
            if (ExtractedEntries.Count < NumberOfEntries)
                FSysExtractor.ExtractFSys(this, false);

            var startDetailsOffset = 0x60;

            // copy the header back, we'll update the sizes later
            NumberOfEntries = ExtractedEntries.Count;
            ExtractedFile.Seek(0, SeekOrigin.Begin);
            ExtractedFile.CopySubStream(fSysStream, 0, startDetailsOffset);

            var sizeOfDetailsPointers = (fSysDetailsHeaders.Count * 4);
            var startNameOffset = startDetailsOffset + sizeOfDetailsPointers + sizeOfDetailsPointers.GetAlignBytesCount(16);
            fSysStream.Seek(startNameOffset, SeekOrigin.Begin);

            // write the name table
            var startDataOffset = uint.MaxValue;
            foreach (var detailHeader in fSysDetailsHeaders)
            {
                fSysStream.Write(Encoding.UTF8.GetBytes(detailHeader.FileName));
                fSysStream.WriteByte(0);

                if (detailHeader.StartOffset < startDataOffset)
                    startDataOffset = detailHeader.StartOffset;
            }

            // align names
            fSysStream.AlignStream(0x10);
            var lastNameOffset = (int)fSysStream.Position;

            // write pointers to details offset
            fSysStream.Seek(startDetailsOffset, SeekOrigin.Begin);
            for (int x = 0; x < fSysDetailsHeaders.Count; x++)
            {
                fSysStream.Write((lastNameOffset + (x * sizeOfFileDetails)).GetBytes());
            }

            // write our data, update offsets along the way if we find mismatches
            for (int i = 0; i < fSysDetailsHeaders.Count; i++)
            {
                var detailHeader = fSysDetailsHeaders[i];

                var entryFileName = detailHeader.FileName.ToString();
                if (!entryFileName.EndsWith(detailHeader.Filetype.FileTypeName()))
                    entryFileName = $"{entryFileName}{detailHeader.Filetype.FileTypeName()}";

                var entry = ExtractedEntries[entryFileName];

                using var encodeStream = entry.Encode(detailHeader.IsCompressed);
                fSysStream.Seek(detailHeader.StartOffset, SeekOrigin.Begin);
                encodeStream.CopyTo(fSysStream);
                fSysStream.Flush();

                detailHeader.UncompressedSize = (uint)entry.ExtractedFile.Length;
                if (encodeStream.Length != detailHeader.CompressedSize)
                {
                    var adjustedSize = (int)(encodeStream.Length - detailHeader.CompressedSize);
                    detailHeader.CompressedSize = (uint)encodeStream.Length;

                    if (adjustedSize < 0)
                        continue; // probably wasteful, but meh

                    for (int j = i + 1; j < fSysDetailsHeaders.Count; j++)
                    {
                        var adjDetailsHeader = fSysDetailsHeaders[j];
                        adjDetailsHeader.StartOffset += (uint)adjustedSize;
                        adjDetailsHeader.StartOffset += adjDetailsHeader.StartOffset.GetAlignBytesCount(0x10);
                    }
                }
            }

            fSysStream.AlignStream(0x10);
            fSysStream.Write(new byte[0x10]);
            fSysStream.Seek(-4, SeekOrigin.Current);
            fSysStream.Write(FSYSbytes.GetBytes());
            fSysStream.Flush();

            fSysStream.WriteBytesAtOffset(FSYSFileSizeOffset, ((int)fSysStream.Length).GetBytes());

            // go back and re-write our file details table
            fSysStream.Seek(lastNameOffset, SeekOrigin.Begin);
            foreach (var detailHeader in fSysDetailsHeaders)
            {
                fSysStream.Write(detailHeader.Encode(sizeOfFileDetails));
            }

            fSysStream.Flush();
            fSysStream.Seek(0, SeekOrigin.Begin);
            return fSysStream;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (!disposedValue)
            {
                foreach (var file in ExtractedEntries.Values)
                {
                    file.Dispose();
                }
                ExtractedEntries.Clear();
                base.Dispose(isDisposing);
            }
        }
    }
}
