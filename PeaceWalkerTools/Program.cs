using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using PeaceWalkerTools.Olang;

namespace PeaceWalkerTools
{
    partial class Program
    {
        static void Main(string[] args)
        {
            var iniFile = new InitializationFile("PeaceWalkerTools.ini");

            Settings.Working = iniFile["Global"]["Working"] ?? ".";
            Settings.SourceFolder = iniFile["Global"]["SourceLocation"];
            Settings.SourceUserFolder = Path.Combine(iniFile["Global"]["SourceLocation"], "USRDIR");
            Settings.SourceSystemFolder = Path.Combine(iniFile["Global"]["SourceLocation"], "SYSDIR");
            Settings.InstallFolder = iniFile["Global"]["InstallLocation"];
            Settings.TranslationFolder = iniFile["Global"]["TranslationFolder"];

            if (Debugger.IsAttached)
            {
                Test();
            }
            else
            {
                Process(args);
            }
        }

        private static void Process(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("PeaceWalkerTools — drag a file onto the exe, or pass paths as arguments.");
                Console.WriteLine();
                Console.WriteLine("Supported files:");
                Console.WriteLine("  *.dar              Unpack DAR archive  → <name>_dar\\  +  <name>.dar.inf");
                Console.WriteLine("  *.qar              Unpack QAR archive  → <name>_qar\\  +  <name>.qar.inf");
                Console.WriteLine("  *.txp              Extract TXP textures → PNG files next to the .txp");
                Console.WriteLine("  *.dar.inf          Repack DAR from manifest");
                Console.WriteLine("  *.qar.inf          Repack QAR from manifest");
                Console.WriteLine("  STAGEDAT.PDT       Decrypt & extract all stage files → STAGEDAT_pdt\\");
                Console.WriteLine("  SLOT.DAT           Decrypt & extract all slot files  → SLOT\\  (needs SLOT.KEY in same folder)");
                Console.WriteLine("  *.slot             Unpack a single .slot file → individual sub-files + .slot.xml manifest");
                Console.WriteLine("  *.slot.xml         Repack .slot from manifest, then patch back into SLOT.DAT");
                Console.ReadLine();
                return;
            }

            foreach (var path in args)
            {
                if (File.Exists(path))
                {
                    var dir  = Path.GetDirectoryName(path);
                    var name = Path.GetFileNameWithoutExtension(path);
                    var ext  = Path.GetExtension(path).ToLowerInvariant();
                    var fullName = Path.GetFileName(path).ToUpperInvariant();

                    switch (ext)
                    {
                        case ".dar":
                            DAR.Unpack(path);
                            break;

                        case ".qar":
                            QAR.Unpack(path);
                            break;

                        case ".txp":
                            TXP.Unpack(path);
                            break;

                        case ".inf":
                            Pack(path);
                            break;

                        case ".olang":
                            DumpOlang(path);
                            break;

                        case ".pdt":
                        {
                            var outDir = Path.Combine(dir, name + "_pdt");
                            Console.WriteLine("Extracting STAGEDAT.PDT → " + outDir);
                            StageDataFile.Read(path, outDir);
                            Console.WriteLine("Done.");
                            break;
                        }

                        case ".dat" when fullName == "SLOT.DAT":
                        {
                            var keyPath = Path.Combine(dir, "SLOT.KEY");
                            if (!File.Exists(keyPath))
                            {
                                Console.WriteLine("ERROR: SLOT.KEY not found next to SLOT.DAT — place both files in the same folder.");
                                break;
                            }
                            var outDir = Path.Combine(dir, "SLOT");
                            Console.WriteLine("Extracting SLOT.DAT → " + outDir);
                            SlotData.Unpack(dir, outDir);
                            Console.WriteLine("Done. Extracted to: " + outDir);
                            break;
                        }

                        case ".slot" when !fullName.EndsWith(".SLOT.XML"):
                        {
                            SlotFile.Unpack(path);
                            break;
                        }

                        case ".xml" when fullName.EndsWith(".SLOT.XML"):
                        {
                            // Step 1: repack the .slot file from the manifest
                            try
                            {
                                SlotFile.Pack(path);
                            }
                            catch (FileNotFoundException ex)
                            {
                                Console.WriteLine("ERROR: " + ex.Message);
                                Console.WriteLine("All sub-files (olang, etc.) must be in the same folder as the .slot.xml.");
                                break;
                            }

                            // The repacked .slot sits next to the .xml (remove the .xml extension)
                            var slotPath   = path.Substring(0, path.Length - 4); // strip .xml
                            var slotName   = Path.GetFileName(slotPath);
                            var slotFolder = Path.GetDirectoryName(slotPath);

                            // Step 2: auto-detect the USRDIR (parent of the SLOT folder)
                            var usrDir = Path.GetDirectoryName(slotFolder);
                            var datPath = Path.Combine(usrDir, "SLOT.DAT");
                            var keyPath = Path.Combine(usrDir, "SLOT.KEY");

                            if (!File.Exists(datPath) || !File.Exists(keyPath))
                            {
                                Console.WriteLine("Repacked .slot written. Could not find SLOT.DAT + SLOT.KEY in: " + usrDir);
                                Console.WriteLine("Patch SLOT.DAT manually by placing the .slot in the SLOT\\ folder next to SLOT.DAT.");
                                break;
                            }

                            // Step 3: patch the repacked .slot back into SLOT.DAT
                            Console.WriteLine("Patching into SLOT.DAT in: " + usrDir);
                            SlotData.Pack(usrDir, new[] { slotName }, slotFolder);
                            Console.ReadLine();
                            break;
                        }

                        default:
                            Console.WriteLine("Unrecognised file: " + path);
                            break;
                    }
                }
                else
                {
                    Console.WriteLine("File not found: " + path);
                }
            }
        }

        private static void Pack(string path)
        {
            var ext = Path.GetExtension(Path.GetFileNameWithoutExtension(path));

            switch (ext)
            {
                case ".dar":
                {
                    DAR.Pack(path);
                    break;
                }
                case ".qar":
                {
                    QAR.Pack(path);
                    break;
                }
                default:
                break;
            }
        }

        private static void DumpOlang(string path)
        {
            var olang = OlangFile.Read(path);
            var outPath = path + ".tsv";

            Console.WriteLine("Dumping: " + Path.GetFileName(path));
            Console.WriteLine("  Unique strings: " + olang.TextList.Count);

            var sb = new StringBuilder();
            sb.AppendLine("Index\tBytes\tText");

            for (int i = 0; i < olang.TextList.Count; i++)
            {
                var text = olang.TextList[i].Text ?? "";
                var byteLen = Encoding.UTF8.GetByteCount(text);
                sb.AppendLine(string.Format("{0}\t{1}\t{2}", i, byteLen, text));
                Console.WriteLine("  [{0:D3}] {1,4} bytes  {2}", i, byteLen,
                    text.Length > 60 ? text.Substring(0, 60) + "…" : text);
            }

            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            Console.WriteLine("Saved: " + outPath);
        }
    }
}
