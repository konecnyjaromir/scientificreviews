using Newtonsoft.Json.Linq;
using ScientificReviews.Bibtex;
using ScientificReviews.Logs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ScientificReviews.Helpers
{
    public sealed class MetadataUpdateProgress
    {
        public string Summary { get; set; }
        public string Details { get; set; }
        public int? Completed { get; set; }
        public int? Total { get; set; }
    }

    public sealed class MetadataUpdateOptions
    {
        public string ContactEmail { get; set; }
        public int ThreadCount { get; set; } = 1;
        public MetadataScreenMode ScreenMode { get; set; } = MetadataScreenMode.OnlyMissing;
    }

    public sealed class MetadataUpdateResult
    {
        public int CheckedEntries { get; set; }
        public int AlreadyCompleteEntries { get; set; }
        public int UpdatedEntries { get; set; }
        public int UnresolvedEntries { get; set; }
        public int FailedEntries { get; set; }
        public List<string> UnresolvedRecordKeys { get; set; } = new List<string>();
        public List<string> FailedRecordKeys { get; set; } = new List<string>();
    }

    internal interface IMetadataProvider
    {
        string Name { get; }
        Task<MetadataPayload> FetchByDoiAsync(string doi, string titleHint, string authorHint, MetadataUpdateOptions options);
        Task<MetadataPayload> FetchByTitleAsync(string title, string doiHint, string authorHint, MetadataUpdateOptions options);
    }

    internal sealed class MetadataPayload
    {
        public string Title { get; set; }
        public string Doi { get; set; }
        public string Abstract { get; set; }
        public string Year { get; set; }
        public string Author { get; set; }
        public string Eprint { get; set; }
        public string Journal { get; set; }
        public List<string> Sources { get; } = new List<string>();

        public MetadataPayload MergeMissing(MetadataPayload other)
        {
            if (other == null)
                return this;

            if (string.IsNullOrWhiteSpace(Title))
                Title = other.Title;
            if (string.IsNullOrWhiteSpace(Doi))
                Doi = other.Doi;
            if (string.IsNullOrWhiteSpace(Abstract))
                Abstract = other.Abstract;
            if (string.IsNullOrWhiteSpace(Year))
                Year = other.Year;
            if (string.IsNullOrWhiteSpace(Author))
                Author = other.Author;
            if (string.IsNullOrWhiteSpace(Eprint))
                Eprint = other.Eprint;
            if (string.IsNullOrWhiteSpace(Journal))
                Journal = other.Journal;

            foreach (string source in other.Sources)
            {
                if (Sources.Contains(source) == false)
                    Sources.Add(source);
            }

            return this;
        }
    }

    public sealed class MetadataFetchService
    {
        private static readonly string[] RequiredTags = { "title", "doi", "abstract", "year", "author" };
        private readonly IMetadataProvider[] _providers;

        public MetadataFetchService()
        {
            _providers = new IMetadataProvider[]
            {
                new CrossrefMetadataProvider(),
                new SemanticScholarMetadataProvider(),
                new ArxivMetadataProvider()
            };
        }

        public static bool HasAllRequiredMetadata(BibtexEntry entry)
        {
            if (entry == null)
                return false;

            foreach (string tag in RequiredTags)
            {
                if (string.IsNullOrWhiteSpace(BibtexTagService.GetTagValueIgnoreCase(entry, tag)))
                    return false;
            }

            return true;
        }

        public async Task<MetadataUpdateResult> PopulateMissingMetadataAsync(
            IEnumerable<BibtexEntry> sourceEntries,
            MetadataUpdateOptions options,
            IProgress<MetadataUpdateProgress> progress = null)
        {
            if (sourceEntries == null)
                throw new ArgumentNullException(nameof(sourceEntries));

            options = options ?? new MetadataUpdateOptions();
            List<BibtexEntry> entries = sourceEntries.Where(entry => entry != null).ToList();
            MetadataUpdateResult result = new MetadataUpdateResult
            {
                CheckedEntries = entries.Count
            };

            if (entries.Count == 0)
            {
                progress?.Report(new MetadataUpdateProgress
                {
                    Summary = "No records to process.",
                    Details = "Load or select at least one BibTeX record.",
                    Completed = 0,
                    Total = 0
                });
                return result;
            }

            List<BibtexEntry> toProcess = GetEntriesToProcess(entries, options.ScreenMode);
            result.AlreadyCompleteEntries = entries.Count - toProcess.Count;

            if (toProcess.Count == 0)
            {
                progress?.Report(new MetadataUpdateProgress
                {
                    Summary = "All records already contain required metadata.",
                    Details = "Nothing to update.",
                    Completed = entries.Count,
                    Total = entries.Count
                });
                return result;
            }

            int completed = 0;
            int threadCount = Math.Max(1, options.ThreadCount);
            SemaphoreSlim semaphore = new SemaphoreSlim(threadCount, threadCount);
            object sync = new object();
            List<Task> tasks = new List<Task>();

            foreach (BibtexEntry entry in toProcess)
            {
                await semaphore.WaitAsync().ConfigureAwait(false);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        MetadataPayload payload = await FetchMetadataAsync(entry, options).ConfigureAwait(false);
                        bool updated = ApplyMetadata(entry, payload);
                        bool complete = HasAllRequiredMetadata(entry);

                        lock (sync)
                        {
                            if (updated)
                                result.UpdatedEntries++;

                            if (complete == false)
                            {
                                result.UnresolvedEntries++;
                                result.UnresolvedRecordKeys.Add(GetRecordLabel(entry));
                            }
                        }

                        if (updated)
                            AppLog.Log($"Metadata result for '{GetRecordLabel(entry)}': updated.", AppLog.MessageType.Info);
                        else
                            AppLog.Log($"Metadata result for '{GetRecordLabel(entry)}': no new metadata found.", AppLog.MessageType.Exclamation);
                    }
                    catch (Exception ex)
                    {
                        lock (sync)
                        {
                            result.FailedEntries++;
                            result.FailedRecordKeys.Add(GetRecordLabel(entry));
                        }

                        AppLog.Log($"Metadata fetch failed for '{GetRecordLabel(entry)}': {ex.Message}", AppLog.MessageType.Error);
                    }
                    finally
                    {
                        int currentCompleted = Interlocked.Increment(ref completed);
                        progress?.Report(new MetadataUpdateProgress
                        {
                            Summary = $"Processed {currentCompleted}/{toProcess.Count}",
                            Details = GetRecordLabel(entry),
                            Completed = currentCompleted,
                            Total = toProcess.Count
                        });
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            progress?.Report(new MetadataUpdateProgress
            {
                Summary = $"Updated {result.UpdatedEntries} record(s)",
                Details = BuildCompletionDetails(result),
                Completed = toProcess.Count,
                Total = toProcess.Count
            });

            return result;
        }

        private async Task<MetadataPayload> FetchMetadataAsync(BibtexEntry entry, MetadataUpdateOptions options)
        {
            string existingDoi = CleanStoredDoi(BibtexTagService.GetTagValueIgnoreCase(entry, "doi"));
            string existingTitle = PrepareTitleForQuery(BibtexTagService.GetTagValueIgnoreCase(entry, "title"));
            string existingAuthor = PrepareAuthorForQuery(BibtexTagService.GetTagValueIgnoreCase(entry, "author"));
            MetadataPayload aggregate = null;

            DoiValueKind doiKind = GetStoredDoiKind(existingDoi);
            if (doiKind == DoiValueKind.Classic || doiKind == DoiValueKind.Arxiv)
                aggregate = await FetchUsingDoiAsync(existingDoi, existingTitle, existingAuthor, options).ConfigureAwait(false);

            if ((aggregate == null || HasAllRequiredMetadata(aggregate) == false) && string.IsNullOrWhiteSpace(existingTitle) == false)
            {
                MetadataPayload titlePayload = await FetchUsingTitleAsync(existingTitle, existingDoi, existingAuthor, options).ConfigureAwait(false);
                aggregate = aggregate == null ? titlePayload : aggregate.MergeMissing(titlePayload);
            }

            return aggregate;
        }

        private async Task<MetadataPayload> FetchUsingDoiAsync(string doi, string titleHint, string authorHint, MetadataUpdateOptions options)
        {
            MetadataPayload aggregate = null;
            foreach (IMetadataProvider provider in GetProvidersForDoi(doi))
            {
                MetadataPayload candidate = await provider.FetchByDoiAsync(doi, titleHint, authorHint, options).ConfigureAwait(false);
                candidate = EnrichCandidateWithRequestedArxivData(doi, candidate);
                if (candidate == null || IsAcceptableDoiCandidate(doi, titleHint, authorHint, candidate) == false)
                    continue;

                aggregate = aggregate == null ? candidate : aggregate.MergeMissing(candidate);
                if (HasAllRequiredMetadata(aggregate))
                    break;
            }

            return aggregate;
        }

        private async Task<MetadataPayload> FetchUsingTitleAsync(string title, string doiHint, string authorHint, MetadataUpdateOptions options)
        {
            MetadataPayload aggregate = null;
            foreach (IMetadataProvider provider in _providers)
            {
                MetadataPayload candidate = await provider.FetchByTitleAsync(title, doiHint, authorHint, options).ConfigureAwait(false);
                candidate = EnrichCandidateWithRequestedArxivData(doiHint, candidate);
                if (candidate == null || IsAcceptableTitleCandidate(title, doiHint, authorHint, candidate) == false)
                    continue;

                aggregate = aggregate == null ? candidate : aggregate.MergeMissing(candidate);
                if (HasAllRequiredMetadata(aggregate))
                    break;
            }

            return aggregate;
        }

        private static bool ApplyMetadata(BibtexEntry entry, MetadataPayload payload)
        {
            if (entry == null || payload == null)
                return false;

            bool updated = false;
            updated |= SetTagIfMissing(entry, "title", payload.Title);
            updated |= SetTagIfMissing(entry, "doi", payload.Doi);
            updated |= SetTagIfMissing(entry, "abstract", payload.Abstract);
            updated |= SetTagIfMissing(entry, "year", payload.Year);
            updated |= SetTagIfMissing(entry, "author", payload.Author);
            updated |= SetTagIfMissing(entry, "eprint", payload.Eprint);
            updated |= SetTagIfMissing(entry, "journal", payload.Journal);
            return updated;
        }

        private static bool SetTagIfMissing(BibtexEntry entry, string key, string value)
        {
            if (entry == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return false;

            string existing = BibtexTagService.GetTagValueIgnoreCase(entry, key);
            if (string.IsNullOrWhiteSpace(existing) == false)
                return false;

            BibtexTagService.SetSingleTagValue(entry, key, value.Trim());
            return true;
        }

        private static string BuildCompletionDetails(MetadataUpdateResult result)
        {
            return $"Already complete: {result.AlreadyCompleteEntries}, unresolved: {result.UnresolvedEntries}, failed: {result.FailedEntries}.";
        }

        private static string GetRecordLabel(BibtexEntry entry)
        {
            if (entry == null)
                return "<null>";

            string title = BibtexTagService.GetTagValueIgnoreCase(entry, "title");
            if (string.IsNullOrWhiteSpace(title) == false)
                return BibtexUtils.RemoveLatex(title).Trim();

            if (string.IsNullOrWhiteSpace(entry.Key) == false)
                return entry.Key.Trim();

            return "<unnamed record>";
        }

        private static bool HasAllRequiredMetadata(MetadataPayload payload)
        {
            if (payload == null)
                return false;

            return
                string.IsNullOrWhiteSpace(payload.Title) == false &&
                string.IsNullOrWhiteSpace(payload.Doi) == false &&
                string.IsNullOrWhiteSpace(payload.Abstract) == false &&
                string.IsNullOrWhiteSpace(payload.Year) == false &&
                string.IsNullOrWhiteSpace(payload.Author) == false;
        }

        private static List<BibtexEntry> GetEntriesToProcess(IEnumerable<BibtexEntry> entries, MetadataScreenMode screenMode)
        {
            if (entries == null)
                return new List<BibtexEntry>();

            switch (screenMode)
            {
                case MetadataScreenMode.All:
                    return entries.Where(entry => entry != null).ToList();
                case MetadataScreenMode.OnlyMissingAndArxivDois:
                    return entries
                        .Where(entry => entry != null && (HasAllRequiredMetadata(entry) == false || HasArxivDoi(entry)))
                        .ToList();
                case MetadataScreenMode.OnlyMissing:
                default:
                    return entries.Where(entry => entry != null && HasAllRequiredMetadata(entry) == false).ToList();
            }
        }

        private static bool HasArxivDoi(BibtexEntry entry)
        {
            return entry != null &&
                GetStoredDoiKind(BibtexTagService.GetTagValueIgnoreCase(entry, "doi")) == DoiValueKind.Arxiv;
        }

        private IEnumerable<IMetadataProvider> GetProvidersForDoi(string doi)
        {
            if (IsCanonicalArxivDoi(doi) == false)
                return _providers;

            return new IMetadataProvider[]
            {
                _providers.OfType<ArxivMetadataProvider>().First(),
                _providers.OfType<CrossrefMetadataProvider>().First(),
                _providers.OfType<SemanticScholarMetadataProvider>().First()
            };
        }

        private static bool IsAcceptableDoiCandidate(string requestedDoi, string titleHint, string authorHint, MetadataPayload candidate)
        {
            if (candidate == null)
                return false;

            if (DoisMatch(requestedDoi, candidate.Doi))
                return CandidateMatchesHints(titleHint, authorHint, candidate);

            return GetStoredDoiKind(requestedDoi) == DoiValueKind.Arxiv &&
                GetStoredDoiKind(candidate.Doi) == DoiValueKind.Classic &&
                CandidateMatchesHints(titleHint, authorHint, candidate);
        }

        private static bool IsAcceptableTitleCandidate(string requestedTitle, string doiHint, string authorHint, MetadataPayload candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.Title))
                return false;

            if (TitlesMatch(requestedTitle, candidate.Title) == false)
                return false;

            if (AuthorHintConflicts(authorHint, candidate.Author))
                return false;

            if (string.IsNullOrWhiteSpace(doiHint) || string.IsNullOrWhiteSpace(candidate.Doi))
                return true;

            return DoisMatch(doiHint, candidate.Doi);
        }

        private static string CleanStoredDoi(string doi)
        {
            return CleanValue(doi);
        }

        private static DoiValueKind GetStoredDoiKind(string doi)
        {
            string storedDoi = CleanStoredDoi(doi);
            if (string.IsNullOrWhiteSpace(storedDoi))
                return DoiValueKind.Empty;

            if (IsCanonicalArxivDoi(storedDoi))
                return DoiValueKind.Arxiv;

            return IsClassicDoi(storedDoi) ? DoiValueKind.Classic : DoiValueKind.Invalid;
        }

        private static bool IsClassicDoi(string doi)
        {
            return string.IsNullOrWhiteSpace(doi) == false &&
                Regex.IsMatch(doi, @"^10\.\d{4,9}/\S+$", RegexOptions.IgnoreCase);
        }

        private static bool IsCanonicalArxivDoi(string doi)
        {
            return string.IsNullOrWhiteSpace(doi) == false &&
                Regex.IsMatch(
                    doi,
                    @"^10\.48550/arxiv\.(?:\d{4}\.\d{4,5}|[a-z\-]+(?:\.[a-z\-]+)?/\d{7})(v\d+)?$",
                    RegexOptions.IgnoreCase);
        }

        private static bool DoisMatch(string requestedDoi, string candidateDoi)
        {
            string left = CleanStoredDoi(requestedDoi);
            string right = CleanStoredDoi(candidateDoi);
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            DoiValueKind leftKind = GetStoredDoiKind(left);
            DoiValueKind rightKind = GetStoredDoiKind(right);
            if (leftKind == DoiValueKind.Invalid || rightKind == DoiValueKind.Invalid || leftKind != rightKind)
                return false;

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static MetadataPayload EnrichCandidateWithRequestedArxivData(string requestedDoi, MetadataPayload candidate)
        {
            if (candidate == null)
                return null;

            string requested = CleanStoredDoi(requestedDoi);
            if (GetStoredDoiKind(requested) != DoiValueKind.Arxiv)
                return candidate;

            if (string.IsNullOrWhiteSpace(candidate.Eprint))
                candidate.Eprint = ExtractArxivIdentifierFromCanonicalDoi(requested);

            return candidate;
        }

        private static string ExtractArxivIdentifierFromCanonicalDoi(string doi)
        {
            string storedDoi = CleanStoredDoi(doi);
            if (IsCanonicalArxivDoi(storedDoi) == false)
                return null;

            Match match = Regex.Match(storedDoi, @"^10\.48550/arxiv\.(.+)$", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }

        private static string NormalizeProviderDoi(string doi)
        {
            string value = CleanStoredDoi(doi);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string arxivIdentifier = TryNormalizeProviderArxivIdentifierFromDoi(value);
            if (string.IsNullOrWhiteSpace(arxivIdentifier) == false)
                return BuildCanonicalArxivDoi(arxivIdentifier);

            return IsClassicDoi(value) ? value.ToLowerInvariant() : value;
        }

        private static string NormalizeProviderArxivIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string normalized = value.Trim();
            normalized = Regex.Replace(normalized, @"^arxiv:\s*", string.Empty, RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"^https?://arxiv\.org/(abs|pdf)/", string.Empty, RegexOptions.IgnoreCase);
            normalized = normalized.Trim().TrimEnd('/', '.', ',', ';');

            return Regex.IsMatch(
                normalized,
                @"^(?:\d{4}\.\d{4,5}|[a-z\-]+(?:\.[a-z\-]+)?/\d{7})(v\d+)?$",
                RegexOptions.IgnoreCase)
                ? normalized.ToLowerInvariant()
                : null;
        }

        private static string BuildCanonicalArxivDoi(string arxivIdentifier)
        {
            string normalizedIdentifier = NormalizeProviderArxivIdentifier(arxivIdentifier);
            return string.IsNullOrWhiteSpace(normalizedIdentifier)
                ? null
                : "10.48550/arXiv." + normalizedIdentifier;
        }

        private static string TryNormalizeProviderArxivIdentifierFromDoi(string value)
        {
            string storedDoi = CleanStoredDoi(value);
            if (string.IsNullOrWhiteSpace(storedDoi))
                return null;

            Match match = Regex.Match(storedDoi, @"^10\.48550/arxiv[\.:](.+)$", RegexOptions.IgnoreCase);
            return match.Success ? NormalizeProviderArxivIdentifier(match.Groups[1].Value) : null;
        }

        private static bool CandidateMatchesHints(string titleHint, string authorHint, MetadataPayload candidate)
        {
            if (candidate == null)
                return false;

            if (string.IsNullOrWhiteSpace(titleHint) == false &&
                string.IsNullOrWhiteSpace(candidate.Title) == false &&
                TitlesMatch(titleHint, candidate.Title) == false)
                return false;

            return AuthorHintConflicts(authorHint, candidate.Author) == false;
        }

        private static bool AuthorHintConflicts(string authorHint, string candidateAuthor)
        {
            if (string.IsNullOrWhiteSpace(authorHint) || string.IsNullOrWhiteSpace(candidateAuthor))
                return false;

            return AuthorsMatch(authorHint, candidateAuthor) == false;
        }

        private static string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            string normalized = BibtexUtils.RemoveLatex(title);
            normalized = Regex.Replace(normalized, "<.*?>", " ");
            normalized = normalized.ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"[^a-z0-9]+", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }

        private static string PrepareTitleForQuery(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            string prepared = BibtexUtils.RemoveLatex(title);
            prepared = Regex.Replace(prepared, "<.*?>", " ");
            prepared = System.Net.WebUtility.HtmlDecode(prepared);
            prepared = Regex.Replace(prepared, @"\s+", " ").Trim();
            return prepared;
        }

        private static string PrepareAuthorForQuery(string author)
        {
            if (string.IsNullOrWhiteSpace(author))
                return null;

            string prepared = BibtexUtils.RemoveLatex(author);
            prepared = Regex.Replace(prepared, "<.*?>", " ");
            prepared = System.Net.WebUtility.HtmlDecode(prepared);
            prepared = Regex.Replace(prepared, @"\s+", " ").Trim();
            return prepared;
        }

        private static bool TitlesMatch(string left, string right)
        {
            string normalizedLeft = NormalizeTitle(left);
            string normalizedRight = NormalizeTitle(right);

            if (string.IsNullOrWhiteSpace(normalizedLeft) || string.IsNullOrWhiteSpace(normalizedRight))
                return false;

            if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
                return true;

            if (normalizedLeft.Length >= 20 && normalizedRight.Contains(normalizedLeft))
                return true;

            if (normalizedRight.Length >= 20 && normalizedLeft.Contains(normalizedRight))
                return true;

            HashSet<string> leftTokens = new HashSet<string>(normalizedLeft.Split(' ').Where(token => token.Length > 2));
            HashSet<string> rightTokens = new HashSet<string>(normalizedRight.Split(' ').Where(token => token.Length > 2));
            if (leftTokens.Count == 0 || rightTokens.Count == 0)
                return false;

            int intersection = leftTokens.Intersect(rightTokens).Count();
            double diceCoefficient = (2.0 * intersection) / (leftTokens.Count + rightTokens.Count);
            return diceCoefficient >= 0.8;
        }

        private static bool AuthorsMatch(string left, string right)
        {
            string normalizedLeft = NormalizeAuthor(left);
            string normalizedRight = NormalizeAuthor(right);

            if (string.IsNullOrWhiteSpace(normalizedLeft) || string.IsNullOrWhiteSpace(normalizedRight))
                return false;

            if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
                return true;

            List<string> leftKeys = ExtractAuthorKeys(left);
            List<string> rightKeys = ExtractAuthorKeys(right);
            if (leftKeys.Count == 0 || rightKeys.Count == 0)
                return false;

            if (string.Equals(leftKeys[0], rightKeys[0], StringComparison.Ordinal))
                return true;

            int overlap = leftKeys.Intersect(rightKeys).Count();
            int baseline = Math.Min(leftKeys.Count, rightKeys.Count);
            return baseline > 0 && ((double)overlap / baseline) >= 0.5;
        }

        private static string NormalizeAuthor(string author)
        {
            if (string.IsNullOrWhiteSpace(author))
                return null;

            string normalized = BibtexUtils.RemoveLatex(author);
            normalized = Regex.Replace(normalized, "<.*?>", " ");
            normalized = System.Net.WebUtility.HtmlDecode(normalized);
            normalized = normalized.ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"\band\b", " ");
            normalized = Regex.Replace(normalized, @"[^a-z0-9]+", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }

        private static List<string> ExtractAuthorKeys(string authors)
        {
            List<string> keys = new List<string>();
            if (string.IsNullOrWhiteSpace(authors))
                return keys;

            string normalized = PrepareAuthorForQuery(authors);
            string[] parts = Regex.Split(normalized, @"\s+and\s+|;");
            foreach (string part in parts)
            {
                string author = part?.Trim();
                if (string.IsNullOrWhiteSpace(author))
                    continue;

                string key = ExtractAuthorKey(author);
                if (string.IsNullOrWhiteSpace(key) == false && keys.Contains(key) == false)
                    keys.Add(key);
            }

            return keys;
        }

        private static string ExtractAuthorKey(string author)
        {
            if (string.IsNullOrWhiteSpace(author))
                return null;

            string cleanAuthor = author.Trim();
            if (cleanAuthor.Contains(","))
            {
                string surname = cleanAuthor.Split(',').FirstOrDefault();
                return NormalizeAuthor(surname);
            }

            string normalized = NormalizeAuthor(cleanAuthor);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            string[] parts = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.LastOrDefault();
        }

        private abstract class MetadataProviderBase : IMetadataProvider
        {
            private static readonly Lazy<HttpClient> HttpClientFactory = new Lazy<HttpClient>(() =>
            {
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(20);
                return client;
            });

            public abstract string Name { get; }

            public abstract Task<MetadataPayload> FetchByDoiAsync(string doi, string titleHint, string authorHint, MetadataUpdateOptions options);
            public abstract Task<MetadataPayload> FetchByTitleAsync(string title, string doiHint, string authorHint, MetadataUpdateOptions options);

            protected HttpClient HttpClient
            {
                get { return HttpClientFactory.Value; }
            }

            protected async Task<JObject> GetJsonAsync(string url, MetadataUpdateOptions options)
            {
                using (HttpRequestMessage request = CreateRequest(url, options))
                using (HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode == false)
                        return null;

                    string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(content))
                        return null;

                    return JObject.Parse(content);
                }
            }

            protected async Task<XDocument> GetXmlAsync(string url, MetadataUpdateOptions options)
            {
                using (HttpRequestMessage request = CreateRequest(url, options))
                using (HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode == false)
                        return null;

                    string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(content))
                        return null;

                    return XDocument.Parse(content);
                }
            }

            protected MetadataPayload AddSource(MetadataPayload payload)
            {
                if (payload == null)
                    return null;

                if (payload.Sources.Contains(Name) == false)
                    payload.Sources.Add(Name);

                return payload;
            }

            private HttpRequestMessage CreateRequest(string url, MetadataUpdateOptions options)
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent", BuildUserAgent(options?.ContactEmail));
                request.Headers.TryAddWithoutValidation("Accept", "application/json, application/atom+xml, application/xml, text/xml");
                return request;
            }

            private static string BuildUserAgent(string email)
            {
                string sanitizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
                if (string.IsNullOrWhiteSpace(sanitizedEmail))
                    return "ScientificReviews/1.0";

                return $"ScientificReviews/1.0 (mailto:{sanitizedEmail})";
            }
        }

        private sealed class CrossrefMetadataProvider : MetadataProviderBase
        {
            public override string Name => "Crossref";

            public override async Task<MetadataPayload> FetchByDoiAsync(string doi, string titleHint, string authorHint, MetadataUpdateOptions options)
            {
                if (string.IsNullOrWhiteSpace(doi))
                    return null;

                string url = $"https://api.crossref.org/works/{Uri.EscapeDataString(doi)}";
                if (string.IsNullOrWhiteSpace(options?.ContactEmail) == false)
                    url += $"?mailto={Uri.EscapeDataString(options.ContactEmail.Trim())}";

                JObject json = await GetJsonAsync(url, options).ConfigureAwait(false);
                return AddSource(ParseCrossrefMessage(json?["message"]));
            }

            public override async Task<MetadataPayload> FetchByTitleAsync(string title, string doiHint, string authorHint, MetadataUpdateOptions options)
            {
                if (string.IsNullOrWhiteSpace(title))
                    return null;

                string url = $"https://api.crossref.org/works?query.title={Uri.EscapeDataString(title)}&rows=5";
                if (string.IsNullOrWhiteSpace(options?.ContactEmail) == false)
                    url += $"&mailto={Uri.EscapeDataString(options.ContactEmail.Trim())}";

                JObject json = await GetJsonAsync(url, options).ConfigureAwait(false);
                JArray items = json?["message"]?["items"] as JArray;
                return AddSource(SelectBestCrossrefCandidate(items, title, authorHint));
            }

            private static MetadataPayload SelectBestCrossrefCandidate(JArray items, string title, string authorHint)
            {
                if (items == null || items.Count == 0)
                    return null;

                MetadataPayload best = null;
                double bestScore = 0;

                foreach (JToken item in items)
                {
                    MetadataPayload candidate = ParseCrossrefMessage(item);
                    if (candidate == null || TitlesMatch(title, candidate.Title) == false || AuthorHintConflicts(authorHint, candidate.Author))
                        continue;

                    double score = GetCandidateScore(title, authorHint, candidate);
                    if (score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }

                return best;
            }

            private static MetadataPayload ParseCrossrefMessage(JToken message)
            {
                if (message == null)
                    return null;

                string title = FirstString(message["title"]);
                string doi = NormalizeProviderDoi(CleanValue(message["DOI"]));
                string summary = CleanAbstract(message["abstract"]);
                string year = ExtractCrossrefYear(message);
                string author = JoinCrossrefAuthors(message["author"] as JArray);
                string journal = FirstString(message["container-title"]);
                string eprint = ExtractArxivIdentifierFromCanonicalDoi(doi);

                if (string.IsNullOrWhiteSpace(title) &&
                    string.IsNullOrWhiteSpace(doi) &&
                    string.IsNullOrWhiteSpace(summary) &&
                    string.IsNullOrWhiteSpace(year) &&
                    string.IsNullOrWhiteSpace(author))
                    return null;

                return new MetadataPayload
                {
                    Title = title,
                    Doi = doi,
                    Abstract = summary,
                    Year = year,
                    Author = author,
                    Journal = journal,
                    Eprint = eprint
                };
            }

            private static string ExtractCrossrefYear(JToken message)
            {
                string year = ExtractDatePartYear(message?["published-print"]);
                if (string.IsNullOrWhiteSpace(year) == false)
                    return year;

                year = ExtractDatePartYear(message?["published-online"]);
                if (string.IsNullOrWhiteSpace(year) == false)
                    return year;

                year = ExtractDatePartYear(message?["issued"]);
                if (string.IsNullOrWhiteSpace(year) == false)
                    return year;

                year = ExtractDatePartYear(message?["created"]);
                if (string.IsNullOrWhiteSpace(year) == false)
                    return year;

                year = ExtractDatePartYear(message?["published"]);
                if (string.IsNullOrWhiteSpace(year) == false)
                    return year;

                return ExtractDatePartYear(message?["deposited"]);
            }

            private static string ExtractDatePartYear(JToken dateToken)
            {
                JArray outerArray = dateToken?["date-parts"] as JArray;
                if (outerArray == null || outerArray.Count == 0)
                    return null;

                JArray parts = outerArray[0] as JArray;
                if (parts == null || parts.Count == 0)
                    return null;

                return CleanValue(parts[0]);
            }

            private static string JoinCrossrefAuthors(JArray authors)
            {
                if (authors == null || authors.Count == 0)
                    return null;

                List<string> names = new List<string>();
                foreach (JToken author in authors)
                {
                    string family = CleanValue(author?["family"]);
                    string given = CleanValue(author?["given"]);
                    string literal = CleanValue(author?["name"] ?? author?["literal"]);
                    string name = string.IsNullOrWhiteSpace(literal) == false
                        ? literal
                        : string.IsNullOrWhiteSpace(family) == false
                            ? string.IsNullOrWhiteSpace(given) == false ? family + ", " + given : family
                            : given;

                    if (string.IsNullOrWhiteSpace(name) == false)
                        names.Add(name);
                }

                return names.Count == 0 ? null : string.Join(" and ", names);
            }
        }

        private sealed class SemanticScholarMetadataProvider : MetadataProviderBase
        {
            public override string Name => "Semantic Scholar";

            public override async Task<MetadataPayload> FetchByDoiAsync(string doi, string titleHint, string authorHint, MetadataUpdateOptions options)
            {
                if (string.IsNullOrWhiteSpace(doi))
                    return null;

                string url = $"https://api.semanticscholar.org/graph/v1/paper/DOI:{Uri.EscapeDataString(doi)}?fields=title,abstract,year,externalIds,journal,authors";
                JObject json = await GetJsonAsync(url, options).ConfigureAwait(false);
                return AddSource(ParseSemanticScholarPayload(json));
            }

            public override async Task<MetadataPayload> FetchByTitleAsync(string title, string doiHint, string authorHint, MetadataUpdateOptions options)
            {
                if (string.IsNullOrWhiteSpace(title))
                    return null;

                string url = $"https://api.semanticscholar.org/graph/v1/paper/search?query={Uri.EscapeDataString(title)}&limit=5&fields=title,abstract,year,externalIds,journal,authors";
                JObject json = await GetJsonAsync(url, options).ConfigureAwait(false);
                JArray items = json?["data"] as JArray;
                return AddSource(SelectBestSemanticScholarCandidate(items, title, authorHint));
            }

            private static MetadataPayload SelectBestSemanticScholarCandidate(JArray items, string title, string authorHint)
            {
                if (items == null || items.Count == 0)
                    return null;

                MetadataPayload best = null;
                double bestScore = 0;

                foreach (JToken item in items)
                {
                    MetadataPayload candidate = ParseSemanticScholarPayload(item);
                    if (candidate == null || TitlesMatch(title, candidate.Title) == false || AuthorHintConflicts(authorHint, candidate.Author))
                        continue;

                    double score = GetCandidateScore(title, authorHint, candidate);
                    if (score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }

                return best;
            }

            private static MetadataPayload ParseSemanticScholarPayload(JToken token)
            {
                if (token == null)
                    return null;

                string title = CleanValue(token["title"]);
                string summary = CleanAbstract(token["abstract"]);
                string year = CleanValue(token["year"]);
                string doi = NormalizeProviderDoi(CleanValue(token["externalIds"]?["DOI"]));
                string eprint = NormalizeProviderArxivIdentifier(CleanValue(token["externalIds"]?["ArXiv"]));
                string journal = CleanValue(token["journal"]?["name"]);
                string author = JoinSemanticScholarAuthors(token["authors"] as JArray);

                if (string.IsNullOrWhiteSpace(title) &&
                    string.IsNullOrWhiteSpace(doi) &&
                    string.IsNullOrWhiteSpace(summary) &&
                    string.IsNullOrWhiteSpace(year) &&
                    string.IsNullOrWhiteSpace(author))
                    return null;

                if (string.IsNullOrWhiteSpace(doi) && string.IsNullOrWhiteSpace(eprint) == false)
                    doi = BuildCanonicalArxivDoi(eprint);

                return new MetadataPayload
                {
                    Title = title,
                    Doi = doi,
                    Abstract = summary,
                    Year = year,
                    Author = author,
                    Eprint = eprint,
                    Journal = journal
                };
            }

            private static string JoinSemanticScholarAuthors(JArray authors)
            {
                if (authors == null || authors.Count == 0)
                    return null;

                List<string> names = authors
                    .Select(author => CleanValue(author?["name"]))
                    .Where(name => string.IsNullOrWhiteSpace(name) == false)
                    .ToList();

                return names.Count == 0 ? null : string.Join(" and ", names);
            }
        }

        private sealed class ArxivMetadataProvider : MetadataProviderBase
        {
            private static readonly XNamespace AtomNamespace = "http://www.w3.org/2005/Atom";
            private static readonly XNamespace ArxivNamespace = "http://arxiv.org/schemas/atom";

            public override string Name => "arXiv";

            public override async Task<MetadataPayload> FetchByDoiAsync(string doi, string titleHint, string authorHint, MetadataUpdateOptions options)
            {
                string arxivId = ExtractArxivIdentifierFromCanonicalDoi(doi);
                if (string.IsNullOrWhiteSpace(arxivId))
                    return null;

                MetadataPayload payload = await FetchByArxivIdAsync(arxivId, options).ConfigureAwait(false);
                if (payload != null)
                    return AddSource(payload);

                string versionlessArxivId = StripArxivVersion(arxivId);
                if (string.Equals(arxivId, versionlessArxivId, StringComparison.OrdinalIgnoreCase))
                    return null;

                return AddSource(await FetchByArxivIdAsync(versionlessArxivId, options).ConfigureAwait(false));
            }

            public override async Task<MetadataPayload> FetchByTitleAsync(string title, string doiHint, string authorHint, MetadataUpdateOptions options)
            {
                if (string.IsNullOrWhiteSpace(title))
                    return null;

                string query = $"ti:\"{title}\"";
                string url = $"https://export.arxiv.org/api/query?search_query={Uri.EscapeDataString(query)}&start=0&max_results=5";
                XDocument xml = await GetXmlAsync(url, options).ConfigureAwait(false);
                List<MetadataPayload> candidates = ParseArxivEntries(xml);

                MetadataPayload best = null;
                double bestScore = 0;
                foreach (MetadataPayload candidate in candidates)
                {
                    if (candidate == null || TitlesMatch(title, candidate.Title) == false || AuthorHintConflicts(authorHint, candidate.Author))
                        continue;

                    double score = GetCandidateScore(title, authorHint, candidate);
                    if (score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }

                return AddSource(best);
            }

            private async Task<MetadataPayload> FetchByArxivIdAsync(string arxivId, MetadataUpdateOptions options)
            {
                string url = $"https://export.arxiv.org/api/query?id_list={Uri.EscapeDataString(arxivId)}";
                XDocument xml = await GetXmlAsync(url, options).ConfigureAwait(false);
                return ParseArxivEntries(xml).FirstOrDefault();
            }

            private static List<MetadataPayload> ParseArxivEntries(XDocument xml)
            {
                List<MetadataPayload> payloads = new List<MetadataPayload>();
                if (xml?.Root == null)
                    return payloads;

                foreach (XElement entry in xml.Root.Elements(AtomNamespace + "entry"))
                {
                    string title = CleanValue(entry.Element(AtomNamespace + "title")?.Value);
                    string summary = CleanAbstract(entry.Element(AtomNamespace + "summary")?.Value);
                    string published = CleanValue(entry.Element(AtomNamespace + "published")?.Value);
                    string updated = CleanValue(entry.Element(AtomNamespace + "updated")?.Value);
                    string year = ExtractYearFromDate(published) ?? ExtractYearFromDate(updated);
                    string doi = NormalizeProviderDoi(CleanValue(entry.Element(ArxivNamespace + "doi")?.Value));
                    string journal = CleanValue(entry.Element(ArxivNamespace + "journal_ref")?.Value);
                    string id = CleanValue(entry.Element(AtomNamespace + "id")?.Value);
                    string eprint = NormalizeProviderArxivIdentifier(id);
                    string author = JoinArxivAuthors(entry.Elements(AtomNamespace + "author"));

                    if (string.IsNullOrWhiteSpace(doi) && string.IsNullOrWhiteSpace(eprint) == false)
                        doi = BuildCanonicalArxivDoi(eprint);

                    if (string.IsNullOrWhiteSpace(title) &&
                        string.IsNullOrWhiteSpace(doi) &&
                        string.IsNullOrWhiteSpace(summary) &&
                        string.IsNullOrWhiteSpace(year) &&
                        string.IsNullOrWhiteSpace(author))
                        continue;

                    payloads.Add(new MetadataPayload
                    {
                        Title = title,
                        Doi = doi,
                        Abstract = summary,
                        Year = year,
                        Author = author,
                        Eprint = eprint,
                        Journal = journal
                    });
                }

                return payloads;
            }

            private static string JoinArxivAuthors(IEnumerable<XElement> authors)
            {
                if (authors == null)
                    return null;

                List<string> names = authors
                    .Select(author => CleanValue(author.Element(AtomNamespace + "name")?.Value))
                    .Where(name => string.IsNullOrWhiteSpace(name) == false)
                    .ToList();

                return names.Count == 0 ? null : string.Join(" and ", names);
            }
        }

        private static string CleanValue(object value)
        {
            string text = value?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return Regex.Replace(text.Trim(), @"\s+", " ");
        }

        private static string FirstString(JToken token)
        {
            if (token == null)
                return null;

            if (token.Type == JTokenType.Array)
                return CleanValue(token.First);

            return CleanValue(token);
        }

        private static string CleanAbstract(object value)
        {
            string text = CleanValue(value);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            text = Regex.Replace(text, "<.*?>", " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text;
        }

        private static string ExtractYearFromDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            DateTime parsedDate;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsedDate))
                return parsedDate.Year.ToString(CultureInfo.InvariantCulture);

            Match match = Regex.Match(value, @"\b(19|20)\d{2}\b");
            return match.Success ? match.Value : null;
        }

        private static string StripArxivVersion(string arxivId)
        {
            if (string.IsNullOrWhiteSpace(arxivId))
                return arxivId;

            return Regex.Replace(arxivId, @"v\d+$", string.Empty, RegexOptions.IgnoreCase);
        }

        private static double GetTitleScore(string requestedTitle, string candidateTitle)
        {
            string left = NormalizeTitle(requestedTitle);
            string right = NormalizeTitle(candidateTitle);
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return 0;

            if (string.Equals(left, right, StringComparison.Ordinal))
                return 1;

            HashSet<string> leftTokens = new HashSet<string>(left.Split(' ').Where(token => token.Length > 2));
            HashSet<string> rightTokens = new HashSet<string>(right.Split(' ').Where(token => token.Length > 2));
            if (leftTokens.Count == 0 || rightTokens.Count == 0)
                return 0;

            int intersection = leftTokens.Intersect(rightTokens).Count();
            return (2.0 * intersection) / (leftTokens.Count + rightTokens.Count);
        }

        private static double GetCandidateScore(string requestedTitle, string authorHint, MetadataPayload candidate)
        {
            double score = GetTitleScore(requestedTitle, candidate?.Title);
            if (string.IsNullOrWhiteSpace(authorHint) == false && AuthorsMatch(authorHint, candidate?.Author))
                score += 0.15;

            return score;
        }
    }
}
