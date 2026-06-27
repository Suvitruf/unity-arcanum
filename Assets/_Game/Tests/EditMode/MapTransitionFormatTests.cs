using System.IO;
using System.Text;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Exercises the map-transition readers (<see cref="JumpPointReader"/>,
    /// <see cref="MapPropertiesReader"/>, <see cref="MapList"/>) against bytes captured
    /// byte-exact from the shipped game data (<c>modules/Arcanum.dat</c>):
    /// <c>maps/bates mansion lev 1/map.jmp</c> + <c>map.prp</c> and <c>rules/MapList.mes</c>.
    /// </summary>
    public sealed class MapTransitionFormatTests
    {
        [Test]
        public void ReadsJumpPoints_BatesMansionLev1()
        {
            // Golden: 6 jump points over tiles (109..110, 92..94), all → map 1 @ (61976, 65664).
            byte[] jmp = BuildJmp(
                (0, Loc(110, 92), 1, Loc(61976, 65664)),
                (0, Loc(110, 93), 1, Loc(61976, 65664)),
                (0, Loc(110, 94), 1, Loc(61976, 65664)),
                (0, Loc(109, 92), 1, Loc(61976, 65664)),
                (0, Loc(109, 93), 1, Loc(61976, 65664)),
                (0, Loc(109, 94), 1, Loc(61976, 65664)));

            var points = JumpPointReader.Read(jmp);

            Assert.That(points.Count, Is.EqualTo(6));
            Assert.That(points[0].SrcX, Is.EqualTo(110));
            Assert.That(points[0].SrcY, Is.EqualTo(92));
            Assert.That(points[0].DstMap, Is.EqualTo(1));
            Assert.That(points[0].DstX, Is.EqualTo(61976));
            Assert.That(points[0].DstY, Is.EqualTo(65664));
            Assert.That(points[5].SrcX, Is.EqualTo(109));
            Assert.That(points[5].SrcY, Is.EqualTo(94));
        }

        [Test]
        public void ReadsEmptyJumpTable()
        {
            byte[] jmp = BuildJmp(); // count == 0, the common case
            Assert.That(JumpPointReader.Read(jmp), Is.Empty);
        }

        [Test]
        public void RejectsCorruptJumpCount()
        {
            byte[] jmp = new byte[4];
            jmp[0] = 0xFF; jmp[1] = 0xFF; jmp[2] = 0xFF; jmp[3] = 0x7F; // huge count, no records
            Assert.That(() => JumpPointReader.Read(jmp), Throws.TypeOf<DatFormatException>());
        }

        [Test]
        public void ReadsMapProperties_BatesMansionLev1()
        {
            // Golden raw bytes from map.prp: base_terrain=2, width=192, height=192 (3×3 sectors).
            byte[] prp = HexToBytes("0200000000006cbec000000000000000c000000000000000");
            var props = MapPropertiesReader.Read(prp);

            Assert.That(props.BaseTerrainType, Is.EqualTo(2));
            Assert.That(props.Width, Is.EqualTo(192));
            Assert.That(props.Height, Is.EqualTo(192));
            Assert.That(props.SectorsWide, Is.EqualTo(3));
            Assert.That(props.SectorsHigh, Is.EqualTo(3));
        }

        [Test]
        public void ResolvesMapList_FromMesEntries()
        {
            // Mirrors the real rules/MapList.mes head: keyed from 5000, comma-separated.
            var mes = MesReader.Read(
                "{5000}{Arcanum1-024-fixed, 92958,82592, Type: START_MAP, WorldMap: 0}\n" +
                "{5001}{BessieToonesMine-fixed, 96, 126, WorldMap: 0, Area: 1}\n" +
                "{5011}{Bates Mansion Lev 1, 104, 92, WorldMap: 0, Area: 21}\n");

            var list = MapList.Read(mes);

            // The run stops at the first gap (5002), so only the first two are consecutive.
            Assert.That(list.Entries.Count, Is.EqualTo(2));

            Assert.That(list.TryGet(1, out var first), Is.True);
            Assert.That(first.Name, Is.EqualTo("Arcanum1-024-fixed"));
            Assert.That(first.X, Is.EqualTo(92958));
            Assert.That(first.Y, Is.EqualTo(82592));
            Assert.That(first.WorldMap, Is.EqualTo(0));

            Assert.That(list.GetName(2), Is.EqualTo("BessieToonesMine-fixed"));
            Assert.That(list.Entries[1].Area, Is.EqualTo(1));

            Assert.That(list.TryGetByName("arcanum1-024-fixed", out var byName), Is.True); // case-insensitive
            Assert.That(byName.MapId, Is.EqualTo(1));
        }

        private static long Loc(int x, int y) => (x & 0xFFFFFFFFL) | ((long)y << 32);

        private static byte[] BuildJmp(params (uint flags, long src, int dstMap, long dst)[] points)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.ASCII);
            w.Write(points.Length);
            foreach (var p in points)
            {
                w.Write(p.flags);
                w.Write(0);            // padding_4
                w.Write(p.src);
                w.Write(p.dstMap);
                w.Write(0);            // padding_14
                w.Write(p.dst);
            }
            w.Flush();
            return ms.ToArray();
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return b;
        }
    }
}
