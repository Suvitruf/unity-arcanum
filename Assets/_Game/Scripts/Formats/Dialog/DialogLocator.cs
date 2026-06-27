using System;
using Arcanum.Formats.Database;

namespace Arcanum.Formats.Dialog
{
    /// <summary>
    /// Resolves a dialog number (from an NPC's <c>OBJ_F_SCRIPTS</c>) to its <c>.dlg</c> file in the VFS.
    /// The engine builds the name from the number; the files are <c>dlg/&lt;5-digit num&gt;&lt;name&gt;.dlg</c>,
    /// so a number-prefix match finds the file without needing the script-name table.
    /// </summary>
    public static class DialogLocator
    {
        /// <summary>Loads and parses the dialog for a number (e.g. 19 → <c>dlg/00019*.dlg</c>), or null if absent.</summary>
        public static DialogScript Load(DatVirtualFileSystem vfs, int dialogNum)
        {
            string path = FindPath(vfs, dialogNum);
            return path == null ? null : DlgReader.Read(vfs.ReadAllBytes(path));
        }

        /// <summary>The <c>.dlg</c> path whose filename begins with the 5-digit dialog number, or null.</summary>
        public static string FindPath(DatVirtualFileSystem vfs, int dialogNum)
        {
            if (vfs == null || dialogNum <= 0) return null;
            string prefix = "dlg/" + dialogNum.ToString("D5");
            foreach (string p in vfs.EnumerateFiles("dlg/"))
                if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase))
                    return p;
            return null;
        }
    }
}
