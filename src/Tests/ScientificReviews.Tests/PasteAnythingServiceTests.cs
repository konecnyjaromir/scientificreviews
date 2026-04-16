using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using System;

namespace ScientificReviews.Tests
{
    internal static class PasteAnythingServiceTests
    {
        public static void Parse_ReturnsBibtexEntriesWhenClipboardContainsBibtex()
        {
            PasteAnythingService service = new PasteAnythingService();
            PasteAnythingParseResult result = service.Parse("@article{mykey, title={Hello World}, doi={10.1000/xyz}}");

            AssertTrue(result.ParsedAsBibtex);
            AssertEqual(1, result.Entries.Length);
            AssertEqual("article", result.Entries[0].Type);
            AssertEqual("mykey", result.Entries[0].Key);
        }

        public static void Parse_CreatesMiscEntryForClassicDoi()
        {
            PasteAnythingService service = new PasteAnythingService();
            PasteAnythingParseResult result = service.Parse("https://doi.org/10.1000/XYZ");

            AssertFalse(result.ParsedAsBibtex);
            AssertEqual(1, result.Entries.Length);
            AssertEqual("misc", result.Entries[0].Type);
            AssertEqual("10.1000/xyz", BibtexTagService.GetTagValueIgnoreCase(result.Entries[0], "doi"));
        }

        public static void Parse_CreatesMiscEntryForArxivUrlAndEprint()
        {
            PasteAnythingService service = new PasteAnythingService();
            PasteAnythingParseResult result = service.Parse("https://arxiv.org/abs/2204.01691");

            AssertEqual(1, result.Entries.Length);
            AssertEqual("misc", result.Entries[0].Type);
            AssertEqual("10.48550/arXiv.2204.01691", BibtexTagService.GetTagValueIgnoreCase(result.Entries[0], "doi"));
            AssertEqual("2204.01691", BibtexTagService.GetTagValueIgnoreCase(result.Entries[0], "eprint"));
        }

        public static void Parse_CreatesOnlineEntryForWebUrl()
        {
            PasteAnythingService service = new PasteAnythingService();
            PasteAnythingParseResult result = service.Parse("https://example.com/article");

            AssertEqual(1, result.Entries.Length);
            AssertEqual("online", result.Entries[0].Type);
            AssertEqual("https://example.com/article", BibtexTagService.GetTagValueIgnoreCase(result.Entries[0], "url"));
            AssertEqual("[online]", BibtexTagService.GetTagValueIgnoreCase(result.Entries[0], "note"));
            AssertTrue(string.IsNullOrWhiteSpace(BibtexTagService.GetTagValueIgnoreCase(result.Entries[0], "urldate")) == false);
        }

        public static void Parse_CreatesMiscEntryForTitleFallback()
        {
            PasteAnythingService service = new PasteAnythingService();
            PasteAnythingParseResult result = service.Parse("A study on resilient metadata extraction");

            AssertEqual(1, result.Entries.Length);
            AssertEqual("misc", result.Entries[0].Type);
            AssertEqual("A study on resilient metadata extraction", BibtexTagService.GetTagValueIgnoreCase(result.Entries[0], "title"));
        }

        private static void AssertEqual(int expected, int actual)
        {
            if (expected != actual)
                throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }

        private static void AssertEqual(string expected, string actual)
        {
            if (string.Equals(expected, actual, StringComparison.Ordinal) == false)
                throw new InvalidOperationException($"Expected '{expected}', got '{actual ?? "<null>"}'.");
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
