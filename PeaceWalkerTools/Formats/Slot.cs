using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Ionic.Zlib;

namespace PeaceWalkerTools
{
    class SlotData
    {
        public static void Unpack(string location, string outputFolder = null)
        {
            var keyPath = Path.Combine(location, "SLOT.KEY");
            var dataPath = Path.Combine(location, "SLOT.DAT");

            if (!File.Exists(dataPath))
            {
                Console.WriteLine("ERROR: SLOT.DAT not found in " + location);
                return;
            }

            Console.WriteLine("Reading: " + dataPath);

            if (outputFolder == null)
                outputFolder = Path.Combine(location, "SLOT");

            Directory.CreateDirectory(outputFolder);

            Hash hash;
            byte[] keys;

            using (var reader = new BinaryReader(File.OpenRead(keyPath)))
            {
                var hash0 = reader.ReadInt32();
                var hash1 = reader.ReadInt32();
                var hash2 = reader.ReadInt32();

                keys = reader.ReadBytes((int)reader.BaseStream.Length - 12);
                hash = new Hash(hash0, hash1, hash2);
            }

            var lastEnd = 0u;

            using (var keyReader = new BinaryReader(new MemoryStream(keys)))
            using (var dataReader = new BinaryReader(File.OpenRead(dataPath)))
            {
                while (keyReader.BaseStream.Position < keyReader.BaseStream.Length)
                {
                    var rawStart = keyReader.ReadUInt32();
                    var rawEnd = keyReader.ReadUInt32();

                    var start = (0x000FFFFF & rawStart) << 0xB;
                    var end = (0x000FFFFF & rawEnd) << 0xB;

                    lastEnd = end;

                    Debug.WriteLine(string.Format("{0:X3} {1:X3} ", rawStart >> 20, rawEnd >> 20));

                    var itemHash = keyReader.ReadInt32();

                    dataReader.BaseStream.Position = start;

                    if (end < start)
                    { continue; }

                    var raw = dataReader.ReadBytes((int)(end - start));

                    var hashCopy = hash;

                    DecryptionUtility.Decrypt(raw, ref hashCopy);
                    var unknown1 = BitConverter.ToInt32(raw, 0);
                    var unknown2 = BitConverter.ToInt32(raw, 4);

                    var compressedSize = BitConverter.ToInt32(raw, 8);
                    var uncompressedSize = BitConverter.ToInt32(raw, 12);

                    var data = new byte[compressedSize];
                    Buffer.BlockCopy(raw, 16, data, 0, compressedSize);

                    var uncompressed = ZlibStream.UncompressBuffer(data);

                    var t5 = BitConverter.ToInt32(uncompressed, 0);
                    var outputPath = Path.Combine(outputFolder, string.Format("{0:X8}_{1:X8}.slot", start, itemHash));

                    File.WriteAllBytes(outputPath, uncompressed);

                    Console.WriteLine("{0:X8} {1:X8} {2:X8} {3:X8} {4:X8} {5:X8}", itemHash, unknown1, unknown2, compressedSize, uncompressedSize, t5);
                }
            }
        }

        /// <summary>
        /// Patch one or more .slot files back into SLOT.DAT.
        /// location     = folder containing SLOT.DAT and SLOT.KEY (e.g. USRDIR)
        /// slotFolder   = folder containing the .slot files (default: location\SLOT)
        /// filter       = filenames to patch (e.g. "0B0D7800_A4FA3560.slot").
        ///                Pass null or empty to patch every slot file that exists on disk.
        /// </summary>
        public static void Pack(string location, string[] filter = null, string slotFolder = null)
        {
            var keyPath      = Path.Combine(location, "SLOT.KEY");
            var dataPath     = Path.Combine(location, "SLOT.DAT");
            var originalPath = Path.Combine(location, "SLOT.DAT.original");

            if (slotFolder == null)
                slotFolder = Path.Combine(location, "SLOT");

            Console.WriteLine("Pack: location  = " + location);
            Console.WriteLine("Pack: slotFolder = " + slotFolder);
            Console.WriteLine("Pack: filter     = " + (filter == null ? "<all>" : string.Join(", ", filter)));

            // Create a backup of the original SLOT.DAT the first time
            if (!File.Exists(originalPath))
            {
                Console.WriteLine("Creating backup: SLOT.DAT.original");
                File.Copy(dataPath, originalPath);
            }

            // Always start from the clean original so we never corrupt on repeated runs
            File.Copy(originalPath, dataPath, overwrite: true);
            Console.WriteLine("Restored SLOT.DAT from backup.");

            Hash hash;
            byte[] rawKeys;

            using (var reader = new BinaryReader(File.OpenRead(keyPath)))
            {
                var hash0 = reader.ReadInt32();
                var hash1 = reader.ReadInt32();
                var hash2 = reader.ReadInt32();

                rawKeys = reader.ReadBytes((int)reader.BaseStream.Length - 12);
                hash = new Hash(hash0, hash1, hash2);
            }

            // Build filter set (by filename only, case-insensitive)
            var filterSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (filter != null)
                foreach (var f in filter)
                    filterSet.Add(Path.GetFileName(f));

            var items = new List<SlotItemInfo>();

            using (var keyReader = new BinaryReader(new MemoryStream(rawKeys)))
            {
                while (keyReader.BaseStream.Position < keyReader.BaseStream.Length)
                {
                    var rawStart = keyReader.ReadUInt32();
                    var rawEnd   = keyReader.ReadUInt32();

                    var start = (0x000FFFFF & rawStart) << 0xB;
                    var end   = (0x000FFFFF & rawEnd)   << 0xB;

                    items.Add(new SlotItemInfo
                    {
                        Start = start,
                        End   = end,
                        Hash  = keyReader.ReadInt32()
                    });
                }
            }

            var patched = 0;

            Console.WriteLine("SLOT.KEY has " + items.Count + " entries.");

            using (var writer = new BinaryWriter(File.OpenWrite(dataPath)))
            {
                foreach (var item in items)
                {
                    var fileName   = string.Format("{0:X8}_{1:X8}.slot", item.Start, item.Hash);
                    var sourcePath = Path.Combine(slotFolder, fileName);

                    // Skip if not in filter (when a filter was given)
                    if (filterSet.Count > 0 && !filterSet.Contains(fileName))
                        continue;

                    Console.WriteLine("Matched filter: " + fileName);

                    // Skip if the file simply doesn't exist on disk
                    if (!File.Exists(sourcePath))
                    {
                        Console.WriteLine("  NOT FOUND on disk: " + sourcePath);
                        continue;
                    }

                    Console.WriteLine("  Found: " + sourcePath);
                    var data = File.ReadAllBytes(sourcePath);
                    Console.WriteLine("  Uncompressed size: " + data.Length + " bytes");

                    byte[] compressed;
                    if (!Compress(data, (int)item.Length - 16, out compressed))
                    {
                        Console.WriteLine("WARNING: Could not compress " + fileName + " to fit in " + (item.Length - 16) + " bytes — skipped.");
                        continue;
                    }
                    Console.WriteLine("  Compressed size: " + compressed.Length + " / max " + (item.Length - 16));

                    var raw = new byte[item.Length];

                    using (var rawWriter = new BinaryWriter(new MemoryStream(raw)))
                    {
                        rawWriter.Write(0x00100004);
                        rawWriter.Write(0x00000000);
                        rawWriter.Write(compressed.Length);
                        rawWriter.Write(data.Length);
                        rawWriter.Write(compressed);
                        rawWriter.Flush();
                    }

                    var hashCopy = hash;
                    DecryptionUtility.Decrypt(raw, ref hashCopy);
                    writer.BaseStream.Position = item.Start;
                    writer.Write(raw);

                    Console.WriteLine("Patched: " + fileName);
                    patched++;
                }
            }

            Console.WriteLine(string.Format("Done. {0} slot(s) patched into SLOT.DAT", patched));
        }

        private static bool Compress(byte[] data, int max, out byte[] compressed)
        {
            compressed = null;

            var level = CompressionLevel.Level4;

        ReTry:
            using (var tempMemoryStream = new MemoryStream())
            {
                using (var zipStream = new ZlibStream(tempMemoryStream, CompressionMode.Compress, level))
                {
                    zipStream.Write(data, 0, data.Length);
                }
                compressed = tempMemoryStream.ToArray();
            }

            if (compressed.Length > max)
            {
                if (level == CompressionLevel.BestCompression)
                {
                    return false;
                }

                level = (CompressionLevel)(level + 1);
                Debug.WriteLine(string.Format("- Retry {0}", level));

                goto ReTry;
            }

            return true;
        }

        class SlotItemInfo
        {
            public uint Start { get; set; }
            public uint Length { get { return End - Start; } }
            public uint End { get; set; }
            public int Hash { get; set; }
        }
    }

    class _ReverseSlot
    { 
        private static int DoSometing(byte[] keys, int a0, int a1)
        {
            int v0 = 0;
            int a3 = 0;


            var v1 = 0x684; //entityCount 

            var t2 = a0;
            var t0 = a0;

            var a2 = v1 - 1;
            var t1 = keys;

            if (a2 >= 0)
            {
                t2 &= 0x00ffffff;
                v0 = a2 >> 0x1f; // v0 = av
                v0 += a2;

                a1 = v0 >> 1; // a1=v0/2
                v1 = a1 << 2; // v1 = a1*4;
                v0 = a1 << 4; // v0 = a1*8;

                v0 -= v1; // v0 = a1*8 - a1*4;

                v0 = BitConverter.ToInt32(t1, v0);

                if (a0 != v0)
                {
                    a3 = 0;
                    t2 &= 0x00ffffff;

                    if (v0 < t2)
                    {

                    }
                    a2 = a1 - 1;

                    v1 = a2 + a3;
                    v0 = v1 >> 0x1f;

                    v0 += v1;
                    a1 = v0 >> 1;
                    v1 = a1 << 2;
                    v0 = a1 << 4;
                    v0 -= v1;

                    if (a2 < a3)
                    {

                    }
                    else
                    {
                        v0 = BitConverter.ToInt32(keys, v0);
                        if (t0 == v0)
                        {
                            return v0;
                        }
                        else
                        {
                            v0 &= 0x00ffffff;
                            if (v0 < t2)
                            {

                            }
                            else
                            {

                            }
                        }
                    }
                }
            }

            return -1;
        }

        public static void ProcessItem(byte[] data)
        {
            var a0 = 0;
            var a1 = 1;
            var a2 = 1;

            var t0 = 0;
            var t1 = 0;
            var t2 = 0;
            var t7 = int.MinValue;

            var s7 = t0;
            var s6 = t1;
            var s5 = t2;
            var s4 = t2;
            var s3 = a0;
            var s2 = a1;

            var v1 = 0;

            var s0 = a0 + 4;

            var sp_4 = 0;
            var sp_0 = 0;

            var t6 = BitConverter.ToInt32(data, a0);
            var v0 = t6 << 3;
            v0 = v0 + 0x803;

            MipsOP.Ins(ref v0, 0, 0, 0xb);
            v0 = v0 + a0;

            if (t6 > 0)
            {
                sp_0 = v0;
                v0 = a2 & 0x1000;
                var s1 = 0;
                var fp = 0x7f000000;
                sp_4 = v0;
                var t4 = BitConverter.ToInt32(data, s0);

            _0880559c:
                v0 = t4;
                MipsOP.Ins(ref v0, 0, 0, 0x18);

                if (fp == v0)
                {

                _08805668:
                    t7 = int.MinValue;
                    s0 = s0 + 8;
                    s1 = s1 + 1;

                    if (s1 < t6)
                    {
                        t4 = BitConverter.ToInt32(data, s0);
                        goto _0880559c;
                    }
                }
                else
                {
                    a1 = t4 | t7;

                    if (t4 == 0)
                    {
                        //goto _08805668;
                    }
                    else
                    {
                        v0 = s2 ^ 3;
                        a0 = BitConverter.ToInt32(data, s0 + 4);
                        t7 = sp_0;
                        s0 = s0 + 8;
                        v1 = BitConverter.ToInt32(data, s0 + 4);
                    }

                }
            }
        }
    }
}
