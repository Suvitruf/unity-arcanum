using System;
using System.Collections.Generic;
using Arcanum.Formats.Database;

namespace Arcanum.Formats.Script
{
    /// <summary>
    /// Every object script (<c>scr/*.scr</c>) parsed once and held in memory, keyed by script number — our
    /// equivalent of the engine's script cache (<c>script.c</c> <c>script_lock</c>), but **unbounded**: the
    /// whole corpus is ~2,400 tiny files (~2 MB resident), so we just preload them all at startup instead of
    /// the engine's 100-entry LRU. After this, running a script is a dictionary lookup — no disk read, no
    /// <c>scr/</c> directory rescan per call.
    /// </summary>
    public sealed class ScriptDatabase
    {
        private readonly Dictionary<int, ScriptFile> _byNum;

        private ScriptDatabase(Dictionary<int, ScriptFile> byNum, int failed)
        {
            _byNum = byNum;
            FailedCount = failed;
        }

        /// <summary>Scripts successfully parsed and cached.</summary>
        public int Count => _byNum.Count;
        /// <summary>Scripts found but skipped because they failed to parse.</summary>
        public int FailedCount { get; }

        /// <summary>The cached script for a number (from <c>OBJ_F_SCRIPTS</c>), or null if absent.</summary>
        public ScriptFile Get(int scriptNum) => _byNum.TryGetValue(scriptNum, out ScriptFile f) ? f : null;

        /// <summary>Enumerates <c>scr/</c>, parses every <c>.scr</c>, and caches it by its filename number.</summary>
        public static ScriptDatabase Load(DatVirtualFileSystem vfs)
        {
            var map = new Dictionary<int, ScriptFile>();
            int failed = 0;
            if (vfs != null)
            {
                foreach (string path in vfs.EnumerateFiles("scr/"))
                {
                    if (!path.EndsWith(".scr", StringComparison.OrdinalIgnoreCase)) continue;
                    int num = NumberFromPath(path);
                    if (num <= 0 || map.ContainsKey(num)) continue;
                    try { map[num] = ScriptReader.Read(vfs.ReadAllBytes(path)); }
                    catch { failed++; }
                }
            }
            return new ScriptDatabase(map, failed);
        }

        // "scr/01267bates mansion….scr" → 1267. Every shipped script uses a fixed 5-digit numeric prefix
        // (verified: all 2,391 files, nums 997–30302), so the first five filename chars are the number.
        private static int NumberFromPath(string path)
        {
            int start = path.LastIndexOf('/') + 1;
            if (start + 5 > path.Length) return 0;
            int n = 0;
            for (int i = start; i < start + 5; i++)
            {
                char ch = path[i];
                if (ch < '0' || ch > '9') return 0;
                n = n * 10 + (ch - '0');
            }
            return n;
        }
    }
}
