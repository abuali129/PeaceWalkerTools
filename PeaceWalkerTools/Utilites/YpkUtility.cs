using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace PeaceWalkerTools
{
    public class YpkUtility
    {
        public static void ExportToExcel()
        {
            using var workbook = new XLWorkbook();
            foreach (var path in Directory.GetFiles("ypk", "*.ypk"))
            {
                var sheet = workbook.Worksheets.Add(Path.GetFileNameWithoutExtension(path));

                var ypk = YPK.Read(path);

                var index = 0;
                var rowIndex = 2;

                sheet.Row(1).Cell(1).Value = "Index";
                sheet.Row(1).Cell(2).Value = "SyncStart";
                sheet.Row(1).Cell(3).Value = "SyncEnd";
                sheet.Row(1).Cell(4).Value = "Unknown";
                sheet.Row(1).Cell(5).Value = "Japanese";
                sheet.Row(1).Cell(6).Value = "Korean";

                sheet.Column(5).Width = 60;
                sheet.Column(6).Width = 60;

                foreach (var entity in ypk.Entities)
                {
                    foreach (var line in entity.Lines)
                    {
                        var row = sheet.Row(rowIndex++);
                        row.Cell(1).Value = index;
                        row.Cell(2).Value = line.SyncStart;
                        row.Cell(3).Value = line.SyncEnd;
                        row.Cell(4).Value = line.Unknown;
                        row.Cell(5).Value = line.Text;
                    }

                    index++;
                }
            }
            workbook.SaveAs("ypk.xlsx");
        }

        public static void ReplaceText(string sourcePath, string location)
        {
            using var workbook = new XLWorkbook(sourcePath);

            var sheetMap = workbook.Worksheets.ToDictionary(x => x.Name, x => GetTextList(x));

            var files = Directory.GetFiles(location, "*.ypk");

            foreach (var file in files)
            {
                var olang = YPK.Read(file);
                var korean = sheetMap[Path.GetFileNameWithoutExtension(file)];

                var list = olang.Entities.SelectMany(x => x.Lines).ToList();

                if (korean.Count != list.Count)
                {
                }
                for (int i = 0; i < list.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(korean[i]))
                    {
                        list[i].Text = korean[i].ReplaceWideCharacters();
                    }
                }

                olang.Write(file);
            }
        }

        private static List<string> GetTextList(IXLWorksheet sheet)
        {
            return sheet.RowsUsed().Skip(1).Select(x => x.Cell(6).GetText()).ToList();
        }
    }
}
