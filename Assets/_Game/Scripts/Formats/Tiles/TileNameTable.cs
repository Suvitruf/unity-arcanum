using System;
using System.Collections.Generic;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.Tiles
{
    /// <summary>Terrain flags parsed from a tile-name's <c>/…</c> suffix (engine <c>TF_*</c>, a_name.c:8).</summary>
    [Flags]
    public enum TileFlags : byte
    {
        None = 0,
        Block = 0x01,
        Sinkable = 0x02,
        Flyable = 0x04,
        Slippery = 0x08,
        Natural = 0x10,
        Soundproof = 0x20,
    }

    /// <summary>
    /// The four terrain-name tables loaded from <c>art/tile/tilename.mes</c>, split
    /// by the entry's key range (mirrors <c>count_tile_names</c>/<c>load_tile_names</c>
    /// in arcanum-ce <c>a_name.c</c>):
    /// <list type="bullet">
    /// <item>keys 0–99 → outdoor flippable</item>
    /// <item>keys 100–199 → outdoor non-flippable</item>
    /// <item>keys 200–299 → indoor flippable</item>
    /// <item>keys 300–399 → indoor non-flippable</item>
    /// </list>
    /// Each value is <c>name[/flags] [sound]</c>: the first three characters are the terrain name used to build
    /// tile <c>.art</c> file names; an optional <c>/</c>-suffix carries terrain <see cref="TileFlags"/> (one char
    /// each: <c>s b f i n p</c>) and a trailing footstep <b>sound id</b> (engine <c>a_name.c:467–502</c>).
    ///
    /// <para>NOT modelled (deferred, see <c>Docs/FormatsCoverage.md</c>): the tile <b>edge/blend adjacency graph</b>
    /// (tilename keys ≥400, <c>load_tile_edges</c> a_name.c:544) and the <c>tilevariant.dat</c> variant-existence
    /// cache — the engine uses these to route a blend through an intermediate terrain; we substitute via
    /// <see cref="TileArtPathResolver.BaseFallback"/> for the ~0.4% of tiles that need it.</para>
    /// </summary>
    public sealed class TileNameTable
    {
        private sealed class Bucket
        {
            public readonly string[] Names;
            public readonly TileFlags[] Flags;
            public readonly int[] Sounds;

            public Bucket(List<string> names, List<TileFlags> flags, List<int> sounds)
            {
                Names = names.ToArray();
                Flags = flags.ToArray();
                Sounds = sounds.ToArray();
            }
        }

        private readonly Bucket _outFlip, _outNon, _inFlip, _inNon;

        public string[] OutdoorFlippable => _outFlip.Names;
        public string[] OutdoorNonFlippable => _outNon.Names;
        public string[] IndoorFlippable => _inFlip.Names;
        public string[] IndoorNonFlippable => _inNon.Names;

        private TileNameTable(Bucket outFlip, Bucket outNon, Bucket inFlip, Bucket inNon)
        {
            _outFlip = outFlip;
            _outNon = outNon;
            _inFlip = inFlip;
            _inNon = inNon;
        }

        public static TileNameTable FromMes(MesFile mes)
        {
            var b = new[]
            {
                (names: new List<string>(), flags: new List<TileFlags>(), sounds: new List<int>()), // out flippable
                (names: new List<string>(), flags: new List<TileFlags>(), sounds: new List<int>()), // out non-flippable
                (names: new List<string>(), flags: new List<TileFlags>(), sounds: new List<int>()), // in flippable
                (names: new List<string>(), flags: new List<TileFlags>(), sounds: new List<int>()), // in non-flippable
            };

            foreach (var entry in mes.Entries)
            {
                int bucket = entry.Key / 100;
                if (bucket < 0 || bucket > 3) continue; // only keys 0..399 are terrain names
                (string name, TileFlags flags, int sound) = ParseEntry(entry.Value);
                b[bucket].names.Add(name);
                b[bucket].flags.Add(flags);
                b[bucket].sounds.Add(sound);
            }

            return new TileNameTable(
                new Bucket(b[0].names, b[0].flags, b[0].sounds),
                new Bucket(b[1].names, b[1].flags, b[1].sounds),
                new Bucket(b[2].names, b[2].flags, b[2].sounds),
                new Bucket(b[3].names, b[3].flags, b[3].sounds));
        }

        /// <summary>Resolves a terrain name for a tile sub-index, or null if out of range.</summary>
        public string Lookup(int num, int type, int flippable)
        {
            Bucket bucket = Select(type, flippable);
            return num >= 0 && num < bucket.Names.Length ? bucket.Names[num] : null;
        }

        /// <summary>Terrain <see cref="TileFlags"/> for a tile sub-index (movement blocking, sinkable water, …).</summary>
        public TileFlags FlagsOf(int num, int type, int flippable)
        {
            Bucket bucket = Select(type, flippable);
            return num >= 0 && num < bucket.Flags.Length ? bucket.Flags[num] : TileFlags.None;
        }

        /// <summary>Footstep sound id for a tile sub-index (0 if none / out of range).</summary>
        public int SoundOf(int num, int type, int flippable)
        {
            Bucket bucket = Select(type, flippable);
            return num >= 0 && num < bucket.Sounds.Length ? bucket.Sounds[num] : 0;
        }

        private Bucket Select(int type, int flippable) => type != 0
            ? (flippable != 0 ? _outFlip : _outNon)
            : (flippable != 0 ? _inFlip : _inNon);

        /// <summary>
        /// Global ordering index used to decide name order in blended tiles
        /// (<c>sub_4EB7D0</c>): outdoor-flippable names first, then outdoor
        /// non-flippable; returns -1 if the name is in neither.
        /// </summary>
        public int Order(string name)
        {
            string[] flip = _outFlip.Names, non = _outNon.Names;
            for (int i = 0; i < flip.Length; i++)
                if (string.Equals(flip[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            for (int i = 0; i < non.Length; i++)
                if (string.Equals(non[i], name, StringComparison.OrdinalIgnoreCase))
                    return flip.Length + i;
            return -1;
        }

        // Parses "name[/flags] [sound]" — engine load_tile_names (a_name.c:460–503).
        private static (string name, TileFlags flags, int sound) ParseEntry(string value)
        {
            int slash = value.IndexOf('/');
            if (slash < 0)
            {
                // "<name><sound>": first 3 chars are the name, the rest is the (optional) sound id.
                string trimmed = value.Trim();
                int snd = trimmed.Length > 3 ? Atoi(trimmed, 3) : 0;
                return (ExtractName(value), TileFlags.None, snd);
            }

            string name = ExtractName(value); // pre-slash, ≤3 chars (real tile codes are 3 chars)
            TileFlags flags = TileFlags.None;
            int j = slash + 1;
            for (; j < value.Length && value[j] != ' '; j++)
            {
                switch (value[j])
                {
                    case 's': flags |= TileFlags.Sinkable; break;
                    case 'b': flags |= TileFlags.Block; break;
                    case 'f': flags |= TileFlags.Block | TileFlags.Flyable; break;
                    case 'i': flags |= TileFlags.Slippery; break;
                    case 'n': flags |= TileFlags.Natural; break;
                    case 'p': flags |= TileFlags.Soundproof; break;
                }
            }

            return (name, flags, Atoi(value, j)); // sound = atoi of the text after the flags (skips the space)
        }

        private static string ExtractName(string value)
        {
            string s = value;
            int slash = s.IndexOf('/');
            if (slash >= 0) s = s.Substring(0, slash);
            s = s.Trim();
            return s.Length > 3 ? s.Substring(0, 3) : s;
        }

        // C-style atoi from an offset: skip leading whitespace, optional sign, then decimal digits.
        private static int Atoi(string s, int start)
        {
            int i = start, n = s.Length;
            while (i < n && (s[i] == ' ' || s[i] == '\t')) i++;
            int sign = 1;
            if (i < n && (s[i] == '+' || s[i] == '-'))
            {
                if (s[i] == '-') sign = -1;
                i++;
            }

            int val = 0;
            bool any = false;
            for (; i < n && s[i] >= '0' && s[i] <= '9'; i++)
            {
                val = val * 10 + (s[i] - '0');
                any = true;
            }

            return any ? sign * val : 0;
        }
    }
}
