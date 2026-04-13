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
        public int AddedJournalCount { get; set; }
        public int NotFoundJournalCount { get; set; }
        public List<string> NotFoundJournals { get; set; } = new List<string>();
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
            Dictionary<string, JournalReportsDto> reportsByName = journalReports
                .Where(report => report?.Journal?.Name != null)
                .ToDictionary(report => report.Journal.Name.ToLower());

            foreach (var entry in entries)
            {
                if (entry == null || entry.Type != "article")
                    continue;

                string journalValue = entry.GetTagValue("journal");
                if (string.IsNullOrWhiteSpace(journalValue))
                    continue;

                string journalName = BibtexUtils.RemoveLatex(journalValue).ToLower();
                if (reportsByName.ContainsKey(journalName) == false && missingJournals.Contains(journalName) == false)
                    missingJournals.Add(journalName);
            }

            missingJournals.Sort();
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
            int addedJournals = 0;

            for (int journalIndex = 0; journalIndex < missingJournals.Count; journalIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string missingJournal = missingJournals[journalIndex];
                progress?.Report(new JcrUpdateProgress
                {
                    Summary = $"{journalIndex + 1}/{missingJournals.Count}",
                    Details = missingJournal,
                    Completed = journalIndex + 1,
                    Total = missingJournals.Count
                });

                try
                {
                    var response = await jcrApiClient.GetJournalsAsync(missingJournal.Replace("&", "").Replace("-", " "));
                    bool foundAnyReport = false;

                    foreach (var hit in response.Hits)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        JournalReportsDto report = null;
                        for (int currentYear = fromYear; currentYear > 2020; currentYear--)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            try
                            {
                                report = await jcrApiClient.GetJournalReportsAsync(hit.Id, currentYear);
                                break;
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

                        if (report == null)
                            continue;

                        foundAnyReport = true;
                        string reportKey = report.Journal.Name.ToLower();
                        if (journalReports.Any(existing => string.Equals(existing?.Journal?.Name, report.Journal.Name, StringComparison.OrdinalIgnoreCase)) == false)
                        {
                            journalReports.Add(report);
                            addedJournals++;
                            saveJournalDatabase?.Invoke();
                        }

                        reportsByName[reportKey] = report;
                    }

                    if (!foundAnyReport)
                        notFoundJournals.Add(missingJournal);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    notFoundJournals.Add(missingJournal);
                    logError?.Invoke($"JCR update failed for journal '{missingJournal}': {ex.Message}");
                }
            }

            string details = notFoundJournals.Count == 0
                ? "All missing journals were resolved."
                : "Not found: " + string.Join(", ", notFoundJournals);

            progress?.Report(new JcrUpdateProgress
            {
                Summary = $"Added {addedJournals}",
                Details = details,
                Completed = missingJournals.Count,
                Total = missingJournals.Count
            });

            return new JcrUpdateResult
            {
                MissingJournalCount = missingJournals.Count,
                AddedJournalCount = addedJournals,
                NotFoundJournalCount = notFoundJournals.Count,
                NotFoundJournals = notFoundJournals
            };
        }
    }
}
