using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;

namespace PeaceWalkerTools
{
    class Loadout
    {
        public static void Unpack()
        {

            var location = @"E:\Games\Metal Gear Solid\Metal Gear Solid - Peace Walker\Metal Gear Solid Peace Walker GEN-D3\PSP_GAME\SYSDIR";

            var path = Path.Combine(location, "EBOOT.BIN");

            Unpack(path);

        }

        private static int ADDRESS_OFFSET = 0xA0;

        private static int WEAPON_TEXT_START = 0x0035B028;
        private static int WEAPON_TEXT_END = 0x003737A8;

        private static int WEAPON_META_START = 0x003737A8;
        private static int WEAPON_META_END = 0x0037DA9C;
        private static int WEAPON_META_OFFSET = 0x74;



        private static int ITEM_TEXT_START = 0x0037FC1C;
        private static int ITEM_TEXT_END = 0x0038E6C0;

        private static int ITEM_META_START = 0x003E2D40;
        private static int ITEM_META_END = 0x003F8494;
        private static int ITEM_META_OFFSET = 0xAC;

        private static SectionParameters PARAMETERS_WEAPON = new SectionParameters
        {
            TextStart = WEAPON_TEXT_START,
            TextEnd = WEAPON_TEXT_END,

            MetaStart = WEAPON_META_START,
            MetaEnd = WEAPON_META_END,
            MetaOffset = WEAPON_META_OFFSET
        };

        private static SectionParameters PARAMETERS_ITEM = new SectionParameters
        {
            TextStart = ITEM_TEXT_START,
            TextEnd = ITEM_TEXT_END,

            MetaStart = ITEM_META_START,
            MetaEnd = ITEM_META_END,
            MetaOffset = ITEM_META_OFFSET
        };


        public static void Unpack(string path)
        {
            var data = File.ReadAllBytes(path + ".original");
            var location = Path.GetDirectoryName(path);

            Do(location, data, "Weapon.xlsx", PARAMETERS_WEAPON);

            Do(location, data, "Item.xlsx", PARAMETERS_ITEM);

        }

        private static void Do(string location, byte[] data, string excelName, SectionParameters section)
        {
            var excelPath = Path.Combine(location, excelName);
            var textEntities = LoadEntity(data, section);


            //CreateWorkbook(excelPath, textEntities);

            var map = textEntities.ToDictionary(x => x.Offset);

            ReplaceFromWorkbook(excelPath, map);

            Update(Path.Combine(location, "EBOOT.BIN"), data, map, section);
        }

        private static void ReplaceFromWorkbook(string excelPath, Dictionary<int, ItemEntity> map)
        {
            var cr = new string(new char[] { (char)0x0d, });

            using var wb = new XLWorkbook(excelPath);
            var sheet = wb.Worksheets.First();

            var rowIndex = 2;

            while (true)
            {
                var row = sheet.Row(rowIndex++);

                if (row.Cell(1).Value.IsBlank)
                { break; }

                var key = (int)row.Cell(1).GetValue<double>();

                var text = row.Cell(3).GetText().Replace(cr, string.Empty);
                if (text.EndsWith(" "))
                {
                    text = text.TrimEnd();
                }

                map[key].Text = text;

            }
        }

        private static List<ItemEntity> LoadEntity(byte[] data, SectionParameters section)
        {
            var textEntities = new List<ItemEntity>();

            var first = 0;

            for (int i = section.TextStart; i < section.TextEnd; i++)
            {
                if (data[i] == 0)
                {
                    if (first > 0)
                    {
                        var start = i - first;
                        var text = Encoding.UTF8.GetString(data, start, first);
                        var offset = start - section.TextStart;

                        textEntities.Add(new ItemEntity
                        {
                            Offset = offset,
                            Text = text
                        });
                        first = 0;
                    }

                    continue;
                }
                first++;
            }

            return textEntities;
        }

        private static void CreateWorkbook(string path, List<ItemEntity> textEntities)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Sheet");

            sheet.Column(2).Width = 71.4;
            sheet.Column(2).Style.Alignment.WrapText = true;
            sheet.Column(3).Width = 71.4;
            sheet.Column(3).Style.Alignment.WrapText = true;

            var rowIndex = 2;

            foreach (var item in textEntities)
            {
                var row = sheet.Row(rowIndex++);
                row.Cell(1).Value = item.Offset;
                row.Cell(2).Value = item.Text;
            }

            workbook.SaveAs(path);
        }

        private static void Update(string path, byte[] data, Dictionary<int, ItemEntity> map, SectionParameters section)
        {
            var textOffset = (section.TextStart - ADDRESS_OFFSET);

            var metaList = new List<MetaEntity>();

            for (int i = section.MetaStart; i < section.MetaEnd; i += section.MetaOffset)
            {
                var meta = new MetaEntity { Offset = i };
                metaList.Add(meta);

                for (int j = 0; j < section.MetaOffset; j += 4)
                {
                    var value = GetTextReference(data, i + j, section.TextStart, section.TextEnd);

                    if (value != -1)
                    {
                        meta.Values.Add(new Tuple<int, int>(j, value - textOffset));
                    }
                }
            }


            for (int i = section.TextStart; i < section.TextEnd; i++)
            {
                data[i] = 0;
            }

            var offset = 0;

            foreach (var item in map.Values.OrderBy(x => x.Offset))
            {
                var rawText = Encoding.UTF8.GetBytes(item.Text);

                item.NewOffset = offset;

                Buffer.BlockCopy(rawText, 0, data, section.TextStart + offset, rawText.Length);
                data[section.TextStart + offset + rawText.Length] = 0;

                offset += rawText.Length + 1;

                if (offset > section.TextEnd)
                {
                    throw new ArgumentOutOfRangeException();
                }
            }



            foreach (var item in metaList)
            {
                foreach (var pair in item.Values)
                {
                    var key = pair.Item2;
                    var newValue = map[key].NewOffset + textOffset;
                    Buffer.BlockCopy(BitConverter.GetBytes(newValue), 0, data, item.Offset + pair.Item1, 4);
                }
            }

            File.WriteAllBytes(path, data);
        }




        private static int GetTextReference(byte[] data, int offset, int start, int end)
        {
            var value = BitConverter.ToInt32(data, offset);

            if (value >= start - ADDRESS_OFFSET && value < end - ADDRESS_OFFSET)
            {
                return value;
            }
            return -1;
        }
    }


    class ItemEntity
    {
        public int Offset { get; set; }
        public int NewOffset { get; set; }
        public string Text { get; set; }
    }

    class MetaEntity
    {
        public MetaEntity()
        {
            Values = new List<Tuple<int, int>>();
        }
        public int Offset { get; set; }

        public List<Tuple<int, int>> Values { get; private set; }

    }


    class SectionParameters
    {
        public int TextStart { get; set; }
        public int TextEnd { get; set; }
        public int MetaStart { get; set; }
        public int MetaEnd { get; set; }
        public int MetaOffset { get; set; }
    }
}
