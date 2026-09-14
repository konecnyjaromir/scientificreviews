using ScientificReviews.Bibtex;
using System;
using System.Collections.Concurrent;
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
    public sealed class PdfMatchingOptions
    {
        public string PdfFolder { get; set; }
        public bool RecursiveSearch { get; set; }
        public int AutoPairThresholdPercent { get; set; } = 95;
        public int ThreadCount { get; set; } = 4;
        public PdfSourceMatchMode SourceMatchMode { get; set; } = PdfSourceMatchMode.TitleOnly;
    }

    public sealed class PdfAutoPairProgress
    {
        public string Summary { get; set; }
        public string Details { get; set; }
        public int? Completed { get; set; }
        public int? Total { get; set; }
        public bool IsIndeterminate { get; set; }
    }

    public sealed class PdfAutoPairResult
    {
        public int DirectMatches { get; set; }
        public int SmartMatches { get; set; }
        public int Unmatched { get; set; }
        public bool NoPdfsFound { get; set; }
        public bool RecommendRecursiveSearch { get; set; }
    }

    public sealed class PdfMatchingService
    {
        private sealed class PdfArchiveItem
        {
            public string FilePath { get; set; }
            public string StandardizedName { get; set; }
            public Dictionary<string, int> Tokens { get; set; }
            public HashSet<string> Keywords { get; set; }
        }

        private sealed class PdfSimilarityCandidate
        {
            public BibtexEntry Entry { get; set; }
            public PdfArchiveItem Pdf { get; set; }
            public double Score { get; set; }
        }

        public string[] GetPdfFiles(PdfMatchingOptions options)
        {
            if (string.IsNullOrWhiteSpace(options?.PdfFolder) || Directory.Exists(options.PdfFolder) == false)
                return new string[0];

            SearchOption searchOption = options.RecursiveSearch
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            return Directory
                .GetFiles(options.PdfFolder, "*.pdf", searchOption)
                .Where(file => IsPdfFileAllowed(file, options.PdfFolder))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string FindPdfFile(BibtexEntry entry, PdfMatchingOptions options, string[] pdfFiles = null)
        {
            if (entry == null)
                return null;

            string storedPdf = FindStoredPdfFile(entry, options?.PdfFolder);
            if (string.IsNullOrWhiteSpace(storedPdf) == false)
                return storedPdf;

            var files = pdfFiles ?? GetPdfFiles(options);
            if (files.Length == 0)
                return null;

            return FindDirectPdfMatch(entry, files, options?.SourceMatchMode ?? PdfSourceMatchMode.TitleOnly);
        }

        public string FindStoredPdfFile(BibtexEntry entry, string pdfFolder)
        {
            string storedValue = BibtexTagService.GetTagValueIgnoreCase(entry, "path_to_pdf");
            if (string.IsNullOrWhiteSpace(storedValue))
                storedValue = BibtexTagService.GetTagValueIgnoreCase(entry, "pdf_file");

            if (string.IsNullOrWhiteSpace(storedValue))
                return null;

            string candidatePath = storedValue;
            if (Path.IsPathRooted(candidatePath) == false)
            {
                if (string.IsNullOrWhiteSpace(pdfFolder))
                    return null;

                candidatePath = Path.Combine(pdfFolder, storedValue);
            }

            candidatePath = Path.GetFullPath(candidatePath);
            return File.Exists(candidatePath) ? candidatePath : null;
        }

        public string FindDirectPdfMatch(BibtexEntry entry, IEnumerable<string> pdfFiles, PdfSourceMatchMode sourceMatchMode)
        {
            var files = (pdfFiles ?? Enumerable.Empty<string>()).ToArray();
            if (files.Length == 0)
                return null;

            foreach (PdfSourceMatchTarget target in GetMatchTargets(entry, sourceMatchMode))
            {
                string titleMatch = files.FirstOrDefault(file => IsDirectMatch(file, target));

                if (string.IsNullOrWhiteSpace(titleMatch) == false)
                    return titleMatch;
            }

            return null;
        }

        public void AssignPdfToEntry(BibtexEntry entry, string pdfFilePath)
        {
            if (entry == null || string.IsNullOrWhiteSpace(pdfFilePath))
                return;

            string fullPdfPath = Path.GetFullPath(pdfFilePath);
            BibtexTagService.SetSingleTagValue(entry, "path_to_pdf", fullPdfPath);
            BibtexTagService.SetSingleTagValue(entry, "pdf_file", Path.GetFileName(pdfFilePath));
            BibtexTagService.SetSingleTagValue(entry, "has_pdf", "yes");
        }

        public void ClearPdfAssignment(BibtexEntry entry)
        {
            if (entry == null)
                return;

            BibtexTagService.RemoveAllTagsByKey(entry, "path_to_pdf");
            BibtexTagService.RemoveAllTagsByKey(entry, "pdf_file");
            UpdateHasPdfTag(entry, false);
        }

        public void UpdateHasPdfTag(BibtexEntry entry, bool hasPdf)
        {
            BibtexTagService.SetSingleTagValue(entry, "has_pdf", hasPdf ? "yes" : "no");
        }

        public Task<PdfAutoPairResult> AutoPairAsync(
            IList<BibtexEntry> entries,
            PdfMatchingOptions options,
            IProgress<PdfAutoPairProgress> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            return Task.Run(() =>
            {
                progress?.Report(new PdfAutoPairProgress
                {
                    Summary = $"Using {GetConfiguredThreadCount(options)} thread(s)",
                    Details = options.PdfFolder,
                    Completed = 0,
                    Total = 1,
                    IsIndeterminate = true
                });

                string[] pdfFiles = GetPdfFiles(options);
                if (pdfFiles.Length == 0)
                {
                    return new PdfAutoPairResult
                    {
                        DirectMatches = 0,
                        SmartMatches = 0,
                        Unmatched = entries.Count,
                        NoPdfsFound = true,
                        RecommendRecursiveSearch = options.RecursiveSearch == false
                    };
                }

                double threshold = Math.Max(0d, Math.Min(100d, options.AutoPairThresholdPercent)) / 100d;
                int directMatches = 0;
                int smartMatches = 0;
                var assignedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pairedEntries = new HashSet<BibtexEntry>();

                progress?.Report(new PdfAutoPairProgress
                {
                    Summary = "Matching stored PDF links...",
                    Completed = 0,
                    Total = Math.Max(1, entries.Count),
                    IsIndeterminate = false
                });

                for (int index = 0; index < entries.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    BibtexEntry entry = entries[index];
                    string storedPdf = FindStoredPdfFile(entry, options.PdfFolder);
                    if (string.IsNullOrWhiteSpace(storedPdf))
                    {
                        ClearPdfAssignment(entry);
                        ReportProgress(progress, index + 1, entries.Count, storedPdf);
                        continue;
                    }

                    if (assignedFiles.Add(storedPdf))
                    {
                        AssignPdfToEntry(entry, storedPdf);
                        pairedEntries.Add(entry);
                        directMatches++;
                    }
                    else
                    {
                        ClearPdfAssignment(entry);
                    }

                    ReportProgress(progress, index + 1, entries.Count, storedPdf);
                }

                List<BibtexEntry> entriesToDirectMatch = entries.Where(item => pairedEntries.Contains(item) == false).ToList();
                progress?.Report(new PdfAutoPairProgress
                {
                    Summary = "Matching direct PDF names...",
                    Completed = 0,
                    Total = Math.Max(1, entriesToDirectMatch.Count),
                    IsIndeterminate = false
                });

                for (int index = 0; index < entriesToDirectMatch.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    BibtexEntry entry = entriesToDirectMatch[index];
                    string directMatch = FindDirectPdfMatch(entry, pdfFiles.Where(file => assignedFiles.Contains(file) == false), options.SourceMatchMode);
                    if (string.IsNullOrWhiteSpace(directMatch) == false)
                    {
                        AssignPdfToEntry(entry, directMatch);
                        assignedFiles.Add(directMatch);
                        pairedEntries.Add(entry);
                        directMatches++;
                    }

                    ReportProgress(progress, index + 1, entriesToDirectMatch.Count, entry.Key);
                }

                List<BibtexEntry> remainingEntries = entries.Where(item => pairedEntries.Contains(item) == false).ToList();
                string[] remainingPdfPaths = pdfFiles
                    .Where(file => assignedFiles.Contains(file) == false)
                    .ToArray();

                progress?.Report(new PdfAutoPairProgress
                {
                    Summary = "Building PDF archive index...",
                    Details = $"{remainingPdfPaths.Length} PDFs",
                    Completed = 0,
                    Total = 1,
                    IsIndeterminate = true
                });

                var remainingPdfsBag = new ConcurrentBag<PdfArchiveItem>();
                Parallel.ForEach(remainingPdfPaths, CreateParallelOptions(options, cancellationToken), file =>
                {
                    remainingPdfsBag.Add(BuildPdfArchiveItem(file));
                });

                List<PdfArchiveItem> remainingPdfs = remainingPdfsBag
                    .OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                progress?.Report(new PdfAutoPairProgress
                {
                    Summary = "Comparing titles with PDF names...",
                    Details = $"{remainingEntries.Count} entries, {remainingPdfs.Count} PDFs",
                    Completed = 0,
                    Total = 1,
                    IsIndeterminate = true
                });

                var candidates = new ConcurrentBag<PdfSimilarityCandidate>();
                Parallel.ForEach(remainingEntries, CreateParallelOptions(options, cancellationToken), entry =>
                {
                    foreach (PdfArchiveItem pdf in remainingPdfs)
                    {
                        double score = ComputeSimilarityScore(entry, pdf, options.SourceMatchMode);
                        if (score >= threshold)
                        {
                            candidates.Add(new PdfSimilarityCandidate
                            {
                                Entry = entry,
                                Pdf = pdf,
                                Score = score
                            });
                        }
                    }
                });

                List<PdfSimilarityCandidate> orderedCandidates = candidates
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.Pdf.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                progress?.Report(new PdfAutoPairProgress
                {
                    Summary = "Assigning best similarity matches...",
                    Details = $"{orderedCandidates.Count} candidates",
                    Completed = 0,
                    Total = Math.Max(1, orderedCandidates.Count),
                    IsIndeterminate = orderedCandidates.Count == 0
                });

                for (int index = 0; index < orderedCandidates.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    PdfSimilarityCandidate candidate = orderedCandidates[index];
                    if (pairedEntries.Contains(candidate.Entry) == false && assignedFiles.Contains(candidate.Pdf.FilePath) == false)
                    {
                        AssignPdfToEntry(candidate.Entry, candidate.Pdf.FilePath);
                        assignedFiles.Add(candidate.Pdf.FilePath);
                        pairedEntries.Add(candidate.Entry);
                        smartMatches++;
                    }

                    ReportProgress(progress, index + 1, orderedCandidates.Count, candidate.Entry.Key);
                }

                foreach (BibtexEntry entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (pairedEntries.Contains(entry))
                    {
                        UpdateHasPdfTag(entry, true);
                    }
                    else
                    {
                        ClearPdfAssignment(entry);
                    }
                }

                return new PdfAutoPairResult
                {
                    DirectMatches = directMatches,
                    SmartMatches = smartMatches,
                    Unmatched = entries.Count - pairedEntries.Count,
                    NoPdfsFound = false
                };
            }, cancellationToken);
        }

        private void ReportProgress(IProgress<PdfAutoPairProgress> progress, int completed, int total, string details)
        {
            progress?.Report(new PdfAutoPairProgress
            {
                Summary = $"{completed}/{total}",
                Details = details,
                Completed = completed,
                Total = total,
                IsIndeterminate = false
            });
        }

        private int GetConfiguredThreadCount(PdfMatchingOptions options)
        {
            return Math.Max(1, options?.ThreadCount ?? 1);
        }

        private ParallelOptions CreateParallelOptions(PdfMatchingOptions options, CancellationToken cancellationToken)
        {
            return new ParallelOptions
            {
                MaxDegreeOfParallelism = GetConfiguredThreadCount(options),
                CancellationToken = cancellationToken
            };
        }

        private bool IsPdfFileAllowed(string filePath, string rootPdfFolder)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string rootFullPath = Path.GetFullPath(rootPdfFolder);
            DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(filePath) ?? rootFullPath);

            while (directory != null && string.Equals(directory.FullName, rootFullPath, StringComparison.OrdinalIgnoreCase) == false)
            {
                if (IsIgnoredPdfFolderName(directory.Name))
                    return false;

                directory = directory.Parent;
            }

            return true;
        }

        private bool IsIgnoredPdfFolderName(string folderName)
        {
            return string.IsNullOrWhiteSpace(folderName) == false &&
                folderName.StartsWith("__", StringComparison.Ordinal) &&
                folderName.EndsWith("__", StringComparison.Ordinal);
        }

        private string GetNormalizedTitleForExactMatch(BibtexEntry entry)
        {
            string title = BibtexTagService.GetTagValueIgnoreCase(entry, "title");
            if (string.IsNullOrWhiteSpace(title))
                return null;

            return BibtexUtils.RemoveLatex(title).Trim().ToLowerInvariant();
        }

        private string GetNormalizedKeyForExactMatch(BibtexEntry entry)
        {
            string key = (entry?.Key ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(key) ? null : key.ToLowerInvariant();
        }

        private string GetStandardizedTitle(BibtexEntry entry)
        {
            string title = BibtexTagService.GetTagValueIgnoreCase(entry, "title");
            return StandardizeText(BibtexUtils.RemoveLatex(title ?? string.Empty));
        }

        private string GetStandardizedKey(BibtexEntry entry)
        {
            return StandardizeText((entry?.Key ?? string.Empty).Trim());
        }

        private PdfArchiveItem BuildPdfArchiveItem(string filePath)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            string standardized = StandardizeText(fileNameWithoutExtension);

            return new PdfArchiveItem
            {
                FilePath = filePath,
                StandardizedName = standardized,
                Tokens = Tokenize(standardized),
                Keywords = ExtractKeywords(standardized)
            };
        }

        private string StandardizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = RemoveDiacritics(value).ToLowerInvariant();
            normalized = normalized.Replace('_', ' ').Replace('-', ' ');
            normalized = Regex.Replace(normalized, @"[^a-z0-9\s]", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }

        private string RemoveDiacritics(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    builder.Append(c);
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private Dictionary<string, int> Tokenize(string value)
        {
            var tokens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in (value ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (tokens.ContainsKey(token))
                    tokens[token]++;
                else
                    tokens[token] = 1;
            }

            return tokens;
        }

        private HashSet<string> ExtractKeywords(string standardizedText)
        {
            return new HashSet<string>(
                (standardizedText ?? string.Empty)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(token => token.Length >= 4),
                StringComparer.OrdinalIgnoreCase);
        }

        private double ComputeCosineSimilarity(Dictionary<string, int> left, Dictionary<string, int> right)
        {
            if (left == null || right == null || left.Count == 0 || right.Count == 0)
                return 0d;

            double dot = 0d;
            foreach (KeyValuePair<string, int> pair in left)
            {
                if (right.TryGetValue(pair.Key, out int rightValue))
                    dot += pair.Value * rightValue;
            }

            double leftNorm = Math.Sqrt(left.Values.Sum(v => v * v));
            double rightNorm = Math.Sqrt(right.Values.Sum(v => v * v));
            if (leftNorm == 0d || rightNorm == 0d)
                return 0d;

            return dot / (leftNorm * rightNorm);
        }

        private double ComputeKeywordScore(HashSet<string> left, HashSet<string> right)
        {
            if (left == null || right == null || left.Count == 0 || right.Count == 0)
                return 0d;

            int intersection = left.Count(token => right.Contains(token));
            int union = left.Count + right.Count - intersection;
            if (union == 0)
                return 0d;

            return (double)intersection / union;
        }

        private double ComputeSimilarityScore(BibtexEntry entry, PdfArchiveItem pdf, PdfSourceMatchMode sourceMatchMode)
        {
            if (pdf == null || string.IsNullOrWhiteSpace(pdf.StandardizedName))
                return 0d;

            double bestScore = 0d;
            foreach (PdfSourceMatchTarget target in GetMatchTargets(entry, sourceMatchMode))
            {
                string source = target.StandardizedValue;
                if (string.IsNullOrWhiteSpace(source))
                    continue;

                if (string.Equals(source, pdf.StandardizedName, StringComparison.Ordinal))
                    return 1d;

                Dictionary<string, int> sourceTokens = Tokenize(source);
                HashSet<string> sourceKeywords = ExtractKeywords(source);
                double cosine = ComputeCosineSimilarity(sourceTokens, pdf.Tokens);
                double keywordScore = ComputeKeywordScore(sourceKeywords, pdf.Keywords);
                double score = Math.Min(1d, (cosine * 0.9d) + (keywordScore * 0.1d));
                if (score > bestScore)
                    bestScore = score;
            }

            return bestScore;
        }

        private bool IsDirectMatch(string filePath, PdfSourceMatchTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(filePath))
                return false;

            string fileName = Path.GetFileNameWithoutExtension(filePath)?.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            if (target.MatchMode == PdfSourceDirectMatchMode.Contains)
                return fileName.IndexOf(target.RawValue, StringComparison.OrdinalIgnoreCase) >= 0;

            return string.Equals(fileName.ToLowerInvariant(), target.RawValue, StringComparison.Ordinal);
        }

        private IEnumerable<PdfSourceMatchTarget> GetMatchTargets(BibtexEntry entry, PdfSourceMatchMode sourceMatchMode)
        {
            switch (sourceMatchMode)
            {
                case PdfSourceMatchMode.KeyOnly:
                    return GetKeyMatchTargets(entry);
                case PdfSourceMatchMode.KeyOrTitle:
                    return GetTitleMatchTargets(entry).Concat(GetKeyMatchTargets(entry));
                case PdfSourceMatchMode.TitleOnly:
                default:
                    return GetTitleMatchTargets(entry);
            }
        }

        private IEnumerable<PdfSourceMatchTarget> GetTitleMatchTargets(BibtexEntry entry)
        {
            string titleRaw = GetNormalizedTitleForExactMatch(entry);
            string titleStandardized = GetStandardizedTitle(entry);
            if (string.IsNullOrWhiteSpace(titleRaw) || string.IsNullOrWhiteSpace(titleStandardized))
                return Enumerable.Empty<PdfSourceMatchTarget>();

            return new[]
            {
                new PdfSourceMatchTarget
                {
                    RawValue = titleRaw,
                    StandardizedValue = titleStandardized,
                    MatchMode = PdfSourceDirectMatchMode.Exact
                }
            };
        }

        private IEnumerable<PdfSourceMatchTarget> GetKeyMatchTargets(BibtexEntry entry)
        {
            string keyRaw = GetNormalizedKeyForExactMatch(entry);
            string keyStandardized = GetStandardizedKey(entry);
            if (string.IsNullOrWhiteSpace(keyRaw) || string.IsNullOrWhiteSpace(keyStandardized))
                return Enumerable.Empty<PdfSourceMatchTarget>();

            return new[]
            {
                new PdfSourceMatchTarget
                {
                    RawValue = keyRaw,
                    StandardizedValue = keyStandardized,
                    MatchMode = PdfSourceDirectMatchMode.Contains
                }
            };
        }

        private sealed class PdfSourceMatchTarget
        {
            public string RawValue { get; set; }
            public string StandardizedValue { get; set; }
            public PdfSourceDirectMatchMode MatchMode { get; set; }
        }

        private enum PdfSourceDirectMatchMode
        {
            Exact,
            Contains
        }

    }
}
