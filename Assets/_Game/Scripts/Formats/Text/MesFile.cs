using System.Collections.Generic;

namespace Arcanum.Formats.Text
{
    /// <summary>
    /// A parsed Arcanum <c>.mes</c> message table: an ordered list of
    /// <c>{key}{value}</c> entries used everywhere in the game for text — tile
    /// names, item descriptions, UI strings, dialogue, and more.
    ///
    /// <para>Lookups (<see cref="Get"/>/<see cref="TryGet"/>) use a by-key map; if a key appears more than once
    /// the <b>last</b> occurrence wins (shipped files have no duplicates — the engine sorts + warns). Iterate
    /// <see cref="Entries"/> for file order.</para>
    /// </summary>
    public sealed class MesFile
    {
        private readonly List<KeyValuePair<int, string>> _entries;
        private readonly Dictionary<int, string> _byKey;

        public MesFile(List<KeyValuePair<int, string>> entries)
        {
            _entries = entries;
            _byKey = new Dictionary<int, string>(entries.Count);
            foreach (var e in entries) _byKey[e.Key] = e.Value;
        }

        /// <summary>Entries in the order they appear in the file.</summary>
        public IReadOnlyList<KeyValuePair<int, string>> Entries => _entries;

        public int Count => _entries.Count;

        public bool TryGet(int key, out string value) => _byKey.TryGetValue(key, out value);

        public string Get(int key) => _byKey.TryGetValue(key, out var v) ? v : null;

        /// <summary>Number of entries whose key falls within <c>[lo, hi]</c> inclusive.</summary>
        public int CountInRange(int lo, int hi)
        {
            int count = 0;
            foreach (var e in _entries)
                if (e.Key >= lo && e.Key <= hi)
                    count++;
            return count;
        }

        /// <summary>
        /// Returns a new table with <paramref name="overlay"/> layered over this one: an overlay entry overrides
        /// the base entry with the same key (in place), and a brand-new overlay key is appended. Ports the engine's
        /// <c>mes_merge</c> (<c>mes.c:351</c>) — the mechanism for localization / patch overlays.
        /// </summary>
        public MesFile MergedWith(MesFile overlay)
        {
            var merged = new List<KeyValuePair<int, string>>(_entries);
            if (overlay == null) return new MesFile(merged);

            var pos = new Dictionary<int, int>(merged.Count);
            for (int i = 0; i < merged.Count; i++) pos[merged[i].Key] = i; // last occurrence wins
            foreach (var e in overlay._entries)
            {
                if (pos.TryGetValue(e.Key, out int i)) merged[i] = e; // override base value, keep position
                else
                {
                    pos[e.Key] = merged.Count;
                    merged.Add(e);
                } // append new key
            }

            return new MesFile(merged);
        }
    }
}
