using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var uniqueEntriesDic = new Dictionary<string,BibtexEntry>();
            foreach (var entry in entries)
            {
                var titleTag = entry.Tags.FirstOrDefault(tag => tag.Key.Equals(tagName, StringComparison.OrdinalIgnoreCase));
                if (titleTag != null && !seenTitles.Contains(titleTag.Value))
                {
                    seenTitles.Add(titleTag.Value);
                    uniqueEntries.Add(entry);
                    uniqueEntriesDic.Add(titleTag.Value.ToLower(), entry);
                }
                else
                {
                    if (titleTag != null)
                        Merge(entry, uniqueEntriesDic[titleTag.Value.ToLower()]);
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
            List<BibtexTag> mergedTags = entry1.Tags.ToList();
            Dictionary<string, BibtexTag> tagsDictionary = mergedTags.ToDictionary(tag => tag.Key);

            foreach (var tag in entry2.Tags)
            {
                if (!tagsDictionary.ContainsKey(tag.Key))
                {
                    mergedTags.Add(tag);
                }
            }

            entry1.Tags = mergedTags.ToArray();
            entry2.Tags = mergedTags.ToArray();

        }

        public static void UpdatePages(List<BibtexEntry> entries)
        {
            foreach (var entry in entries)
            {
                var tag = entry.GetTag("pages");
                if ( tag != null)
                {
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
                    tag.Value = ret;
                }
            }                       
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
