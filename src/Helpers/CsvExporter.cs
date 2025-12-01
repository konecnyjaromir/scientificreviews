using ScientificReviews.Bibtex;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScientificReviews.Helpers
{
    public static class CsvExporter
    {
        public static void ExportToCsv(DataTable table, string filePath, char separator = ';')
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            // Případně vytvoří cílovou složku
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // UTF8 s BOM kvůli Excelu
            using (var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            {
                // --- Hlavička ---
                bool first = true;
                foreach (DataColumn col in table.Columns)
                {
                    // Přeskočíme interní sloupec typu BibtexEntry
                    if (col.DataType == typeof(BibtexEntry) || string.Equals(col.ColumnName, "Entry", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!first)
                        writer.Write(separator);

                    writer.Write(EscapeCsv(col.ColumnName, separator));
                    first = false;
                }
                writer.WriteLine();

                // --- Řádky ---
                foreach (DataRow row in table.Rows)
                {
                    first = true;
                    foreach (DataColumn col in table.Columns)
                    {
                        if (col.DataType == typeof(BibtexEntry) || string.Equals(col.ColumnName, "Entry", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!first)
                            writer.Write(separator);

                        var value = row[col] == DBNull.Value ? string.Empty : row[col]?.ToString() ?? string.Empty;
                        writer.Write(EscapeCsv(value, separator));
                        first = false;
                    }
                    writer.WriteLine();
                }
            }
        }

        private static string EscapeCsv(string input, char separator)
        {
            if (input == null)
                return string.Empty;

            bool mustQuote = input.Contains(separator)
                             || input.Contains('"')
                             || input.Contains('\r')
                             || input.Contains('\n');

            if (mustQuote)
            {
                // Verdvojíme uvozovky
                var sb = new StringBuilder();
                sb.Append('"');
                foreach (char c in input)
                {
                    if (c == '"')
                        sb.Append("\"\"");
                    else
                        sb.Append(c);
                }
                sb.Append('"');
                return sb.ToString();
            }

            return input;
        }
    }
}
