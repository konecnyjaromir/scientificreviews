using ScientificReviews.Bibtex;
using System;
using System.Linq;

namespace ScientificReviews.Tests
{
    internal static class BibtexUtilsTests
    {
        public static void RemoveDuplicateEntriesByTag_RemovesDuplicateTagsBeforeMatching()
        {
            BibtexEntry entry = CreateEntry(
                "single",
                new BibtexTag("title", "Unique title"),
                new BibtexTag("author", "Alice"),
                new BibtexTag("Author", "Alice duplicate"),
                new BibtexTag("year", "2025"));

            var result = BibtexUtils.RemoveDuplicateEntriesByTag(
                new System.Collections.Generic.List<BibtexEntry> { entry },
                "title");

            AssertEqual(1, result.Count);
            AssertEqual(1, entry.Tags.Count(tag => string.Equals(tag.Key, "author", StringComparison.OrdinalIgnoreCase)));
            AssertTrue(entry.Tags.Any(tag => string.Equals(tag.Key, "year", StringComparison.OrdinalIgnoreCase)));
        }

        public static void RemoveDuplicateEntriesByTag_MergesSafelyWhenDuplicateRecordContainsDuplicateTags()
        {
            BibtexEntry firstEntry = CreateEntry(
                "first",
                new BibtexTag("title", "Shared title"),
                new BibtexTag("doi", "10.1000/first"),
                new BibtexTag("year", "2024"));

            BibtexEntry duplicateEntry = CreateEntry(
                "duplicate",
                new BibtexTag("title", "Shared title"),
                new BibtexTag("author", "Alice"),
                new BibtexTag("Author", "Alice duplicate"),
                new BibtexTag("journal", "Testing Journal"));

            var result = BibtexUtils.RemoveDuplicateEntriesByTag(
                new System.Collections.Generic.List<BibtexEntry> { firstEntry, duplicateEntry },
                "title");

            AssertEqual(1, result.Count);
            AssertSame(firstEntry, result[0]);
            AssertEqual(1, firstEntry.Tags.Count(tag => string.Equals(tag.Key, "author", StringComparison.OrdinalIgnoreCase)));
            AssertTrue(firstEntry.Tags.Any(tag => string.Equals(tag.Key, "journal", StringComparison.OrdinalIgnoreCase)));
            AssertTrue(firstEntry.Tags.Any(tag => string.Equals(tag.Key, "doi", StringComparison.OrdinalIgnoreCase)));
        }

        private static BibtexEntry CreateEntry(string key, params BibtexTag[] tags)
        {
            return new BibtexEntry
            {
                Key = key,
                Type = "article",
                Tags = tags
            };
        }

        private static void AssertEqual(int expected, int actual)
        {
            if (expected != actual)
                throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }

        private static void AssertTrue(bool value)
        {
            if (value == false)
                throw new InvalidOperationException("Expected true, got false.");
        }

        private static void AssertSame(object expected, object actual)
        {
            if (ReferenceEquals(expected, actual) == false)
                throw new InvalidOperationException("Expected both references to point to the same object.");
        }
    }
}
