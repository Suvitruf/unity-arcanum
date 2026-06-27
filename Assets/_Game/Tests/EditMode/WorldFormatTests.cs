using System;
using System.Collections.Generic;
using System.IO;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// World formats: the <c>.sec</c> placeholder + optional-section walk (<see cref="SectorReader.ReadSections"/>),
    /// the version-gated block mask, and the <c>AreaList</c> radius / <c>MapList</c> Type fixes.
    /// </summary>
    public sealed class WorldFormatTests
    {
        private const int V4 = 0xAA0004, V3 = 0xAA0003;

        // Minimal .sec: 0 lights, 4096 zero tiles, empty roof, placeholder + sections, optional block mask,
        // then the trailing object count.
        private static byte[] BuildSector(int version, int tileScripts = 0, int sectorScriptNum = 0,
                                          int townMap = 0, int aptitude = 0, int lightScheme = 0, byte[] blockMask = null, int objectCount = 0)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(0);                                 // light count
            for (int i = 0; i < 4096; i++) w.Write(0u); // tiles
            w.Write(1);                                 // roof: empty flag (non-zero → no grid)
            w.Write(version);                           // placeholder
            if (version != 0xAA0000)
            {
                w.Write(tileScripts);                  // tile-script count
                w.Write(new byte[tileScripts * 0x18]); // nodes (zeroed)
            }

            if (version >= 0xAA0002)
            {
                w.Write(0);
                w.Write(0);
                w.Write(sectorScriptNum);
            } // Script: hdr(8) + num

            if (version >= 0xAA0003)
            {
                w.Write(townMap);
                w.Write(aptitude);
                w.Write(lightScheme);
                w.Write(0);
                w.Write(0);
                w.Write(0); // sounds (flags / music / ambient)
            }

            if (version >= 0xAA0004) w.Write(blockMask ?? new byte[512]);
            w.Write(objectCount); // trailing object count (last 4 bytes)
            w.Flush();
            return ms.ToArray();
        }

        [Test]
        public void ReadSectionsParsesPlaceholderAndSections()
        {
            SectorReader.SectorSections s = SectorReader.ReadSections(
                BuildSector(V4, tileScripts: 2, sectorScriptNum: 42, townMap: 5, aptitude: -1, lightScheme: 3));
            Assert.That(s.Version, Is.EqualTo(V4));
            Assert.That(s.TileScriptCount, Is.EqualTo(2));
            Assert.That(s.SectorScriptNum, Is.EqualTo(42));
            Assert.That(s.TownMapInfo, Is.EqualTo(5));
            Assert.That(s.AptitudeAdjustment, Is.EqualTo(-1));
            Assert.That(s.LightScheme, Is.EqualTo(3));
            Assert.That(s.BlockMaskOffset, Is.GreaterThan(0)); // 0xAA0004 has a block section
        }

        [Test]
        public void OlderVersionHasNoBlockSection()
        {
            SectorReader.SectorSections s = SectorReader.ReadSections(BuildSector(V3));
            Assert.That(s.Version, Is.EqualTo(V3));
            Assert.That(s.BlockMaskOffset, Is.EqualTo(-1)); // pre-0xAA0004 → none
        }

        [Test]
        public void ReadBlockMaskRespectsVersionAndBits()
        {
            byte[] mask = new byte[512];
            mask[0] = 0b101; // word 0 bits 0 and 2 → tiles 0 and 2 blocked
            bool[] blocked = SectorReader.ReadBlockMask(BuildSector(V4, blockMask: mask));
            Assert.That(blocked[0], Is.True);
            Assert.That(blocked[1], Is.False);
            Assert.That(blocked[2], Is.True);

            // The bug fix: a pre-0xAA0004 sector has no block section → all walkable (not garbage).
            bool[] older = SectorReader.ReadBlockMask(BuildSector(V3));
            Assert.That(Array.TrueForAll(older, x => !x), Is.True);
        }

        [Test]
        public void AreaRadiusIsTilesTimes64()
        {
            AreaList areas = AreaList.FromMes(Mes(
                (1, "10, 20, 0, 0/Town/A town/Radius:5"),
                (0, "0, 0, 0, 0/Unknown/"),
                (2, "5, 5, 0, 0/Place/No radius")));
            Assert.That(areas.TryGet(1, out Area a1) && a1.RadiusTiles == 320, Is.True); // 5 × 64
            Assert.That(areas.TryGet(0, out Area a0) && a0.RadiusTiles == 0, Is.True);   // area 0 → 0
            Assert.That(areas.TryGet(2, out Area a2) && a2.RadiusTiles == 320, Is.True); // default 5 → 320
        }

        [Test]
        public void MapListParsesType()
        {
            MapList list = MapList.Read(Mes(
                (5000, "town, 100, 200, WorldMap: 1, Area: 3, Type: START_MAP"),
                (5001, "shop, 50, 60, Type: SHOPPING_MAP"),
                (5002, "cave, 0, 0")));
            Assert.That(list.TryGet(1, out MapListEntry m1) && m1.Type == MapType.StartMap && m1.WorldMap == 1 && m1.Area == 3, Is.True);
            Assert.That(list.TryGet(2, out MapListEntry m2) && m2.Type == MapType.ShoppingMap, Is.True);
            Assert.That(list.TryGet(3, out MapListEntry m3) && m3.Type == MapType.None, Is.True);
        }

        private static MesFile Mes(params (int k, string v)[] e)
        {
            var list = new List<KeyValuePair<int, string>>();
            foreach (var (k, v) in e) list.Add(new KeyValuePair<int, string>(k, v));
            return new MesFile(list);
        }
    }
}
