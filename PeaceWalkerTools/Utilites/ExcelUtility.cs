using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace PeaceWalkerTools
{
    public static class ExcelUtility
    {
        public static string GetText(this IXLCell cell)
        {
            if (cell == null || cell.Value.IsBlank) return null;
            var v = cell.Value;
            if (v.IsText)   return v.GetText();
            if (v.IsNumber) return v.GetNumber().ToString();
            return v.ToString();
        }

        private static Dictionary<string, string> WIDE_LETTERS = new Dictionary<string, string>
        {
            {"!","！"}, {"?","？"}, {"...","…"}, {"\r\n","\n"}, {":","："}, {".","．"},
        };
        private static string EXPRESSION_WIDE_LETTERS = @"(\!|\?|\.\.\.|\r\n|\:|\.)";

        public static string ReplaceWideCharacters(this string text)
            => Regex.Replace(text, EXPRESSION_WIDE_LETTERS, m => WIDE_LETTERS[m.Value]);

        private static Dictionary<string, string> SPECIAL_LETTERS = new Dictionary<string, string>
        {
            {"“","{*"}, {"”","*}"}, {"「","["}, {"」","]"}, {"『","{"}, {"』","}"},
        };
        private static Dictionary<string, string> SPECIAL_LETTERS_BACK = new Dictionary<string, string>
        {
            {@"{*","“"}, {@"*}","”"}, {@"[","「"}, {@"]","」"}, {@"{","『"}, {@"}","』"},
        };
        private static string EXPRESSION      = string.Format("({0})", string.Join("|", SPECIAL_LETTERS.Keys));
        private static string EXPRESSION_BACK = string.Format("({0})", string.Join("|", SPECIAL_LETTERS_BACK.Keys.Select(x => Regex.Escape(x))));

        static string MultipleReplace(this string text, string expression, Dictionary<string, string> replacements)
            => Regex.Replace(text, expression, m => replacements[m.Value]);

        public static void ReplaceSpecialLetter(string path, params int[] columns) => Replace(path, columns, EXPRESSION, SPECIAL_LETTERS);
        public static void ReplaceBackSpecialLetter(string path, params int[] columns) => Replace(path, columns, EXPRESSION_BACK, SPECIAL_LETTERS_BACK);

        private static void Replace(string path, int[] columns, string expression, Dictionary<string, string> dic)
        {
            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheets.First();
            int rowIndex = 2;
            while (true)
            {
                var row = sheet.Row(rowIndex++);
                if (row.Cell(1).Value.IsBlank) break;
                for (int i = 0; i < columns.Length; i++)
                {
                    var text = row.Cell(columns[i] + 1).GetText();
                    if (text != null) row.Cell(columns[i] + 1).Value = text.MultipleReplace(expression, dic);
                }
            }
            workbook.SaveAs(path);
        }
    }
}
