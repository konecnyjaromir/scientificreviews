using ScientificReviews.Bibtex;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScientificReviews.Helpers
{
    public static class BibtexTagService
    {
        public static void SetSingleTagValue(BibtexEntry entry, string key, string value)
        {
            if (entry == null || string.IsNullOrWhiteSpace(key))
                return;

            List<BibtexTag> tags = (entry.Tags ?? Array.Empty<BibtexTag>()).ToList();
            var updatedTags = new List<BibtexTag>();
            bool updated = false;

            foreach (BibtexTag tag in tags)
            {
                if (tag == null)
                    continue;

                if (string.Equals(tag.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    if (updated)
                        continue;

                    tag.Value = value;
                    updatedTags.Add(tag);
                    updated = true;
                    continue;
                }

                updatedTags.Add(tag);
            }

            if (updated == false)
                updatedTags.Add(new BibtexTag(key, value));

            entry.Tags = updatedTags.ToArray();
        }

        public static string GetTagValueIgnoreCase(BibtexEntry entry, string key)
        {
            if (entry?.Tags == null || string.IsNullOrWhiteSpace(key))
                return null;

            foreach (BibtexTag tag in entry.Tags)
            {
                if (tag != null && string.Equals(tag.Key, key, StringComparison.OrdinalIgnoreCase))
                    return tag.Value;
            }

            return null;
        }

        public static void RemoveAllTagsByKey(BibtexEntry entry, string key)
        {
            if (entry?.Tags == null || string.IsNullOrWhiteSpace(key))
                return;

            entry.Tags = entry.Tags
                .Where(tag => tag != null && string.Equals(tag.Key, key, StringComparison.OrdinalIgnoreCase) == false)
                .ToArray();
        }

        public static int RemoveDuplicateTags(BibtexEntry entry)
        {
            if (entry?.Tags == null || entry.Tags.Length <= 1)
                return 0;

            var uniqueTags = new List<BibtexTag>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = entry.Tags.Length - 1; i >= 0; i--)
            {
                BibtexTag tag = entry.Tags[i];
                if (tag == null)
                    continue;

                if (seenKeys.Add(tag.Key))
                    uniqueTags.Add(tag);
            }

            uniqueTags.Reverse();
            int removed = entry.Tags.Length - uniqueTags.Count;
            entry.Tags = uniqueTags.ToArray();
            return removed;
        }
    }
}
