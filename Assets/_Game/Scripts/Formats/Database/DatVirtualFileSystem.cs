using System;
using System.Collections.Generic;
using System.IO;

namespace Arcanum.Formats.Database
{
    /// <summary>
    /// Mounts several <see cref="DatArchive"/>s — and optionally loose-file directories — as a single virtual
    /// filesystem, the way the original engine layers its repositories.
    ///
    /// <para><b>Precedence.</b> Loose-file directories (mods / patches) override the archives. Among archives the
    /// <b>first</b> mounted wins (search is front-to-back), so mount the highest-priority archive first (e.g. a
    /// module dat before <c>arcanum2.dat</c>). NOTE: this is the <i>inverse</i> of the original engine, which adds
    /// each repository to the head of its list so the <i>last</i>-added wins (tig <c>file.c:625</c>); callers here
    /// compensate by mounting in priority order. Loose files always override archives, matching the engine's
    /// loose-file overlay (tig <c>file.c:1784</c>) — the mechanism mods/patches use.</para>
    ///
    /// Dispose to close all mounted archives. (Loose directories hold no handles.)
    /// </summary>
    public sealed class DatVirtualFileSystem : IDisposable
    {
        private readonly List<DatArchive> _mounts = new List<DatArchive>();
        private readonly List<string> _looseRoots = new List<string>();

        public IReadOnlyList<DatArchive> Mounts => _mounts;

        /// <summary>Loose-file directory roots that overlay the archives (later-added wins).</summary>
        public IReadOnlyList<string> LooseRoots => _looseRoots;

        /// <summary>Mounts an already-open archive (searched after existing archive mounts).</summary>
        public void Mount(DatArchive archive) => _mounts.Add(archive);

        /// <summary>Opens and mounts an archive from disk.</summary>
        public DatArchive MountFile(string path)
        {
            var archive = DatArchive.Open(path);
            _mounts.Add(archive);
            return archive;
        }

        /// <summary>
        /// Adds a loose-file directory whose contents override the archives: a file at
        /// <c>&lt;root&gt;/&lt;virtualPath&gt;</c> shadows that same path in any mounted archive. This is the
        /// modding / patch overlay. Later-added roots take precedence over earlier ones.
        /// </summary>
        public void MountDirectory(string root)
        {
            if (string.IsNullOrEmpty(root)) throw new ArgumentNullException(nameof(root));
            _looseRoots.Add(root);
        }

        /// <summary>
        /// Locates a path in the mounted <b>archives</b>. Does NOT see loose-file overlays — use
        /// <see cref="Exists"/> / <see cref="ReadAllBytes"/> for the full, overlay-aware view.
        /// </summary>
        public bool TryGetEntry(string virtualPath, out DatArchive archive, out DatFileEntry entry)
        {
            foreach (var mount in _mounts)
            {
                if (mount.TryGetEntry(virtualPath, out entry))
                {
                    archive = mount;
                    return true;
                }
            }

            archive = null;
            entry = null;
            return false;
        }

        public bool Exists(string virtualPath)
            => TryLooseFile(virtualPath, out _) || TryGetEntry(virtualPath, out _, out _);

        /// <summary>Every virtual path under <paramref name="prefix"/> across loose roots + all mounts (deduped).
        /// Recursive prefix match (not the engine's single-level glob), which is what our consumers want.</summary>
        public IEnumerable<string> EnumerateFiles(string prefix)
        {
            string norm = DatFileEntry.Normalize(prefix);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string root in _looseRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    string rel = DatFileEntry.Normalize(file.Substring(root.Length));
                    if (rel.StartsWith(norm, StringComparison.Ordinal) && seen.Add(rel))
                        yield return rel;
                }
            }

            foreach (var mount in _mounts)
                foreach (var entry in mount.Entries)
                    if (entry.Path.StartsWith(norm, StringComparison.Ordinal) && seen.Add(entry.Path))
                        yield return entry.Path;
        }

        public byte[] ReadAllBytes(string virtualPath)
        {
            if (TryLooseFile(virtualPath, out string loosePath))
                return File.ReadAllBytes(loosePath);
            if (!TryGetEntry(virtualPath, out var archive, out var entry))
                throw new FileNotFoundException($"'{virtualPath}' not present in any mounted archive or loose directory");
            return archive.ReadAllBytes(entry);
        }

        // Resolves a virtual path against the loose roots, later-added winning (so it overrides earlier roots).
        private bool TryLooseFile(string virtualPath, out string fullPath)
        {
            string norm = DatFileEntry.Normalize(virtualPath);
            for (int i = _looseRoots.Count - 1; i >= 0; i--)
            {
                string candidate = Path.Combine(_looseRoots[i], norm.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                {
                    fullPath = candidate;
                    return true;
                }
            }

            fullPath = null;
            return false;
        }

        public void Dispose()
        {
            foreach (var mount in _mounts) mount.Dispose();
            _mounts.Clear();
            _looseRoots.Clear();
        }
    }
}
