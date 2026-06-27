using System;

namespace Arcanum.Formats.Database
{
    /// <summary>
    /// Storage flags for a single record inside an Arcanum <c>.dat</c> archive.
    /// Values mirror the <c>TIG_DATABASE_ENTRY_*</c> constants used by the
    /// original engine (see alexbatalov/tig <c>database.h</c>).
    /// </summary>
    [Flags]
    public enum DatEntryFlags
    {
        None = 0,

        /// <summary>Payload is stored verbatim; <see cref="DatFileEntry.CompressedSize"/> equals the real size.</summary>
        Plain = 0x01,

        /// <summary>Payload is zlib/DEFLATE compressed and must be inflated to <see cref="DatFileEntry.UncompressedSize"/>.</summary>
        Compressed = 0x02,

        /// <summary>Reserved bit observed in archives; meaning not required for reading.</summary>
        Reserved0x100 = 0x100,

        /// <summary>Reserved bit observed in archives; meaning not required for reading.</summary>
        Reserved0x200 = 0x200,

        /// <summary>Record describes a directory node rather than a file payload.</summary>
        Directory = 0x400,

        /// <summary>Record is marked ignored by the original tooling and should be skipped.</summary>
        Ignored = 0x800,
    }
}
