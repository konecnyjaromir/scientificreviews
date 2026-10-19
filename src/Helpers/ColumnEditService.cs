using ScientificReviews.Bibtex;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ScientificReviews.Helpers
{
    public enum ColumnEditOperation { Merge, Copy, SetValue, ReplaceText, Remove }

    public sealed class ColumnEditOptions
    {
        public ColumnEditOperation Operation { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public string Value { get; set; } = "";
        public string Find { get; set; } = "";
        public string Separator { get; set; } = "; ";
        public bool CaseSensitive { get; set; } = true;
        public bool RemoveSource { get; set; }
    }

    public sealed class ColumnEditChange
    {
        public BibtexEntry Entry { get; internal set; }
        public BibtexTag[] Tags { get; internal set; }
    }

    public static class ColumnEditService
    {
        public static bool IsEditableColumn(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_\-:]*$")
                && !string.Equals(name, "Key", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "Entry", StringComparison.OrdinalIgnoreCase);
        }

        // Build the complete preview before modifying any live records.
        public static List<ColumnEditChange> Plan(IEnumerable<BibtexEntry> entries, ColumnEditOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (!Enum.IsDefined(typeof(ColumnEditOperation), options.Operation))
                throw new ArgumentException("Choose a column operation.");
            if (!IsEditableColumn(options.Target))
                throw new ArgumentException("Choose a valid target tag (letters/digits and _ - :, starting with a letter). Key and Entry are reserved.");
            bool usesSource = options.Operation == ColumnEditOperation.Merge || options.Operation == ColumnEditOperation.Copy;
            if (usesSource && (!IsEditableColumn(options.Source) || string.Equals(options.Source, options.Target, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Choose different source and target columns.");
            if (options.Operation == ColumnEditOperation.ReplaceText && string.IsNullOrEmpty(options.Find))
                throw new ArgumentException("Enter the text to find.");

            var changes = new List<ColumnEditChange>();
            foreach (BibtexEntry entry in (entries ?? Array.Empty<BibtexEntry>()).Where(e => e != null).Distinct())
            {
                BibtexTag[] original = entry.Tags ?? Array.Empty<BibtexTag>();
                var copy = new BibtexEntry
                {
                    Tags = original.Select(t => t == null ? null : new BibtexTag(t.Key, t.Value)).ToArray()
                };
                BibtexTag[] source = original.Where(t => t != null && string.Equals(t.Key, options.Source, StringComparison.OrdinalIgnoreCase)).ToArray();
                switch (options.Operation)
                {
                    case ColumnEditOperation.Merge:
                        if (source.Length == 0) continue;
                        string[] values = original.Where(t => t != null && string.Equals(t.Key, options.Target, StringComparison.OrdinalIgnoreCase))
                            .Concat(source).Select(t => t.Value).Where(v => !string.IsNullOrWhiteSpace(v))
                            .Distinct(StringComparer.Ordinal).ToArray();
                        if (values.Length > 0)
                            SetValue(copy, options.Target, string.Join(options.Separator ?? "", values));
                        if (options.RemoveSource) BibtexTagService.RemoveAllTagsByKey(copy, options.Source);
                        break;
                    case ColumnEditOperation.Copy:
                        if (source.Length == 0) continue;
                        SetValue(copy, options.Target, source[0].Value ?? "");
                        break;
                    case ColumnEditOperation.SetValue:
                        SetValue(copy, options.Target, options.Value ?? "");
                        break;
                    case ColumnEditOperation.ReplaceText:
                        foreach (BibtexTag tag in copy.Tags.Where(t => t != null && string.Equals(t.Key, options.Target, StringComparison.OrdinalIgnoreCase)))
                            tag.Value = Replace(tag.Value, options.Find, options.Value ?? "", options.CaseSensitive);
                        break;
                    case ColumnEditOperation.Remove:
                        BibtexTagService.RemoveAllTagsByKey(copy, options.Target);
                        break;
                }
                if (!SameTags(original, copy.Tags))
                    changes.Add(new ColumnEditChange { Entry = entry, Tags = copy.Tags });
            }
            return changes;
        }

        private static void SetValue(BibtexEntry entry, string target, string value)
        {
            BibtexTagService.SetSingleTagValue(entry, target, value);
            entry.Tags.First(t => t != null && string.Equals(t.Key, target, StringComparison.OrdinalIgnoreCase)).Key = target;
        }

        private static bool SameTags(BibtexTag[] left, BibtexTag[] right)
        {
            return left.Length == right.Length && left.Zip(right, (a, b) =>
                a == null ? b == null : b != null && a.Key == b.Key && a.Value == b.Value).All(equal => equal);
        }

        private static string Replace(string value, string find, string replacement, bool caseSensitive)
        {
            if (value == null) return null;
            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var result = new StringBuilder();
            int start = 0;
            int index;
            while ((index = value.IndexOf(find, start, comparison)) >= 0)
            {
                result.Append(value, start, index - start).Append(replacement);
                start = index + find.Length;
            }
            return result.Append(value, start, value.Length - start).ToString();
        }
    }
}
