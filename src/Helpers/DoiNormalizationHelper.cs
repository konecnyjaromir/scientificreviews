using System;
using System.Text.RegularExpressions;

namespace ScientificReviews.Helpers
{
    public enum DoiValueKind
    {
        Empty,
        Classic,
        Arxiv,
        Invalid
    }

    public static class DoiNormalizationHelper
    {
        private const string ArxivDoiPrefix = "10.48550/arXiv.";

        public static string PrepareDoiForLookup(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string prepared = value.Trim();
            prepared = Regex.Replace(prepared, @"^https?://(dx\.)?doi\.org/", string.Empty, RegexOptions.IgnoreCase);
            prepared = Regex.Replace(prepared, @"^doi:\s*", string.Empty, RegexOptions.IgnoreCase);
            return prepared.Trim().TrimEnd('/', '.', ',', ';');
        }

        public static string NormalizeDoiValue(string doi)
        {
            string prepared = PrepareDoiForLookup(doi);
            if (string.IsNullOrWhiteSpace(prepared))
                return null;

            string arxivIdentifier = TryExtractArxivIdentifier(prepared);
            if (string.IsNullOrWhiteSpace(arxivIdentifier) == false)
                return BuildArxivDoi(arxivIdentifier);

            if (IsClassicDoiValue(prepared))
                return prepared.ToLowerInvariant();

            return prepared;
        }

        public static DoiValueKind GetDoiValueKind(string doi)
        {
            string prepared = PrepareDoiForLookup(doi);
            if (string.IsNullOrWhiteSpace(prepared))
                return DoiValueKind.Empty;

            if (string.IsNullOrWhiteSpace(TryExtractArxivIdentifier(prepared)) == false)
                return DoiValueKind.Arxiv;

            return IsClassicDoiValue(prepared) ? DoiValueKind.Classic : DoiValueKind.Invalid;
        }

        public static string NormalizeArxivIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string normalized = value.Trim();
            normalized = Regex.Replace(normalized, @"^arxiv:\s*", string.Empty, RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"^https?://arxiv\.org/(abs|pdf)/", string.Empty, RegexOptions.IgnoreCase);
            normalized = normalized.Trim().TrimEnd('/', '.', ',', ';');
            return IsArxivIdentifier(normalized) ? normalized.ToLowerInvariant() : null;
        }

        public static string BuildArxivDoi(string value)
        {
            string normalizedIdentifier = NormalizeArxivIdentifier(value);
            if (string.IsNullOrWhiteSpace(normalizedIdentifier))
                return null;

            return ArxivDoiPrefix + normalizedIdentifier;
        }

        public static string TryExtractArxivIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string normalized = NormalizeArxivIdentifier(value);
            if (string.IsNullOrWhiteSpace(normalized) == false)
                return normalized;

            string prepared = PrepareDoiForLookup(value);
            if (string.IsNullOrWhiteSpace(prepared))
                return null;

            Match doiMatch = Regex.Match(prepared, @"^10\.48550/arxiv[\.:](.+)$", RegexOptions.IgnoreCase);
            if (doiMatch.Success == false)
                return null;

            return NormalizeArxivIdentifier(doiMatch.Groups[1].Value);
        }

        public static bool IsArxivIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return Regex.IsMatch(
                value.Trim(),
                @"^(?:\d{4}\.\d{4,5}|[a-z\-]+(?:\.[a-z\-]+)?/\d{7})(v\d+)?$",
                RegexOptions.IgnoreCase);
        }

        public static bool IsClassicDoiValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return Regex.IsMatch(value.Trim(), @"^10\.\d{4,9}/\S+$", RegexOptions.IgnoreCase);
        }

        public static bool RequestedDoiMatchesCandidateDoi(string requestedDoi, string candidateDoi)
        {
            DoiValueKind requestedKind = GetDoiValueKind(requestedDoi);
            DoiValueKind candidateKind = GetDoiValueKind(candidateDoi);
            if (requestedKind == DoiValueKind.Empty ||
                requestedKind == DoiValueKind.Invalid ||
                candidateKind == DoiValueKind.Empty ||
                candidateKind == DoiValueKind.Invalid)
                return false;

            if (requestedKind != candidateKind)
                return false;

            return string.Equals(
                NormalizeDoiValue(requestedDoi),
                NormalizeDoiValue(candidateDoi),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
