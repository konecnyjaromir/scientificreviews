using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScientificReviews.Reports
{
    public sealed class EntryChangeReport
    {
        public int AddedEntries { get; set; }
        public int RemovedEntries { get; set; }
        public int ModifiedEntries { get; set; }
        public List<OperationReportChange> Changes { get; } = new List<OperationReportChange>();
        public int TotalChangedEntries => AddedEntries + RemovedEntries + ModifiedEntries;
        public bool HasChanges => TotalChangedEntries > 0;
    }

    public sealed class EntryChangeSnapshot
    {
        private sealed class EntryState
        {
            public BibtexEntry OriginalEntry { get; set; }
            public BibtexEntry Snapshot { get; set; }
        }

        private readonly List<EntryState> _states;

        internal EntryChangeSnapshot(IEnumerable<BibtexEntry> entries)
        {
            _states = (entries ?? Array.Empty<BibtexEntry>())
                .Where(entry => entry != null)
                .Select(entry => new EntryState
                {
                    OriginalEntry = entry,
                    Snapshot = entry.DeepClone()
                })
                .ToList();
        }

        public EntryChangeReport Build(IEnumerable<BibtexEntry> currentEntries)
        {
            EntryChangeReport report = new EntryChangeReport();
            List<BibtexEntry> currentList = (currentEntries ?? Array.Empty<BibtexEntry>())
                .Where(entry => entry != null)
                .ToList();

            HashSet<BibtexEntry> currentSet = new HashSet<BibtexEntry>(currentList);
            HashSet<BibtexEntry> originalSet = new HashSet<BibtexEntry>(_states.Select(state => state.OriginalEntry));

            foreach (EntryState state in _states)
            {
                if (!currentSet.Contains(state.OriginalEntry))
                {
                    report.RemovedEntries++;
                    report.Changes.Add(new OperationReportChange
                    {
                        Kind = OperationReportChangeKind.Removed,
                        RecordLabel = GetRecordLabel(state.Snapshot),
                        Summary = "Record removed",
                        Details = BuildEntryDetails(state.Snapshot, "-")
                    });
                    continue;
                }

                OperationReportChange change = BuildModifiedChange(state.Snapshot, state.OriginalEntry);
                if (change == null)
                    continue;

                report.ModifiedEntries++;
                report.Changes.Add(change);
            }

            foreach (BibtexEntry entry in currentList)
            {
                if (originalSet.Contains(entry))
                    continue;

                report.AddedEntries++;
                report.Changes.Add(new OperationReportChange
                {
                    Kind = OperationReportChangeKind.Added,
                    RecordLabel = GetRecordLabel(entry),
                    Summary = "Record added",
                    Details = BuildEntryDetails(entry, "+")
                });
            }

            return report;
        }

        private static OperationReportChange BuildModifiedChange(BibtexEntry before, BibtexEntry after)
        {
            List<string> touchedFields = new List<string>();
            List<string> detailLines = new List<string>();

            if (!string.Equals(before?.Key, after?.Key, StringComparison.Ordinal))
            {
                touchedFields.Add("key");
                detailLines.Add($"- key: {FormatValue(before?.Key)}");
                detailLines.Add($"+ key: {FormatValue(after?.Key)}");
            }

            if (!string.Equals(before?.Type, after?.Type, StringComparison.OrdinalIgnoreCase))
            {
                touchedFields.Add("type");
                detailLines.Add($"- type: {FormatValue(before?.Type)}");
                detailLines.Add($"+ type: {FormatValue(after?.Type)}");
            }

            Dictionary<string, string> beforeTags = ToTagDictionary(before);
            Dictionary<string, string> afterTags = ToTagDictionary(after);
            List<string> allKeys = beforeTags.Keys
                .Union(afterTags.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string key in allKeys)
            {
                bool hasBefore = beforeTags.TryGetValue(key, out string beforeValue);
                bool hasAfter = afterTags.TryGetValue(key, out string afterValue);

                if (hasBefore && !hasAfter)
                {
                    touchedFields.Add(key);
                    detailLines.Add($"- {key}: {FormatValue(beforeValue)}");
                    continue;
                }

                if (!hasBefore && hasAfter)
                {
                    touchedFields.Add(key);
                    detailLines.Add($"+ {key}: {FormatValue(afterValue)}");
                    continue;
                }

                if (!string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
                {
                    touchedFields.Add(key);
                    detailLines.Add($"- {key}: {FormatValue(beforeValue)}");
                    detailLines.Add($"+ {key}: {FormatValue(afterValue)}");
                }
            }

            if (detailLines.Count == 0)
                return null;

            string summary = touchedFields.Count == 1
                ? $"Updated {touchedFields[0]}"
                : $"Updated {touchedFields.Count} fields";

            return new OperationReportChange
            {
                Kind = OperationReportChangeKind.Modified,
                RecordLabel = GetRecordLabel(after) ?? GetRecordLabel(before),
                Summary = summary,
                Details = string.Join(Environment.NewLine, detailLines)
            };
        }

        private static Dictionary<string, string> ToTagDictionary(BibtexEntry entry)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (BibtexTag tag in entry?.Tags ?? Array.Empty<BibtexTag>())
            {
                string key = (tag?.Key ?? string.Empty).Trim();
                if (key.Length == 0)
                    continue;

                dictionary[key] = tag.Value;
            }

            return dictionary;
        }

        private static string BuildEntryDetails(BibtexEntry entry, string prefix)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"{prefix} type: {entry?.Type ?? "<null>"}");
            builder.AppendLine($"{prefix} key: {entry?.Key ?? "<null>"}");

            foreach (BibtexTag tag in entry?.Tags ?? Array.Empty<BibtexTag>())
                builder.AppendLine($"{prefix} {tag.Key}: {FormatValue(tag.Value)}");

            return builder.ToString().TrimEnd();
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
        private static string FormatValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "<empty>";

            string normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length > 160
                ? normalized.Substring(0, 157) + "..."
                : normalized;
        }
    }

    public static class EntryChangeTracker
    {
        public static EntryChangeSnapshot Capture(IEnumerable<BibtexEntry> entries)
        {
            return new EntryChangeSnapshot(entries);
        }
    }
}
