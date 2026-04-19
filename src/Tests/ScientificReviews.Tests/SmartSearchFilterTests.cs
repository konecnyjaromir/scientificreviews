using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using System;

namespace ScientificReviews.Tests
{
    internal static class SmartSearchFilterTests
    {
        public static void TryParse_MatchesFieldSelectorsAndBooleanOperators()
        {
            BibtexEntry entry = CreateEntry(
                "novak2024",
                "article",
                new BibtexTag("title", "Machine learning for reviews"),
                new BibtexTag("author", "Jiri Novak"),
                new BibtexTag("year", "2024"));

            SmartSearchParseResult result = SmartSearchFilter.TryParse("title:\"machine learning\" AND author:novak AND NOT year:2023");

            AssertTrue(result.Success);
            AssertTrue(result.Filter.IsMatch(entry));
        }

        public static void TryParse_TreatsCommaAsOrAndSupportsParentheses()
        {
            BibtexEntry entry = CreateEntry(
                "vision2025",
                "inproceedings",
                new BibtexTag("title", "Computer vision pipeline"),
                new BibtexTag("keywords", "segmentation"));

            SmartSearchParseResult result = SmartSearchFilter.TryParse("(title:graph, title:vision) AND keywords:segment");

            AssertTrue(result.Success);
            AssertTrue(result.Filter.IsMatch(entry));
        }

        public static void TryParse_UsesImplicitAndBetweenAdjacentTerms()
        {
            BibtexEntry entry = CreateEntry(
                "smith2022",
                "article",
                new BibtexTag("title", "Systematic literature review"),
                new BibtexTag("journal", "Expert Systems"));

            SmartSearchParseResult result = SmartSearchFilter.TryParse("review journal:expert");

            AssertTrue(result.Success);
            AssertTrue(result.Filter.IsMatch(entry));
        }

        public static void TryParse_ReturnsValidationErrorForIncompleteField()
        {
            SmartSearchParseResult result = SmartSearchFilter.TryParse("title:");

            AssertFalse(result.Success);
            AssertTrue(string.IsNullOrWhiteSpace(result.ErrorMessage) == false);
        }

        public static void TryParse_TreatsPlainUrlAsValueInsteadOfFieldSelector()
        {
            BibtexEntry entry = CreateEntry(
                "site2026",
                "online",
                new BibtexTag("url", "https://example.com/article"),
                new BibtexTag("title", "Example article"));

            SmartSearchParseResult result = SmartSearchFilter.TryParse("https://example.com/article");

            AssertTrue(result.Success);
            AssertTrue(result.Filter.IsMatch(entry));
        }

        public static void TryParse_MatchesNumericRangeQueries()
        {
            BibtexEntry entry = CreateEntry(
                "novak2022",
                "article",
                new BibtexTag("year", "2022"));

            SmartSearchParseResult result = SmartSearchFilter.TryParse("year:2020-2025");

            AssertTrue(result.Success);
            AssertTrue(result.Filter.IsMatch(entry));
        }

        public static void TryParse_MatchesNumericComparisonsWithSpaces()
        {
            BibtexEntry entry = CreateEntry(
                "novak2026",
                "article",
                new BibtexTag("year", "2026"));

            SmartSearchParseResult result = SmartSearchFilter.TryParse("year > 2025");

            AssertTrue(result.Success);
            AssertTrue(result.Filter.IsMatch(entry));
        }

        public static void TryParse_MatchesNumericComparisonsWithoutSpaces()
        {
            BibtexEntry entry = CreateEntry(
                "novak2025",
                "article",
                new BibtexTag("year", "2025"));

            SmartSearchParseResult result = SmartSearchFilter.TryParse("year>=2025");

            AssertTrue(result.Success);
            AssertTrue(result.Filter.IsMatch(entry));
        }

        private static BibtexEntry CreateEntry(string key, string type, params BibtexTag[] tags)
        {
            return new BibtexEntry
            {
                Key = key,
                Type = type,
                Tags = tags
            };
        }

        private static void AssertTrue(bool value)
        {
            if (value == false)
                throw new InvalidOperationException("Expected true, got false.");
        }

        private static void AssertFalse(bool value)
        {
            if (value)
                throw new InvalidOperationException("Expected false, got true.");
        }
    }
}
