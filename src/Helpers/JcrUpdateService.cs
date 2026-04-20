using ScientificReviews.Bibtex;
using ScientificReviews.JCR;
using ScientificReviews.JCR.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScientificReviews.Helpers
{
    public sealed class JcrUpdateProgress
    {
        public string Summary { get; set; }
        public string Details { get; set; }
        public int? Completed { get; set; }
        public int? Total { get; set; }
    }

    public sealed class JcrUpdateResult
    {
        public int MissingJournalCount { get; set; }
        public int ResolvedJournalCount { get; set; }
        public int AddedJournalCount { get; set; }
        public int NotFoundJournalCount { get; set; }
        public List<string> NotFoundJournals { get; set; } = new List<string>();
        public List<string> ResolvedJournals { get; set; } = new List<string>();
        public List<JcrJournalLookupIssue> LookupIssues { get; set; } = new List<JcrJournalLookupIssue>();
    }

    public sealed class JcrJournalLookupIssue
    {
        public string JournalName { get; set; }
        public string Reason { get; set; }
    }

    internal sealed class JcrJournalLookupResult
    {
        public JournalReportsDto Report { get; set; }
        public string FailureReason { get; set; }
    }

    public sealed class JcrUpdateService
    {
        public async Task<JcrUpdateResult> UpdateMissingJournalsAsync(
            IEnumerable<BibtexEntry> entries,
            IList<JournalReportsDto> journalReports,
            string apiKey,
            int fromYear,
            Action saveJournalDatabase,
            Action<string> logError,
            IProgress<JcrUpdateProgress> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            if (journalReports == null)
                throw new ArgumentNullException(nameof(journalReports));
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key must not be empty.", nameof(apiKey));

            JcrApiClient jcrApiClient = new JcrApiClient(apiKey);
            List<string> missingJournals = new List<string>();
            Dictionary<string, string> missingJournalDisplayNames = new Dictionary<string, string>();
            Dictionary<string, JournalReportsDto> reportsByName = journalReports
                .Where(report => report?.Journal?.Name != null)
                .GroupBy(report => NormalizeJournalKey(report.Journal.Name))
                .Where(group => string.IsNullOrWhiteSpace(group.Key) == false)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var entry in entries)
            {
                if (entry == null || entry.Type != "article")
                    continue;

                string journalValue = entry.GetTagValue("journal");
                if (string.IsNullOrWhiteSpace(journalValue))
                    continue;

                string journalName = BibtexUtils.RemoveLatex(journalValue).Trim();
                string journalKey = NormalizeJournalKey(journalName);
                if (string.IsNullOrWhiteSpace(journalKey))
                    continue;

                if (reportsByName.ContainsKey(journalKey) == false && missingJournals.Contains(journalKey) == false)
                {
                    missingJournals.Add(journalKey);
                    missingJournalDisplayNames[journalKey] = journalName;
                }
            }

            missingJournals = missingJournals
                .OrderBy(key => missingJournalDisplayNames[key], StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (missingJournals.Count == 0)
            {
                progress?.Report(new JcrUpdateProgress
                {
                    Summary = "No missing journals.",
                    Details = "Local JCR database already covers all loaded records.",
                    Completed = 1,
                    Total = 1
                });

                return new JcrUpdateResult();
            }

            var notFoundJournals = new List<string>();
            var resolvedJournalNames = new List<string>();
            var lookupIssues = new List<JcrJournalLookupIssue>();
            int addedJournals = 0;
            int resolvedJournals = 0;

            for (int journalIndex = 0; journalIndex < missingJournals.Count; journalIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string missingJournalKey = missingJournals[journalIndex];
                string missingJournal = missingJournalDisplayNames[missingJournalKey];
                progress?.Report(new JcrUpdateProgress
                {
                    Summary = "Checking missing journals",
                    Details = $"{journalIndex + 1}/{missingJournals.Count}: {missingJournal}",
                    Completed = journalIndex + 1,
                    Total = missingJournals.Count
                });

                try
                {
                    var response = await jcrApiClient.GetJournalsAsync(missingJournal.Replace("&", "").Replace("-", " "));
                    JcrJournalLookupResult lookupResult = await FindBestJournalReportAsync(
                        jcrApiClient,
                        response?.Hits,
                        missingJournalKey,
                        fromYear,
                        cancellationToken);

                    JournalReportsDto report = lookupResult?.Report;
                    if (report == null)
                    {
                        notFoundJournals.Add(missingJournal);
                        lookupIssues.Add(new JcrJournalLookupIssue
                        {
                            JournalName = missingJournal,
                            Reason = lookupResult?.FailureReason ?? "Journal could not be resolved in JCR."
                        });
                        continue;
                    }

                    string reportKey = NormalizeJournalKey(report.Journal?.Name);
                    if (string.IsNullOrWhiteSpace(reportKey))
                    {
                        notFoundJournals.Add(missingJournal);
                        continue;
                    }

                    resolvedJournals++;
                    resolvedJournalNames.Add(missingJournal);
                    if (journalReports.Any(existing =>
                        string.Equals(NormalizeJournalKey(existing?.Journal?.Name), reportKey, StringComparison.Ordinal)) == false)
                    {
                        journalReports.Add(report);
                        addedJournals++;
                        saveJournalDatabase?.Invoke();
                    }

                    reportsByName[reportKey] = report;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    notFoundJournals.Add(missingJournal);
                    lookupIssues.Add(new JcrJournalLookupIssue
                    {
                        JournalName = missingJournal,
                        Reason = $"Unexpected JCR lookup error: {ex.Message}"
                    });
                    logError?.Invoke($"JCR update failed for journal '{missingJournal}': {ex.Message}");
                }
            }

            string details = notFoundJournals.Count == 0
                ? "All missing journals were resolved."
                : "Not found: " + string.Join(", ", notFoundJournals);

            progress?.Report(new JcrUpdateProgress
            {
                Summary = $"Resolved {resolvedJournals}/{missingJournals.Count}",
                Details = details,
                Completed = missingJournals.Count,
                Total = missingJournals.Count
            });

            return new JcrUpdateResult
            {
                MissingJournalCount = missingJournals.Count,
                ResolvedJournalCount = resolvedJournals,
                AddedJournalCount = addedJournals,
                NotFoundJournalCount = notFoundJournals.Count,
                NotFoundJournals = notFoundJournals,
                ResolvedJournals = resolvedJournalNames,
                LookupIssues = lookupIssues
            };
        }

        private async Task<JcrJournalLookupResult> FindBestJournalReportAsync(
            JcrApiClient jcrApiClient,
            IEnumerable<JournalHitDto> hits,
            string missingJournalKey,
            int fromYear,
            CancellationToken cancellationToken)
        {
            JournalReportsDto looseMatch = null;
            List<JournalHitDto> hitList = (hits ?? Enumerable.Empty<JournalHitDto>())
                .Where(item => item != null && string.IsNullOrWhiteSpace(item.Id) == false)
                .OrderByDescending(item => JournalKeyMatches(item.Name, missingJournalKey))
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (hitList.Count == 0)
            {
                return new JcrJournalLookupResult
                {
                    FailureReason = "Journal was not found in Clarivate journal search."
                };
            }

            foreach (JournalHitDto hit in hitList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                JournalReportsDto report = await TryGetLatestJournalReportAsync(jcrApiClient, hit.Id, fromYear, cancellationToken);
                if (report == null)
                    continue;

                string reportKey = NormalizeJournalKey(report.Journal?.Name);
                if (string.IsNullOrWhiteSpace(reportKey))
                    continue;

                if (string.Equals(reportKey, missingJournalKey, StringComparison.Ordinal))
                {
                    return new JcrJournalLookupResult
                    {
                        Report = report
                    };
                }

                if (looseMatch == null && JournalKeyMatches(report.Journal?.Name, missingJournalKey))
                    looseMatch = report;
            }

            if (looseMatch != null)
            {
                return new JcrJournalLookupResult
                {
                    Report = looseMatch
                };
            }

            return new JcrJournalLookupResult
            {
                FailureReason = "Journal search returned candidates, but no usable JCR annual report could be resolved."
            };
        }

        private async Task<JournalReportsDto> TryGetLatestJournalReportAsync(
            JcrApiClient jcrApiClient,
            string journalId,
            int fromYear,
            CancellationToken cancellationToken)
        {
            for (int currentYear = fromYear; currentYear > 2020; currentYear--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await jcrApiClient.GetJournalReportsAsync(journalId, currentYear);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return null;
        }

        private static string NormalizeJournalKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            char[] normalized = BibtexUtils.RemoveLatex(value)
                .Trim()
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray();

            return normalized.Length == 0 ? null : new string(normalized);
        }

        private static bool JournalKeyMatches(string candidateName, string expectedKey)
        {
            string candidateKey = NormalizeJournalKey(candidateName);
            if (string.IsNullOrWhiteSpace(candidateKey) || string.IsNullOrWhiteSpace(expectedKey))
                return false;

            return string.Equals(candidateKey, expectedKey, StringComparison.Ordinal) ||
                candidateKey.Contains(expectedKey) ||
                expectedKey.Contains(candidateKey);
        }
    }
}
