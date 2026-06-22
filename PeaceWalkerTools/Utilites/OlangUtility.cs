using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using PeaceWalkerTools.Olang;

namespace PeaceWalkerTools
{
    public static class OlangUtility
    {
        public static void ReplaceText(string sourcePath, string location)
        {
            using var workbook = new XLWorkbook(sourcePath);
            // Build map: sheetName -> ordered list of translated texts (col index 3, 0-based -> cell 4)
            var sheetMap = workbook.Worksheets.ToDictionary(
                x => x.Name,
                x => x.RowsUsed()
                       .Skip(1)    // skip header
                       .OrderBy(y => y.Cell(1).GetValue<double>())
                       .Select(y => y.Cell(4).GetText()?.Replace("\r\n", "\n"))
                       .ToList());

            var files = Directory.GetFiles(location, "*.olang");
            foreach (var file in files)
            {
                var olang = OlangFile.Read(file);
                var key = Path.GetFileNameWithoutExtension(file);
                if (!sheetMap.TryGetValue(key, out var korean)) continue;

                for (var i = 0; i < olang.TextList.Count; i++)
                    olang.TextList[i].Text = korean[i].ReplaceWideCharacters();

                olang.Write(file);
            }
        }

        public static void DumpLang(string path)
        {
            var raw = File.ReadAllBytes(path);
            var dic = new Dictionary<string, byte[]>();
            string lastFileName = null;
            var extBuffer = new System.Text.StringBuilder();

            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == (byte)'.')
                {
                    var j = i + 1;
                    extBuffer.Clear();
                    while (j < raw.Length && (raw[j] >= 'a' && raw[j] <= 'z' || raw[j] >= '0' && raw[j] <= '9'))
                        extBuffer.Append((char)raw[j++]);
                    var ext = extBuffer.ToString();

                    if (raw.Find(".olang", i) || raw.Find(".ypk", i))
                    {
                        var start = i; var end = i;
                        while (start > 1 && raw[start - 2] != 0) start--;
                        while (end < raw.Length - 1 && raw[++end] != 0) ;
                        lastFileName = System.Text.Encoding.ASCII.GetString(raw, start, end - start);
                        i = end;
                        while (i < raw.Length && raw[i] == 0) i++;
                        var length = System.BitConverter.ToInt32(raw, i);
                        i += 4;
                        while (i < raw.Length && raw[i] == 0) i++;
                        var section = new byte[length];
                        System.Buffer.BlockCopy(raw, i, section, 0, length);
                        dic[lastFileName] = section;
                        i += length;
                    }
                }
            }

            var loc = Path.GetDirectoryName(path);
            foreach (var item in dic)
                File.WriteAllBytes(Path.Combine(loc, item.Key), item.Value);
        }
    }
}
