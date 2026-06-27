using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arcanum.Formats.IO;

namespace Arcanum.Formats.Database
{
    /// <summary>
    /// Reads an Arcanum <c>.dat</c> archive (the format used by <c>Arcanum1.dat</c>
    /// … <c>Arcanum4.dat</c> and module archives). The table of contents lives at the
    /// end of the file; payloads live at the front and are either stored verbatim or
    /// zlib-compressed.
    ///
    /// On-disk layout (all integers little-endian), matched against the shipped game
    /// files and alexbatalov/tig <c>database.c</c> (<c>tig_database_open</c>):
    /// <code>
    /// [ payload blocks ... ][ entriesCount(4) | entry records ... ][ entryTableSize(4) ][ trailer ]
    /// trailer (last 12 bytes):  magic(4)  nameTableSize(4)  entryTableOffset(4)
    ///   for the "1TAD" variant a 16-byte GUID precedes the magic; "  TAD" has none.
    /// entryTableSize sits at (fileSize - 4 - entryTableOffset); the record block
    ///   (entriesCount + records) begins at (fileSize - entryTableOffset).
    /// entry record: nameSize(4) name(nameSize) padding(4) flags(4) uncompressedSize(4) compressedSize(4) offset(4)
    ///   each record's real data offset = offset + baseOffset, where
    ///   baseOffset = fileSize - entryTableSize - entryTableOffset.
    /// </code>
    /// Keeps the underlying file open so payloads can be read lazily; dispose to release it.
    /// </summary>
    public sealed class DatArchive : IDisposable
    {
        /// <summary>Magic for the plain archive variant — ASCII <c>" TAD"</c>.</summary>
        public const uint MagicDat = 0x44415420; // bytes 0x20 'T' 'A' 'D'

        /// <summary>Magic for the GUID-tagged variant — ASCII <c>"1TAD"</c>.</summary>
        public const uint MagicDat1 = 0x44415431; // bytes '1' 'T' 'A' 'D'

        private const int TrailerSize = 12; // magic(4) + nameTableSize(4) + entryTableOffset(4)

        private readonly Stream _stream;
        private readonly bool _ownsStream;
        private readonly List<DatFileEntry> _entries;
        private readonly Dictionary<string, DatFileEntry> _byPath;

        public string FilePath { get; }
        public uint Magic { get; }
        public IReadOnlyList<DatFileEntry> Entries => _entries;

        private DatArchive(Stream stream, bool ownsStream, string filePath)
        {
            _stream = stream;
            _ownsStream = ownsStream;
            FilePath = filePath;
            _entries = new List<DatFileEntry>();
            _byPath = new Dictionary<string, DatFileEntry>(StringComparer.Ordinal);

            Magic = ReadTableOfContents();
        }

        public static DatArchive Open(string path)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                return new DatArchive(stream, ownsStream: true, path);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        /// <summary>Opens an archive over an already-positioned, seekable stream (e.g. for tests).</summary>
        public static DatArchive Open(Stream stream, string label = "<stream>")
            => new DatArchive(stream, ownsStream: false, label);

        public bool TryGetEntry(string virtualPath, out DatFileEntry entry)
            => _byPath.TryGetValue(DatFileEntry.Normalize(virtualPath), out entry);

        public byte[] ReadAllBytes(string virtualPath)
        {
            if (!TryGetEntry(virtualPath, out var entry))
                throw new FileNotFoundException($"'{virtualPath}' is not present in {FilePath}");
            return ReadAllBytes(entry);
        }

        /// <summary>Reads and (if needed) decompresses the payload for <paramref name="entry"/>.</summary>
        public byte[] ReadAllBytes(DatFileEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry.IsDirectory)
                throw new InvalidOperationException($"'{entry.Path}' is a directory, not a file");
            if (entry.CompressedSize == 0)
                return Array.Empty<byte>();

            // Thread-safe: the seek+read on the shared stream is serialized (so background-thread parsing —
            // e.g. the async boot systems — can read concurrently with the main thread); the decompress works
            // on the local buffer and runs outside the lock.
            byte[] raw = new byte[entry.CompressedSize];
            lock (_stream)
            {
                _stream.Seek(entry.DataOffset, SeekOrigin.Begin);
                ReadExactly(_stream, raw, 0, raw.Length);
            }

            return entry.IsCompressed
                ? ZlibInflate.Inflate(raw, 0, raw.Length, entry.UncompressedSize)
                : raw;
        }

        private uint ReadTableOfContents()
        {
            long fileSize = _stream.Length;
            if (fileSize < TrailerSize)
                throw new DatFormatException($"{FilePath} is too small ({fileSize} bytes) to be a .dat archive");

            using var reader = new BinaryReader(_stream, Encoding.ASCII, leaveOpen: true);

            // Trailer: the last 12 bytes hold magic, name-table size, and the entry-table offset.
            // (The "1TAD" variant also stores a 16-byte GUID just before the magic; we don't need it.)
            _stream.Seek(fileSize - TrailerSize, SeekOrigin.Begin);
            uint magic = reader.ReadUInt32();
            int nameTableSize = reader.ReadInt32();
            int entryTableOffset = reader.ReadInt32();

            if (magic != MagicDat && magic != MagicDat1)
                throw new DatFormatException(
                    $"{FilePath} has unexpected magic 0x{magic:X8} (\"{FourCc(magic)}\"); " +
                    $"expected \" TAD\" or \"1TAD\". nameTableSize={nameTableSize}, entryTableOffset={entryTableOffset}.");

            if (entryTableOffset < 0 || entryTableOffset > fileSize)
                throw new DatFormatException(
                    $"{FilePath} entryTableOffset is out of bounds ({entryTableOffset}, fileSize={fileSize}).");

            // entryTableSize is stored 4 bytes before the record block; the record block
            // (entriesCount followed by the records) begins at fileSize - entryTableOffset.
            _stream.Seek(fileSize - 4 - entryTableOffset, SeekOrigin.Begin);
            int entryTableSize = reader.ReadInt32();
            long baseOffset = fileSize - entryTableSize - entryTableOffset;

            int entriesCount = reader.ReadInt32();
            if (entriesCount < 0 || entriesCount > 1_000_000)
                throw new DatFormatException($"{FilePath} has an implausible entry count ({entriesCount}).");

            ReadEntries(reader, entriesCount, baseOffset, fileSize);
            return magic;
        }

        private void ReadEntries(BinaryReader reader, int entriesCount, long baseOffset, long fileSize)
        {
            for (int i = 0; i < entriesCount; i++)
            {
                int nameSize = reader.ReadInt32();
                if (nameSize < 0 || nameSize > 4096)
                    throw new DatFormatException(
                        $"{FilePath} has a corrupt entry name length ({nameSize}) for entry {i}");

                string storedPath = ReadLatin1(reader, nameSize).TrimEnd('\0');
                reader.ReadInt32(); // padding / reserved
                var flags = (DatEntryFlags)reader.ReadInt32();
                int uncompressedSize = reader.ReadInt32();
                int compressedSize = reader.ReadInt32();
                long dataOffset = reader.ReadUInt32() + baseOffset;

                if (!flags.HasFlag(DatEntryFlags.Directory) &&
                    (dataOffset < 0 || dataOffset + compressedSize > fileSize))
                    throw new DatFormatException(
                        $"{FilePath}: entry '{storedPath}' payload runs out of bounds " +
                        $"(offset={dataOffset}, size={compressedSize}, fileSize={fileSize})");

                var entry = new DatFileEntry(storedPath, flags, uncompressedSize, compressedSize, dataOffset);
                if (entry.IsIgnored) continue;

                _entries.Add(entry);
                if (!entry.IsDirectory)
                    _byPath[entry.Path] = entry;
            }
        }

        private static string ReadLatin1(BinaryReader reader, int byteCount)
        {
            byte[] bytes = reader.ReadBytes(byteCount);
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) chars[i] = (char)bytes[i];
            return new string(chars);
        }

        private static string FourCc(uint value)
        {
            return new string(new[]
            {
                (char)(value & 0xFF),
                (char)((value >> 8) & 0xFF),
                (char)((value >> 16) & 0xFF),
                (char)((value >> 24) & 0xFF),
            });
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, offset + read, count - read);
                if (n <= 0) throw new EndOfStreamException($"Expected {count} bytes but stream ended after {read}");
                read += n;
            }
        }

        public void Dispose()
        {
            if (_ownsStream) _stream.Dispose();
        }
    }
}
