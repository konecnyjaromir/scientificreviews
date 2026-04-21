using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ScientificReviews.Helpers;

namespace ScientificReviews.Bibtex
{
    public class BibtexUtils
    {
        public static string GetFirstAuthorLastName(string authors)
        {
            if (string.IsNullOrWhiteSpace(authors))
                throw new ArgumentException("Input cannot be null or empty.", nameof(authors));

            // Zkontrolujeme, jestli řetězec obsahuje čárku (naznačuje formát s oddělením příjmení a křestního jména)
            if (authors.Contains(","))
            {
                // Formát s čárkami: "Crespo Márquez, Adolfo; de la Fuente Carmona, Antonio"
                var firstAuthor = authors.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (firstAuthor != null)
                {
                    var parts = firstAuthor.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    return parts[0].Trim(); // Vrací příjmení
                }
            }
            else
            {
                // Formát bez čárek: "Adolfo Crespo Márquez and Antonio de la Fuente Carmona"
                var firstAuthor = authors.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (firstAuthor != null)
                {
                    var parts = firstAuthor.Split(' ');
                    return parts.Last().Trim(); // Vrací poslední slovo jako příjmení
                }
            }

            throw new FormatException("Unable to parse the author string. Unsupported format.");
        }       

        public static List<BibtexEntry> RemoveDuplicateEntriesByTag(List<BibtexEntry> entries, string tagName)
        {
            var uniqueEntries = new List<BibtexEntry>();

            var uniqueEntriesDic = new Dictionary<string, BibtexEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                BibtexTagService.RemoveDuplicateTags(entry);

                var tag = entry.Tags.FirstOrDefault(t =>
                    t.Key.Equals(tagName, StringComparison.OrdinalIgnoreCase));

                if (tag == null || string.IsNullOrWhiteSpace(tag.Value))
                {
                    uniqueEntries.Add(entry);
                    continue;
                }

                var key = tag.Value.Trim();

                if (!uniqueEntriesDic.TryGetValue(key, out var existing))
                {
                    uniqueEntriesDic[key] = entry;
                    uniqueEntries.Add(entry);
                }
                else
                {
                    Merge(entry, existing);
                    BibtexTagService.RemoveDuplicateTags(entry);
                    BibtexTagService.RemoveDuplicateTags(existing);
                }
            }

            return uniqueEntries;
        }

        public static List<BibtexEntry> ExcludeEntries(List<BibtexEntry> entries, List<BibtexEntry> toExclude)
        {
            // Kolekce pro uložení výsledků po vyloučení
            var afterExclusion = new List<BibtexEntry>();

            // Sada názvů položek, které chceme vyloučit (pro efektivní vyhledávání)
            var excludedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Přidání názvů položek k vyloučení do HashSetu
            foreach (var exclude in toExclude)
            {
                string title = exclude.GetTagValue("title");
                if (!string.IsNullOrEmpty(title))
                {
                    excludedTitles.Add(title);
                }
            }

            // Iterace přes vstupní položky a přidání těch, které nejsou ve vyloučených
            foreach (var entry in entries)
            {
                string title = entry.GetTagValue("title");
                if (!string.IsNullOrEmpty(title) && !excludedTitles.Contains(title))
                {
                    afterExclusion.Add(entry);
                }
            }

            return afterExclusion;
        }

        private static void Merge(BibtexEntry entry1, BibtexEntry entry2)
        {
            List<BibtexTag> mergedTags = new List<BibtexTag>();
            HashSet<string> seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AppendMissingTags(mergedTags, seenKeys, entry1?.Tags);
            AppendMissingTags(mergedTags, seenKeys, entry2?.Tags);

            entry1.Tags = mergedTags.ToArray();
            entry2.Tags = mergedTags.ToArray();
        }

        private static void AppendMissingTags(List<BibtexTag> destination, HashSet<string> seenKeys, IEnumerable<BibtexTag> source)
        {
            foreach (BibtexTag tag in source ?? Enumerable.Empty<BibtexTag>())
            {
                if (tag == null)
                    continue;

                string key = (tag.Key ?? string.Empty).Trim();
                if (key.Length == 0)
                {
                    destination.Add(tag);
                    continue;
                }

                if (seenKeys.Add(key))
                    destination.Add(tag);
            }
        }

        public static int UpdatePages(List<BibtexEntry> entries)
        {
            int changedEntries = 0;
            foreach (var entry in entries)
            {
                var tag = entry.GetTag("pages");
                if ( tag != null)
                {
                    string originalValue = tag.Value;
                    string[] numbers = Regex.Split(tag.Value, "[^0-9]+");
                    int length = numbers.Length;
                    string ret = string.Empty;
                    for (int i = 0; i < length; i++)
                    {
                        ret += numbers[i];
                        if (i + 1 != length)
                        {
                            ret += "--";
                        }
                    }   
                    if (string.Equals(originalValue, ret, StringComparison.Ordinal) == false)
                    {
                        tag.Value = ret;
                        changedEntries++;
                    }
                }
            }

            return changedEntries;
        }

        public static string RemoveLatex(string value)
        {
            value = value.Replace("\\&", "&");
            value = value.Replace("{", "");
            value = value.Replace("}", "");
            return value;
        }
    }
}
