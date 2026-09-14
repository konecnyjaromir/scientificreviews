using ScientificReviews.Bibtex;
using ScientificReviews.Forms;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ScientificReviews.Helpers
{
    public sealed class PdfExportService
    {
        private sealed class PdfExportJob
        {
            public string SourcePdfPath { get; set; }
            public string DestinationPath { get; set; }
            public string Doi { get; set; }
            public bool InjectDoiMetadata { get; set; }
        }

        public async Task<ExportPdfsRunResult> RunExportAsync(
            IList<BibtexEntry> entriesToExport,
            ExportPdfsRunOptions options,
            PdfMatchingService pdfMatchingService,
            PdfMatchingOptions pdfMatchingOptions,
            IProgress<ExportPdfsProgress> progress,
            CancellationToken cancellationToken)
        {
            if (entriesToExport == null)
                throw new ArgumentNullException(nameof(entriesToExport));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (pdfMatchingService == null)
                throw new ArgumentNullException(nameof(pdfMatchingService));
            if (pdfMatchingOptions == null)
                throw new ArgumentNullException(nameof(pdfMatchingOptions));

            return await Task.Run(() =>
            {
                int total = entriesToExport.Count;
                int completed = 0;
                int exported = 0;
                int skipped = 0;
                int injected = 0;
                string exportDirectory = options.PackToFolder
                    ? Path.Combine(options.OutputDirectory, "export")
                    : options.OutputDirectory;

                try
                {
                    Directory.CreateDirectory(exportDirectory);

                    string[] pdfFiles = pdfMatchingService.GetPdfFiles(pdfMatchingOptions);
                    var jobs = new List<PdfExportJob>();
                    var reservedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    progress?.Report(new ExportPdfsProgress
                    {
                        Total = total,
                        Completed = 0,
                        Exported = 0,
                        Skipped = 0,
                        Injected = 0,
                        StatusText = "Preparing export jobs..."
                    });

                    foreach (BibtexEntry entry in entriesToExport)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string sourcePdf = pdfMatchingService.FindPdfFile(entry, pdfMatchingOptions, pdfFiles);
                        if (string.IsNullOrWhiteSpace(sourcePdf))
                        {
                            int finishedSkipped = Interlocked.Increment(ref skipped);
                            int finishedCount = Interlocked.Increment(ref completed);
                            ReportProgress(progress, total, finishedCount, exported, finishedSkipped, injected, $"Preparing export... {finishedCount}/{total}");
                            continue;
                        }

                        string baseFileName = BuildExportPdfBaseName(entry, options.FileNameMode, options.CustomPattern);
                        string destination = BuildReservedExportDestinationPath(exportDirectory, baseFileName, sourcePdf, reservedDestinations);
                        jobs.Add(new PdfExportJob
                        {
                            SourcePdfPath = sourcePdf,
                            DestinationPath = destination,
                            Doi = BibtexTagService.GetTagValueIgnoreCase(entry, "doi"),
                            InjectDoiMetadata = options.InjectDoiMetadata
                        });
                    }

                    Parallel.ForEach(jobs, CreateParallelOptions(pdfMatchingOptions.ThreadCount, cancellationToken), job =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        bool isSameFile = string.Equals(
                            Path.GetFullPath(job.SourcePdfPath),
                            Path.GetFullPath(job.DestinationPath),
                            StringComparison.OrdinalIgnoreCase);

                        if (isSameFile == false)
                            File.Copy(job.SourcePdfPath, job.DestinationPath, false);

                        if (job.InjectDoiMetadata && string.IsNullOrWhiteSpace(job.Doi) == false)
                        {
                            InjectDoiIntoPdfMetadata(job.DestinationPath, job.Doi);
                            Interlocked.Increment(ref injected);
                        }

                        int finishedExported = Interlocked.Increment(ref exported);
                        int finishedCount = Interlocked.Increment(ref completed);
                        ReportProgress(progress, total, finishedCount, finishedExported, skipped, injected, $"Exporting PDFs... {finishedCount}/{total}");
                    });

                    return new ExportPdfsRunResult
                    {
                        Total = total,
                        Completed = completed,
                        Exported = exported,
                        Skipped = skipped,
                        Injected = injected,
                        Cancelled = false
                    };
                }
                catch (OperationCanceledException)
                {
                    return new ExportPdfsRunResult
                    {
                        Total = total,
                        Completed = completed,
                        Exported = exported,
                        Skipped = skipped,
                        Injected = injected,
                        Cancelled = true
                    };
                }
            }, cancellationToken);
        }

        private void ReportProgress(
            IProgress<ExportPdfsProgress> progress,
            int total,
            int completed,
            int exported,
            int skipped,
            int injected,
            string statusText)
        {
            progress?.Report(new ExportPdfsProgress
            {
                Total = total,
                Completed = completed,
                Exported = exported,
                Skipped = skipped,
                Injected = injected,
                StatusText = statusText
            });
        }

        private ParallelOptions CreateParallelOptions(int threadCount, CancellationToken cancellationToken)
        {
            return new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, threadCount),
                CancellationToken = cancellationToken
            };
        }

        private string BuildExportPdfBaseName(BibtexEntry entry, PdfExportFileNameMode mode, string customPattern)
        {
            string pattern;
            switch (mode)
            {
                case PdfExportFileNameMode.Key:
                    pattern = "<key>";
                    break;
                case PdfExportFileNameMode.Custom:
                    pattern = string.IsNullOrWhiteSpace(customPattern) ? "<key>" : customPattern;
                    break;
                default:
                    pattern = "<key>_<title>";
                    break;
            }

            string rendered = Regex.Replace(pattern, @"<(?<tag>[^>]+)>", match =>
            {
                string tagName = match.Groups["tag"].Value.Trim();
                string value;
                if (string.Equals(tagName, "key", StringComparison.OrdinalIgnoreCase))
                    value = entry.Key;
                else if (string.Equals(tagName, "type", StringComparison.OrdinalIgnoreCase))
                    value = entry.Type;
                else
                    value = BibtexTagService.GetTagValueIgnoreCase(entry, tagName);

                return SanitizeFileNamePart(BibtexUtils.RemoveLatex(value ?? string.Empty));
            });

            rendered = Regex.Replace(rendered, @"\s+", " ").Trim();
            rendered = rendered.Trim(' ', '.', '_', '-');

            if (string.IsNullOrWhiteSpace(rendered))
                rendered = SanitizeFileNamePart(entry.Key);

            if (string.IsNullOrWhiteSpace(rendered))
                rendered = "record";

            return rendered;
        }

        private string SanitizeFileNamePart(string value)
        {
            string sanitized = value ?? string.Empty;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();
            return sanitized;
        }

        private string BuildReservedExportDestinationPath(string outputDirectory, string baseFileName, string sourcePdfPath, HashSet<string> reservedDestinations)
        {
            string sourceFullPath = Path.GetFullPath(sourcePdfPath);

            for (int index = 1; ; index++)
            {
                string candidateFileName = index == 1 ? baseFileName + ".pdf" : $"{baseFileName}_{index}.pdf";
                string candidate = Path.Combine(outputDirectory, candidateFileName);
                string candidateFullPath = Path.GetFullPath(candidate);

                bool isSameFile = string.Equals(candidateFullPath, sourceFullPath, StringComparison.OrdinalIgnoreCase);
                bool exists = File.Exists(candidateFullPath);
                bool alreadyReserved = reservedDestinations.Contains(candidateFullPath);

                if ((isSameFile || exists == false) && alreadyReserved == false)
                {
                    reservedDestinations.Add(candidateFullPath);
                    return candidateFullPath;
                }
            }
        }

        private void InjectDoiIntoPdfMetadata(string fileName, string doi)
        {
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(doi))
                return;

            byte[] originalBytes = File.ReadAllBytes(fileName);
            Encoding encoding = Encoding.GetEncoding("ISO-8859-1");
            string pdfText = encoding.GetString(originalBytes);

            Match trailerMatch = Regex.Matches(pdfText, @"trailer\s*<<(?<dict>.*?)>>\s*startxref\s*(?<xref>\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                .Cast<Match>()
                .LastOrDefault();
            if (trailerMatch == null)
                return;

            string trailerDict = trailerMatch.Groups["dict"].Value;
            int prevXref = int.Parse(trailerMatch.Groups["xref"].Value, CultureInfo.InvariantCulture);

            Match rootMatch = Regex.Match(trailerDict, @"/Root\s+(?<root>\d+\s+\d+\s+R)", RegexOptions.IgnoreCase);
            Match sizeMatch = Regex.Match(trailerDict, @"/Size\s+(?<size>\d+)", RegexOptions.IgnoreCase);
            Match infoMatch = Regex.Match(trailerDict, @"/Info\s+(?<info>\d+)\s+\d+\s+R", RegexOptions.IgnoreCase);
            Match idMatch = Regex.Match(trailerDict, @"/ID\s*\[[^\]]+\]", RegexOptions.IgnoreCase);

            if (rootMatch.Success == false || sizeMatch.Success == false)
                return;

            int newObjectNumber = int.Parse(sizeMatch.Groups["size"].Value, CultureInfo.InvariantCulture);
            string existingInfoBody = string.Empty;
            if (infoMatch.Success)
            {
                int infoObjectNumber = int.Parse(infoMatch.Groups["info"].Value, CultureInfo.InvariantCulture);
                Match infoObjectMatch = Regex.Match(pdfText, $@"(?s)\b{infoObjectNumber}\s+0\s+obj\s*<<(.*?)>>\s*endobj");
                if (infoObjectMatch.Success)
                    existingInfoBody = infoObjectMatch.Groups[1].Value;
            }

            existingInfoBody = Regex.Replace(existingInfoBody, @"(?is)/DOI\s*(\((?:\\.|[^\\)])*\)|<[^>]*>)", string.Empty).Trim();

            string escapedDoi = EscapePdfLiteralString(doi.Trim());
            string infoObject = $"{newObjectNumber} 0 obj\n<<\n{existingInfoBody}\n/DOI ({escapedDoi})\n>>\nendobj\n";
            int objectOffset = originalBytes.Length;
            string xref = $"xref\n{newObjectNumber} 1\n{objectOffset:D10} 00000 n \n";
            int xrefOffset = objectOffset + encoding.GetByteCount(infoObject);

            string trailer = $"trailer\n<< /Size {newObjectNumber + 1} /Root {rootMatch.Groups["root"].Value} /Info {newObjectNumber} 0 R";
            if (idMatch.Success)
                trailer += " " + idMatch.Value;

            trailer += $" /Prev {prevXref} >>\nstartxref\n{xrefOffset}\n%%EOF";

            byte[] appendedBytes = encoding.GetBytes(infoObject + xref + trailer);
            File.WriteAllBytes(fileName, originalBytes.Concat(appendedBytes).ToArray());
        }

        private string EscapePdfLiteralString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
