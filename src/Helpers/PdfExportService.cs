using ScientificReviews.Bibtex;
using ScientificReviews.Forms;
using ScientificReviews.Logs;
using iText.Kernel.Pdf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ScientificReviews.Helpers
{
    public sealed class PdfExportService
    {
        private const int MaxDestinationPathLength = 240;
        private const int MaxFileNameLength = 120;

        private sealed class MetadataInjectionResult
        {
            public bool Injected { get; set; }
            public string ErrorMessage { get; set; }
        }

        private sealed class PdfExportJob
        {
            public string EntryKey { get; set; }
            public string SourcePdfPath { get; set; }
            public string DestinationPath { get; set; }
            public string Doi { get; set; }
            public string Eprint { get; set; }
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
                int errors = 0;
                int injected = 0;
                string lastErrorMessage = null;
                string exportDirectory = options.PackToFolder
                    ? Path.Combine(options.OutputDirectory, "export")
                    : options.OutputDirectory;
                string metadataTempDirectory = Path.Combine(exportDirectory, "_metadata_tmp_" + Guid.NewGuid().ToString("N"));
                string metadataInjectionAvailabilityError = null;
                bool canInjectPdfMetadata = options.InjectDoiMetadata && IsITextMetadataInjectionAvailable(out metadataInjectionAvailabilityError);

                try
                {
                    Directory.CreateDirectory(exportDirectory);
                    if (canInjectPdfMetadata)
                        Directory.CreateDirectory(metadataTempDirectory);

                    if (options.InjectDoiMetadata && !canInjectPdfMetadata)
                    {
                        string warningMessage = "PDF metadata injection is unavailable. Install/restore iText Bouncy Castle adapter. " + metadataInjectionAvailabilityError;
                        LogExportDetail("<global>", "ERROR", warningMessage);
                        lastErrorMessage = warningMessage;
                        errors++;
                    }

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
                            LogExportDetail(entry, "SKIP", "No valid source PDF was found for this record.");
                            int finishedSkipped = Interlocked.Increment(ref skipped);
                            int finishedCount = Interlocked.Increment(ref completed);
                            ReportProgress(progress, total, finishedCount, exported, finishedSkipped, injected, $"Preparing export... {finishedCount}/{total}");
                            continue;
                        }

                        if (!File.Exists(sourcePdf))
                        {
                            string sourceError = $"Source PDF path is invalid or unavailable: {sourcePdf}";
                            LogExportDetail(entry, "ERROR", sourceError);
                            lastErrorMessage = sourceError;
                            Interlocked.Increment(ref errors);
                            int finishedSkipped = Interlocked.Increment(ref skipped);
                            int finishedCount = Interlocked.Increment(ref completed);
                            ReportProgress(progress, total, finishedCount, exported, finishedSkipped, injected, $"Preparing export... {finishedCount}/{total}");
                            continue;
                        }

                        string baseFileName = BuildExportPdfBaseName(entry, options.FileNameMode, options.CustomPattern);
                        string destination;
                        string destinationError;
                        if (!TryBuildReservedExportDestinationPath(exportDirectory, baseFileName, sourcePdf, reservedDestinations, out destination, out destinationError))
                        {
                            string fullError = $"Cannot prepare destination path. {destinationError}";
                            LogExportDetail(entry, "ERROR", fullError);
                            lastErrorMessage = fullError;
                            Interlocked.Increment(ref errors);
                            int finishedSkipped = Interlocked.Increment(ref skipped);
                            int finishedCount = Interlocked.Increment(ref completed);
                            ReportProgress(progress, total, finishedCount, exported, finishedSkipped, injected, $"Preparing export... {finishedCount}/{total}");
                            continue;
                        }

                        LogExportDetail(entry, "JOB", $"Prepared export job | source={sourcePdf} | destination={destination}");
                        jobs.Add(new PdfExportJob
                        {
                            EntryKey = entry?.Key,
                            SourcePdfPath = sourcePdf,
                            DestinationPath = destination,
                            Doi = BibtexTagService.GetTagValueIgnoreCase(entry, "doi"),
                            Eprint = BibtexTagService.GetTagValueIgnoreCase(entry, "eprint"),
                            InjectDoiMetadata = options.InjectDoiMetadata
                        });
                    }

                    Parallel.ForEach(jobs, CreateParallelOptions(pdfMatchingOptions.ThreadCount, cancellationToken), job =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            bool isSameFile = string.Equals(
                                Path.GetFullPath(job.SourcePdfPath),
                                Path.GetFullPath(job.DestinationPath),
                                StringComparison.OrdinalIgnoreCase);

                            if (isSameFile == false)
                            {
                                string destinationDirectory = Path.GetDirectoryName(job.DestinationPath);
                                if (string.IsNullOrWhiteSpace(destinationDirectory))
                                    throw new DirectoryNotFoundException("Destination directory could not be determined.");

                                Directory.CreateDirectory(destinationDirectory);
                                File.Copy(job.SourcePdfPath, job.DestinationPath, false);
                            }

                            if (job.InjectDoiMetadata && canInjectPdfMetadata)
                            {
                                MetadataInjectionResult metadataResult = InjectIdentifiersIntoPdfMetadata(job.DestinationPath, job.Doi, job.Eprint, metadataTempDirectory);
                                if (metadataResult.Injected)
                                    Interlocked.Increment(ref injected);
                                else if (string.IsNullOrWhiteSpace(metadataResult.ErrorMessage) == false)
                                {
                                    string warningMessage = $"Metadata injection failed but exported PDF was kept. {metadataResult.ErrorMessage}";
                                    LogExportDetail(job.EntryKey, "ERROR", warningMessage);
                                    Interlocked.Exchange(ref lastErrorMessage, warningMessage);
                                    Interlocked.Increment(ref errors);
                                }
                            }

                            LogExportDetail(job.EntryKey, "OK", $"Exported to {job.DestinationPath}");
                            int finishedExported = Interlocked.Increment(ref exported);
                            int finishedCount = Interlocked.Increment(ref completed);
                            ReportProgress(progress, total, finishedCount, finishedExported, skipped, injected, $"Exporting PDFs... {finishedCount}/{total}");
                        }
                        catch (Exception ex)
                        {
                            string errorMessage = $"{ex.GetType().Name}: {ex.Message} | source={job.SourcePdfPath} | destination={job.DestinationPath}";
                            LogExportDetail(job.EntryKey, "ERROR", errorMessage);
                            Interlocked.Exchange(ref lastErrorMessage, errorMessage);
                            Interlocked.Increment(ref errors);
                            int finishedSkipped = Interlocked.Increment(ref skipped);
                            int finishedCount = Interlocked.Increment(ref completed);
                            ReportProgress(progress, total, finishedCount, exported, finishedSkipped, injected, $"Exporting PDFs... {finishedCount}/{total}");
                        }
                    });

                    return new ExportPdfsRunResult
                    {
                        Total = total,
                        Completed = completed,
                        Exported = exported,
                        Skipped = skipped,
                        Errors = errors,
                        Injected = injected,
                        Cancelled = false,
                        LastErrorMessage = lastErrorMessage
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
                        Errors = errors,
                        Injected = injected,
                        Cancelled = true,
                        LastErrorMessage = lastErrorMessage
                    };
                }
                finally
                {
                    TryDeleteDirectory(metadataTempDirectory);
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

        private bool TryBuildReservedExportDestinationPath(string outputDirectory, string baseFileName, string sourcePdfPath, HashSet<string> reservedDestinations, out string destinationPath, out string errorMessage)
        {
            destinationPath = null;
            errorMessage = null;
            string sourceFullPath = Path.GetFullPath(sourcePdfPath);

            for (int index = 1; ; index++)
            {
                string suffix = index == 1 ? string.Empty : "_" + index.ToString(CultureInfo.InvariantCulture);
                string effectiveBaseName = TrimBaseFileNameForPath(baseFileName, outputDirectory, suffix);
                if (string.IsNullOrWhiteSpace(effectiveBaseName))
                    effectiveBaseName = "record";

                string candidateFileName = effectiveBaseName + suffix + ".pdf";
                string candidate = Path.Combine(outputDirectory, candidateFileName);
                string candidateFullPath;

                try
                {
                    candidateFullPath = Path.GetFullPath(candidate);
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    return false;
                }

                if (candidateFullPath.Length > MaxDestinationPathLength)
                {
                    if (index == 1 && effectiveBaseName.Length > 10)
                        continue;

                    errorMessage = $"Destination path is too long: {candidateFullPath}";
                    return false;
                }

                bool isSameFile = string.Equals(candidateFullPath, sourceFullPath, StringComparison.OrdinalIgnoreCase);
                bool exists = File.Exists(candidateFullPath);
                bool alreadyReserved = reservedDestinations.Contains(candidateFullPath);

                if ((isSameFile || exists == false) && alreadyReserved == false)
                {
                    reservedDestinations.Add(candidateFullPath);
                    destinationPath = candidateFullPath;
                    return true;
                }
            }
        }

        private string TrimBaseFileNameForPath(string baseFileName, string outputDirectory, string suffix)
        {
            string safeBaseName = string.IsNullOrWhiteSpace(baseFileName) ? "record" : baseFileName.Trim();
            safeBaseName = safeBaseName.Trim(' ', '.', '_', '-');
            if (string.IsNullOrWhiteSpace(safeBaseName))
                safeBaseName = "record";

            if (safeBaseName.Length > MaxFileNameLength)
                safeBaseName = safeBaseName.Substring(0, MaxFileNameLength).TrimEnd(' ', '.', '_', '-');

            int available = MaxDestinationPathLength - outputDirectory.Length - suffix.Length - ".pdf".Length - 1;
            if (available <= 0)
                return "record";

            if (safeBaseName.Length > available)
                safeBaseName = safeBaseName.Substring(0, available).TrimEnd(' ', '.', '_', '-');

            return string.IsNullOrWhiteSpace(safeBaseName) ? "record" : safeBaseName;
        }

        private void LogExportDetail(BibtexEntry entry, string stage, string message)
        {
            string key = entry?.Key;
            LogExportDetail(key, stage, message);
        }

        private void LogExportDetail(string entryKey, string stage, string message)
        {
            string safeStage = string.IsNullOrWhiteSpace(stage) ? "INFO" : stage.Trim();
            string safeKey = string.IsNullOrWhiteSpace(entryKey) ? "<no-key>" : entryKey.Trim();
            string safeMessage = string.IsNullOrWhiteSpace(message) ? "-" : message.Trim();
            AppLog.Log($"[Export PDFs detail][{safeStage}][{safeKey}] {safeMessage}", stage == "ERROR" ? AppLog.MessageType.Error : AppLog.MessageType.Info);
        }

        private MetadataInjectionResult InjectIdentifiersIntoPdfMetadata(string fileName, string doi, string eprint, string metadataTempDirectory)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return new MetadataInjectionResult();

            string normalizedDoi = string.IsNullOrWhiteSpace(doi) ? null : doi.Trim();
            string normalizedEprint = string.IsNullOrWhiteSpace(eprint) ? null : eprint.Trim();
            if (string.IsNullOrWhiteSpace(normalizedDoi) && string.IsNullOrWhiteSpace(normalizedEprint))
                return new MetadataInjectionResult();

            string tempDirectory = string.IsNullOrWhiteSpace(metadataTempDirectory)
                ? (Path.GetDirectoryName(fileName) ?? string.Empty)
                : metadataTempDirectory;
            Directory.CreateDirectory(tempDirectory);

            string tempFileName = Path.Combine(
                tempDirectory,
                Path.GetFileNameWithoutExtension(fileName) + ".metadata_tmp_" + Guid.NewGuid().ToString("N") + Path.GetExtension(fileName));

            try
            {
                using (PdfReader reader = new PdfReader(fileName))
                using (PdfWriter writer = new PdfWriter(tempFileName))
                using (PdfDocument pdfDocument = new PdfDocument(reader, writer))
                {
                    PdfDocumentInfo info = pdfDocument.GetDocumentInfo();

                    if (string.IsNullOrWhiteSpace(normalizedDoi) == false)
                        info.SetMoreInfo("DOI", normalizedDoi);

                    if (string.IsNullOrWhiteSpace(normalizedEprint) == false)
                        info.SetMoreInfo("eprint", normalizedEprint);
                }

                File.Copy(tempFileName, fileName, true);
                return new MetadataInjectionResult
                {
                    Injected = true
                };
            }
            catch (Exception ex)
            {
                Exception baseException = ex.GetBaseException() ?? ex;
                return new MetadataInjectionResult
                {
                    Injected = false,
                    ErrorMessage = $"{baseException.GetType().Name}: {baseException.Message} | file={fileName}"
                };
            }
            finally
            {
                TryDeleteFile(tempFileName);
            }
        }

        private void TryDeleteFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || File.Exists(fileName) == false)
                return;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.Delete(fileName);
                    return;
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void TryDeleteDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || Directory.Exists(directoryPath) == false)
                return;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    foreach (string file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                        }
                        catch
                        {
                        }
                    }

                    foreach (string subDirectory in Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
                        .OrderByDescending(path => path.Length))
                    {
                        try
                        {
                            Directory.Delete(subDirectory, true);
                        }
                        catch
                        {
                        }
                    }

                    Directory.Delete(directoryPath, true);
                    return;
                }
                catch
                {
                    Thread.Sleep(150);
                }
            }
        }

        private bool IsITextMetadataInjectionAvailable(out string errorMessage)
        {
            errorMessage = null;

            try
            {
                Assembly.Load("BouncyCastle.Cryptography");
                Assembly.Load("itext.bouncy-castle-adapter");
                return true;
            }
            catch
            {
            }

            try
            {
                Assembly.Load("itext.bouncy-castle-fips-adapter");
                return true;
            }
            catch
            {
            }

            errorMessage = "Missing assembly 'BouncyCastle.Cryptography' and/or 'itext.bouncy-castle-adapter' (or FIPS alternative).";
            return false;
        }
    }
}
