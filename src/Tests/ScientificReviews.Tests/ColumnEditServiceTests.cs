using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using System;
using System.Linq;

namespace ScientificReviews.Tests
{
    internal static class ColumnEditServiceTests
    {
        public static void Merge_PreviewsWithoutMutationAndHandlesDuplicatesAndMissingValues()
        {
            var entry = Entry(new BibtexTag("keywords", "Existing"), new BibtexTag("KEYWORDS", "Existing"),
                new BibtexTag("source", "Added"), new BibtexTag("SOURCE", "Added"), new BibtexTag("year", "2026"));
            var empty = Entry(new BibtexTag("source", ""));
            var missing = Entry(new BibtexTag("keywords", "Unchanged"));
            var options = new ColumnEditOptions { Operation = ColumnEditOperation.Merge, Source = "source", Target = "keywords", Separator = " | ", RemoveSource = true };
            var plan = ColumnEditService.Plan(new[] { entry, empty, missing }, options);
            Equal(2, plan.Count);
            Equal(2, entry.Tags.Count(t => t.Key.Equals("source", StringComparison.OrdinalIgnoreCase)));
            Equal(5, entry.Tags.Length); // Preview must leave all live data intact.
            entry.Tags = plan[0].Tags;
            Equal("Existing | Added", entry.GetTagValue("keywords"));
            Equal(2, entry.Tags.Length);
            Equal("2026", entry.GetTagValue("year"));
            Equal(0, plan[1].Tags.Length);
            Equal("Unchanged", missing.GetTagValue("keywords"));

            options.RemoveSource = false;
            var kept = Entry(new BibtexTag("source", "Kept"));
            kept.Tags = ColumnEditService.Plan(new[] { kept }, options).Single().Tags;
            Equal("Kept", kept.GetTagValue("source"));
            Equal("Kept", kept.GetTagValue("keywords"));
        }

        public static void CopySetReplaceAndRemove_RespectScopeAndLiteralCaseMatching()
        {
            var entry = Entry(new BibtexTag("title", "Alpha ALPHA alpha $1"), new BibtexTag("note", ""));
            var missing = Entry();
            var outsideScope = Entry(new BibtexTag("title", "Untouched"));
            var options = new ColumnEditOptions { Operation = ColumnEditOperation.ReplaceText, Target = "title", Find = "Alpha", Value = "$1", CaseSensitive = true };
            entry.Tags = ColumnEditService.Plan(new[] { entry, missing }, options).Single().Tags;
            Equal("$1 ALPHA alpha $1", entry.GetTagValue("title"));
            options.CaseSensitive = false;
            options.Find = "alpha";
            options.Value = "";
            entry.Tags = ColumnEditService.Plan(new[] { entry }, options).Single().Tags;
            Equal("$1   $1", entry.GetTagValue("title"));
            Equal("Untouched", outsideScope.GetTagValue("title"));

            options.Operation = ColumnEditOperation.Copy;
            options.Source = "note";
            entry.Tags = ColumnEditService.Plan(new[] { entry, missing }, options).Single().Tags;
            Equal("", entry.GetTagValue("title"));
            options.Operation = ColumnEditOperation.SetValue;
            options.Value = "";
            Equal(0, ColumnEditService.Plan(new[] { entry }, options).Count);
            missing.Tags = ColumnEditService.Plan(new[] { missing }, options).Single().Tags;
            Equal("", missing.GetTagValue("title"));
            options.Operation = ColumnEditOperation.Remove;
            missing.Tags = ColumnEditService.Plan(new[] { missing }, options).Single().Tags;
            Equal(0, missing.Tags.Length);
        }

        public static void Plan_RejectsInvalidColumnsAndEmptyFindBeforeMutation()
        {
            var entry = Entry(new BibtexTag("title", "Original"));
            var options = new ColumnEditOptions { Operation = ColumnEditOperation.Merge, Source = "TITLE", Target = "title" };
            Reject(entry, options);
            options.Source = "note";
            options.Target = "Key";
            Reject(entry, options);
            options.Target = "Entry Type";
            Reject(entry, options);
            options.Target = "bad name";
            Reject(entry, options);
            options.Target = "title";
            options.Operation = ColumnEditOperation.ReplaceText;
            Reject(entry, options);
            Equal("Original", entry.GetTagValue("title"));
        }

        private static void Reject(BibtexEntry entry, ColumnEditOptions options)
        {
            try { ColumnEditService.Plan(new[] { entry }, options); }
            catch (ArgumentException) { return; }
            throw new InvalidOperationException("Expected invalid options to be rejected.");
        }

        private static BibtexEntry Entry(params BibtexTag[] tags) => new BibtexEntry { Key = "record", Type = "article", Tags = tags };

        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual)) throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }
}
