namespace Arcanum.Formats.Database
{
    /// <summary>
    /// Describes one record in a <see cref="DatArchive"/>: where its payload
    /// lives in the file and how to turn that payload back into usable bytes.
    /// Instances are immutable and produced while reading the archive's table
    /// of contents; the payload itself is read on demand.
    /// </summary>
    public sealed class DatFileEntry
    {
        /// <summary>
        /// Normalized virtual path using forward slashes and lower case,
        /// e.g. <c>art/tile/foo.art</c>. Use this for lookups.
        /// </summary>
        public string Path { get; }

        /// <summary>The path exactly as stored in the archive (typically back-slashed).</summary>
        public string StoredPath { get; }

        public DatEntryFlags Flags { get; }

        /// <summary>Size of the payload after decompression, in bytes.</summary>
        public int UncompressedSize { get; }

        /// <summary>Number of bytes occupied by the payload on disk.</summary>
        public int CompressedSize { get; }

        /// <summary>Absolute byte offset of the payload from the start of the archive file.</summary>
        public long DataOffset { get; }

        public bool IsDirectory => (Flags & DatEntryFlags.Directory) != 0;
        public bool IsCompressed => (Flags & DatEntryFlags.Compressed) != 0;
        public bool IsIgnored => (Flags & DatEntryFlags.Ignored) != 0;

        public DatFileEntry(string storedPath, DatEntryFlags flags, int uncompressedSize, int compressedSize, long dataOffset)
        {
            StoredPath = storedPath;
            Path = Normalize(storedPath);
            Flags = flags;
            UncompressedSize = uncompressedSize;
            CompressedSize = compressedSize;
            DataOffset = dataOffset;
        }

        /// <summary>Converts a stored path to the canonical lookup form.</summary>
        public static string Normalize(string storedPath)
        {
            if (string.IsNullOrEmpty(storedPath)) return string.Empty;
            return storedPath.Replace('\\', '/').Trim('/').ToLowerInvariant();
        }

        public override string ToString() => $"{Path} ({UncompressedSize} bytes, {Flags})";
    }
}
