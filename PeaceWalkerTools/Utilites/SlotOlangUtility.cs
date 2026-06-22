using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using PeaceWalkerTools.Olang;

namespace PeaceWalkerTools
{
    class SlotOlangUtility
    {
        public static string[] GetFiles()
            => Directory.GetFiles("SLOT", "*.olang").Select(x => x.Remove(22) + ".slot").Distinct().ToArray();

        public static void ReplaceText(string input)
        {
            using var workbook = new XLWorkbook(input);

            var textMap = workbook.Worksheets.ToDictionary(
                x => x.Name,
                x => x.RowsUsed().Skip(1)
                       .OrderBy(y => y.Cell(1).GetValue<double>())
                       .Select(y => y.Cell(4).GetText())
                       .ToList());

            var slots = Directory.GetFiles("SLOT", "*.olang")
                                 .Select(x => Path.GetFileName(x.Remove(22) + ".slot.xml"))
                                 .Distinct();

            foreach (var slot in slots)
            {
                var olangs = SerializationHelper<SlotSubItem[]>.Read(Path.Combine("SLOT", slot))
                             .Where(x => x.Extension == EntityExtensions.olang).ToList();

                var prefix = slot.RemoveExtension(2);
                foreach (var olangItem in olangs)
                {
                    var olangFilePath = string.Format(@"SLOT\{0}_{1:X6}.olang", prefix, olangItem.Key & 0xFFFFFF);
                    var olang = OlangFile.Read(olangFilePath);
                    var key = Path.GetFileNameWithoutExtension(olangFilePath);

                    if (!textMap.TryGetValue(key, out var texts)) continue;
                    if (texts.TrueForAll(x => x == null)) continue;

                    System.Diagnostics.Debug.Assert(olang.TextList.Count == texts.Count);
                    for (int i = 0; i < texts.Count; i++)
                        if (!string.IsNullOrEmpty(texts[i]))
                            olang.TextList[i].Text = texts[i].ReplaceWideCharacters();

                    olang.Write(olangFilePath);
                }
            }
        }

        public static void ReplaceWorksheetName(IXLWorkbook workbook)
        {
            foreach (var item in workbook.Worksheets
                .GroupBy(x => Path.GetFileNameWithoutExtension(x.Name))
                .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Name).ToList()))
            {
                var list = SerializationHelper<List<SlotSubItem>>.Read(string.Format(@"SLOT\{0}.slot.xml", item.Key))
                           .Where(x => x.Extension == EntityExtensions.olang).ToList();

                if (item.Value.Count == list.Count)
                    for (int i = 0; i < list.Count; i++)
                        item.Value[i].Name = string.Format("{0}_{1:X6}", item.Key, list[i].Key & 0xFFFFFF);
                else
                    for (int i = 0; i < 2; i++)
                        item.Value[i].Name = string.Format("{0}_{1:X6}", item.Key, list[i + 1].Key & 0xFFFFFF);
            }
            workbook.SaveAs("SlotOlang.xlsx");
        }
    }
}
