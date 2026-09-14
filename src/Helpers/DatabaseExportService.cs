using ScientificReviews.Bibtex;
using ScientificReviews.Forms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScientificReviews.Helpers
{
    public sealed class DatabaseExportService
    {
        public async Task<DatabaseExportRunResult> RunExportAsync(
            IList<BibtexEntry> entriesToExport,
            DatabaseExportOptions options,
            IList<string> exportColumns,
            IProgress<DatabaseExportProgress> progress,
            CancellationToken cancellationToken)
        {
            if (entriesToExport == null)
                throw new ArgumentNullException(nameof(entriesToExport));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.OutputFilePath))
                throw new ArgumentException("Output file path must not be empty.", nameof(options));

            return await Task.Run(() =>
            {
                int total = entriesToExport.Count;
                int completed = 0;

                try
                {
                    progress?.Report(new DatabaseExportProgress
                    {
                        Total = total,
                        Completed = 0,
                        StatusText = "Preparing export..."
                    });

                    string outputDirectory = Path.GetDirectoryName(options.OutputFilePath);
                    if (string.IsNullOrWhiteSpace(outputDirectory) == false)
                        Directory.CreateDirectory(outputDirectory);

                    if (options.Format == DatabaseExportFormat.Csv)
                        ExportCsv(entriesToExport, options, exportColumns, progress, cancellationToken, ref completed);
                    else
                        ExportBib(entriesToExport, options, exportColumns, progress, cancellationToken, ref completed);

                    return new DatabaseExportRunResult
                    {
                        Total = total,
                        Completed = completed,
                        Cancelled = false,
                        OutputFilePath = options.OutputFilePath,
                        Format = options.Format
                    };
                }
                catch (OperationCanceledException)
                {
                    return new DatabaseExportRunResult
                    {
                        Total = total,
                        Completed = completed,
                        Cancelled = true,
                        OutputFilePath = options.OutputFilePath,
                        Format = options.Format
                    };
                }
            }, cancellationToken);
        }

        private void ExportBib(
            IList<BibtexEntry> entries,
            DatabaseExportOptions options,
            IList<string> exportColumns,
            IProgress<DatabaseExportProgress> progress,
            CancellationToken cancellationToken,
            ref int completed)
        {
            BibtexExporter exporter = new BibtexExporter();

            using (StreamWriter writer = new StreamWriter(options.OutputFilePath, false, new UTF8Encoding(false)))
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    BibtexEntry entry = entries[index];
                    BibtexEntry projected = options.Mode == DatabaseExportMode.Normal
                        ? CloneEntry(entry)
                        : ProjectEntry(entry, exportColumns);

                    writer.Write(exporter.EntryToString(projected));
                    completed++;

                    ReportProgress(progress, entries.Count, completed, $"Exporting BibTeX... {completed}/{entries.Count}");
                }
            }
        }

        private void ExportCsv(
            IList<BibtexEntry> entries,
            DatabaseExportOptions options,
            IList<string> exportColumns,
            IProgress<DatabaseExportProgress> progress,
            CancellationToken cancellationToken,
            ref int completed)
        {
            string separator = string.IsNullOrEmpty(options.CsvSeparator) ? "," : options.CsvSeparator;

            using (StreamWriter writer = new StreamWriter(options.OutputFilePath, false, new UTF8Encoding(true)))
            {
                writer.Write(EscapeCsv("Key", separator));
                writer.Write(separator);
                writer.Write(EscapeCsv("Entry Type", separator));

                foreach (string column in exportColumns ?? Array.Empty<string>())
                {
                    writer.Write(separator);
                    writer.Write(EscapeCsv(column, separator));
                }

                writer.WriteLine();

                for (int index = 0; index < entries.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    BibtexEntry entry = entries[index];
                    writer.Write(EscapeCsv(entry?.Key ?? string.Empty, separator));
                    writer.Write(separator);
                    writer.Write(EscapeCsv(entry?.Type ?? string.Empty, separator));

                    foreach (string column in exportColumns ?? Array.Empty<string>())
                    {
                        writer.Write(separator);
                        writer.Write(EscapeCsv(GetTagValueIgnoreCase(entry, column), separator));
                    }

                    writer.WriteLine();
                    completed++;

                    ReportProgress(progress, entries.Count, completed, $"Exporting CSV... {completed}/{entries.Count}");
                }
            }
        }

        private void ReportProgress(IProgress<DatabaseExportProgress> progress, int total, int completed, string statusText)
        {
            progress?.Report(new DatabaseExportProgress
            {
                Total = total,
                Completed = completed,
                StatusText = statusText
            });
        }

        private BibtexEntry ProjectEntry(BibtexEntry entry, IEnumerable<string> orderedColumns)
        {
            List<BibtexTag> tags = new List<BibtexTag>();
            HashSet<string> added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string column in orderedColumns ?? Array.Empty<string>())
            {
                BibtexTag matchingTag = (entry?.Tags ?? Array.Empty<BibtexTag>())
                    .FirstOrDefault(tag => tag != null && string.Equals(tag.Key, column, StringComparison.OrdinalIgnoreCase));

                if (matchingTag == null || !added.Add(matchingTag.Key))
                    continue;

                tags.Add(new BibtexTag(matchingTag.Key, matchingTag.Value));
            }

            return new BibtexEntry
            {
                Key = entry?.Key,
                Type = entry?.Type,
                Tags = tags.ToArray()
            };
        }

        private BibtexEntry CloneEntry(BibtexEntry entry)
        {
            return new BibtexEntry
            {
                Key = entry?.Key,
                Type = entry?.Type,
                Tags = (entry?.Tags ?? Array.Empty<BibtexTag>())
                    .Where(tag => tag != null)
                    .Select(tag => new BibtexTag(tag.Key, tag.Value))
                    .ToArray()
            };
        }

        private string GetTagValueIgnoreCase(BibtexEntry entry, string key)
        {
            if (entry?.Tags == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            foreach (BibtexTag tag in entry.Tags)
            {
                if (tag != null && string.Equals(tag.Key, key, StringComparison.OrdinalIgnoreCase))
                    return tag.Value ?? string.Empty;
            }

            return string.Empty;
        }

        private string EscapeCsv(string input, string separator)
        {
            string value = input ?? string.Empty;
            bool mustQuote = value.Contains(separator)
                || value.Contains('"')
                || value.Contains('\r')
                || value.Contains('\n');

            if (mustQuote == false)
                return value;

            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            foreach (char c in value)
            {
                if (c == '"')
                    builder.Append("\"\"");
                else
                    builder.Append(c);
            }

            builder.Append('"');
            return builder.ToString();
        }
    }
}
