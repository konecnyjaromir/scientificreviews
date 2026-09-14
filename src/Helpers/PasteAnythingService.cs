using ScientificReviews.Bibtex;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ScientificReviews.Helpers
{
    public enum PasteAnythingEntryKind
    {
        Bibtex,
        Doi,
        Url,
        Title
    }

    public sealed class PasteAnythingParseResult
    {
        public BibtexEntry[] Entries { get; set; } = Array.Empty<BibtexEntry>();
        public PasteAnythingEntryKind[] EntryKinds { get; set; } = Array.Empty<PasteAnythingEntryKind>();
        public bool ParsedAsBibtex { get; set; }
        public int SkippedItems { get; set; }
        public int DoiEntries { get; set; }
        public int UrlEntries { get; set; }
        public int TitleEntries { get; set; }
    }

    public sealed class PasteAnythingService
    {
        public PasteAnythingParseResult Parse(string rawText)
        {
            PasteAnythingParseResult bibtexResult = TryParseBibtex(rawText);
            if (bibtexResult != null)
                return bibtexResult;

            return ParseStructuredText(rawText);
        }

        private PasteAnythingParseResult TryParseBibtex(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return null;

            try
            {
                BibtexParser parser = new BibtexParser();
                BibtexEntry[] entries = parser.ParseFile(rawText);
                if (entries == null || entries.Length == 0)
                    return null;

                return new PasteAnythingParseResult
                {
                    Entries = entries,
                    EntryKinds = Enumerable.Repeat(PasteAnythingEntryKind.Bibtex, entries.Length).ToArray(),
                    ParsedAsBibtex = true
                };
            }
            catch
            {
                return null;
            }
        }

        private PasteAnythingParseResult ParseStructuredText(string rawText)
        {
            PasteAnythingParseResult result = new PasteAnythingParseResult();
            if (string.IsNullOrWhiteSpace(rawText))
                return result;

            List<BibtexEntry> entries = new List<BibtexEntry>();
            List<PasteAnythingEntryKind> kinds = new List<PasteAnythingEntryKind>();
            string[] items = rawText
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(item => (item ?? string.Empty).Trim())
                .Where(item => string.IsNullOrWhiteSpace(item) == false)
                .ToArray();

            for (int i = 0; i < items.Length; i++)
            {
                string item = items[i];
                BibtexEntry entry = TryCreateDoiEntry(item, i + 1);
                if (entry != null)
                {
                    entries.Add(entry);
                    kinds.Add(PasteAnythingEntryKind.Doi);
                    result.DoiEntries++;
                    continue;
                }

                entry = TryCreateUrlEntry(item, i + 1);
                if (entry != null)
                {
                    entries.Add(entry);
                    kinds.Add(PasteAnythingEntryKind.Url);
                    result.UrlEntries++;
                    continue;
                }

                entry = TryCreateTitleEntry(item, i + 1);
                if (entry != null)
                {
                    entries.Add(entry);
                    kinds.Add(PasteAnythingEntryKind.Title);
                    result.TitleEntries++;
                    continue;
                }

                result.SkippedItems++;
            }

            result.Entries = entries.ToArray();
            result.EntryKinds = kinds.ToArray();
            return result;
        }

        private BibtexEntry TryCreateDoiEntry(string value, int index)
        {
            string arxivIdentifier = DoiNormalizationHelper.TryExtractArxivIdentifier(value);
            if (string.IsNullOrWhiteSpace(arxivIdentifier) == false)
            {
                BibtexEntry arxivEntry = CreateEntry("misc", "paste_misc", index);
                BibtexTagService.SetSingleTagValue(arxivEntry, "doi", DoiNormalizationHelper.BuildArxivDoi(arxivIdentifier));
                BibtexTagService.SetSingleTagValue(arxivEntry, "eprint", arxivIdentifier);
                return arxivEntry;
            }

            string prepared = DoiNormalizationHelper.PrepareDoiForLookup(value);
            DoiValueKind kind = DoiNormalizationHelper.GetDoiValueKind(prepared);
            if (kind != DoiValueKind.Classic && kind != DoiValueKind.Arxiv)
                return null;

            string normalizedDoi = DoiNormalizationHelper.NormalizeDoiValue(prepared);
            if (string.IsNullOrWhiteSpace(normalizedDoi))
                return null;

            BibtexEntry entry = CreateEntry("misc", "paste_misc", index);
            BibtexTagService.SetSingleTagValue(entry, "doi", normalizedDoi);

            string eprint = DoiNormalizationHelper.TryExtractArxivIdentifier(normalizedDoi);
            if (string.IsNullOrWhiteSpace(eprint) == false)
                BibtexTagService.SetSingleTagValue(entry, "eprint", eprint);

            return entry;
        }

        private BibtexEntry TryCreateUrlEntry(string value, int index)
        {
            Uri uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri) == false)
                return null;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return null;

            string absoluteUrl = uri.AbsoluteUri;
            BibtexEntry doiEntry = TryCreateDoiEntry(absoluteUrl, index);
            if (doiEntry != null)
                return doiEntry;

            string normalizedUrl = NormalizeUrl(uri);
            if (string.IsNullOrWhiteSpace(normalizedUrl))
                return null;

            BibtexEntry entry = CreateEntry("online", "paste_online", index);
            BibtexTagService.SetSingleTagValue(entry, "url", normalizedUrl);
            BibtexTagService.SetSingleTagValue(entry, "urldate", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            BibtexTagService.SetSingleTagValue(entry, "note", "[online]");
            return entry;
        }

        private BibtexEntry TryCreateTitleEntry(string value, int index)
        {
            string title = NormalizeTitle(value);
            if (string.IsNullOrWhiteSpace(title))
                return null;

            BibtexEntry entry = CreateEntry("misc", "paste_misc", index);
            BibtexTagService.SetSingleTagValue(entry, "title", title);
            return entry;
        }

        private static BibtexEntry CreateEntry(string type, string keyPrefix, int index)
        {
            string key = string.Format(
                CultureInfo.InvariantCulture,
                "{0}_{1}_{2}_{3}",
                keyPrefix,
                DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                index,
                Guid.NewGuid().ToString("N").Substring(0, 8));

            return new BibtexEntry
            {
                Type = string.IsNullOrWhiteSpace(type) ? "misc" : type.Trim().ToLowerInvariant(),
                Key = key,
                Tags = new BibtexTag[0]
            };
        }

        private static string NormalizeUrl(Uri uri)
        {
            if (uri == null || uri.IsAbsoluteUri == false)
                return null;

            UriBuilder builder = new UriBuilder(uri)
            {
                Host = uri.Host.ToLowerInvariant(),
                Scheme = uri.Scheme.ToLowerInvariant()
            };

            if ((builder.Scheme == Uri.UriSchemeHttp && builder.Port == 80) ||
                (builder.Scheme == Uri.UriSchemeHttps && builder.Port == 443))
                builder.Port = -1;

            return builder.Uri.AbsoluteUri;
        }

        private static string NormalizeTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string title = value.Trim();
            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
    }
}
