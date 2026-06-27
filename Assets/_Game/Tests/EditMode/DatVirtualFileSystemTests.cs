using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arcanum.Formats.Database;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Mount precedence + the loose-file overlay of <see cref="DatVirtualFileSystem"/>: among archives the
    /// first-mounted wins; loose-file directories override the archives (the modding/patch overlay).
    /// </summary>
    public sealed class DatVirtualFileSystemTests
    {
        private string _tmp;

        [SetUp]
        public void SetUp()
        {
            _tmp = Path.Combine(Path.GetTempPath(), "arcvfs_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmp);
        }

        [TearDown]
        public void TearDown()
        {
            if (_tmp != null && Directory.Exists(_tmp)) Directory.Delete(_tmp, recursive: true);
        }

        [Test]
        public void FirstMountedArchiveWins()
        {
            using var vfs = new DatVirtualFileSystem();
            vfs.Mount(DatArchive.Open(new MemoryStream(PlainArchive(("data/x.txt", "from-A")))));
            vfs.Mount(DatArchive.Open(new MemoryStream(PlainArchive(("data/x.txt", "from-B")))));
            Assert.That(Text(vfs.ReadAllBytes("data/x.txt")), Is.EqualTo("from-A"));
        }

        [Test]
        public void LooseFileOverridesArchive()
        {
            using var vfs = new DatVirtualFileSystem();
            vfs.Mount(DatArchive.Open(new MemoryStream(PlainArchive(("data/x.txt", "from-archive")))));
            WriteLoose("data/x.txt", "from-loose");
            vfs.MountDirectory(_tmp);

            Assert.That(vfs.Exists("data/x.txt"), Is.True);
            Assert.That(Text(vfs.ReadAllBytes("data/x.txt")), Is.EqualTo("from-loose"));
        }

        [Test]
        public void ReadsLooseOnlyFileNotInAnyArchive()
        {
            using var vfs = new DatVirtualFileSystem();
            vfs.Mount(DatArchive.Open(new MemoryStream(PlainArchive(("data/x.txt", "x")))));
            WriteLoose("mods/new.txt", "loose-only");
            vfs.MountDirectory(_tmp);
            Assert.That(Text(vfs.ReadAllBytes("mods/new.txt")), Is.EqualTo("loose-only"));
        }

        [Test]
        public void LaterLooseRootWins()
        {
            using var vfs = new DatVirtualFileSystem();
            string a = Path.Combine(_tmp, "a"), b = Path.Combine(_tmp, "b");
            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);
            File.WriteAllText(Path.Combine(a, "f.txt"), "root-a");
            File.WriteAllText(Path.Combine(b, "f.txt"), "root-b");
            vfs.MountDirectory(a);
            vfs.MountDirectory(b);
            Assert.That(Text(vfs.ReadAllBytes("f.txt")), Is.EqualTo("root-b"));
        }

        [Test]
        public void EnumerateUnionsLooseAndArchiveDeduped()
        {
            using var vfs = new DatVirtualFileSystem();
            vfs.Mount(DatArchive.Open(new MemoryStream(PlainArchive(
                ("data/x.txt", "x"), ("data/y.txt", "y"), ("other/z.txt", "z")))));
            WriteLoose("data/x.txt", "override"); // same path as an archive entry
            WriteLoose("data/new.txt", "new");
            vfs.MountDirectory(_tmp);

            var under = new List<string>(vfs.EnumerateFiles("data"));
            Assert.That(under, Does.Contain("data/x.txt"));
            Assert.That(under, Does.Contain("data/y.txt"));
            Assert.That(under, Does.Contain("data/new.txt"));
            Assert.That(under, Does.Not.Contain("other/z.txt"));
            Assert.That(under.FindAll(p => p == "data/x.txt").Count, Is.EqualTo(1), "deduped across loose + archive");
        }

        [Test]
        public void MissingPathThrows()
        {
            using var vfs = new DatVirtualFileSystem();
            vfs.Mount(DatArchive.Open(new MemoryStream(PlainArchive(("a.txt", "a")))));
            Assert.That(vfs.Exists("nope.txt"), Is.False);
            Assert.Throws<FileNotFoundException>(() => vfs.ReadAllBytes("nope.txt"));
        }

        // --- helpers ---

        private void WriteLoose(string virtualPath, string content)
        {
            string full = Path.Combine(_tmp, virtualPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, content);
        }

        private static string Text(byte[] bytes) => Encoding.ASCII.GetString(bytes);

        // Minimal stored-payload .dat archive (no compression) — enough to exercise the VFS layering.
        private static byte[] PlainArchive(params (string path, string content)[] entries)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

            var payloads = new byte[entries.Length][];
            var offsets = new long[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                payloads[i] = Encoding.ASCII.GetBytes(entries[i].content);
                offsets[i] = ms.Position;
                w.Write(payloads[i]);
            }

            long recordBlockStart = ms.Position + 4;
            w.Write((int)recordBlockStart);
            w.Write(entries.Length);
            int nameTableSize = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                byte[] name = Encoding.ASCII.GetBytes(entries[i].path);
                nameTableSize += name.Length;
                w.Write(name.Length);
                w.Write(name);
                w.Write(0);                              // padding
                w.Write((int)DatEntryFlags.Plain);
                w.Write(payloads[i].Length);             // uncompressed
                w.Write(payloads[i].Length);             // compressed (== uncompressed for plain)
                w.Write((uint)offsets[i]);               // absolute offset (baseOffset == 0)
            }

            long fileSize = ms.Position + 12;
            w.Write(DatArchive.MagicDat);
            w.Write(nameTableSize);
            w.Write((int)(fileSize - recordBlockStart)); // entryTableOffset
            w.Flush();
            return ms.ToArray();
        }
    }
}
