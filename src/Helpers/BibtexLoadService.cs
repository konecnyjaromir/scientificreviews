using ScientificReviews.Bibtex;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ScientificReviews.Helpers
{
    public sealed class BibtexLoadProgress
    {
        public string Summary { get; set; }
        public string Details { get; set; }
        public bool IsIndeterminate { get; set; }
    }

    public sealed class BibtexLoadResult
    {
        public List<BibtexEntry> Entries { get; set; } = new List<BibtexEntry>();
        public string SourcePath { get; set; }
        public bool IsFolderLoad { get; set; }
    }

    public sealed class BibtexLoadService
    {
        public Task<BibtexLoadResult> LoadFileAsync(string fileName, IProgress<BibtexLoadProgress> progress = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name must not be empty.", nameof(fileName));

            return Task.Run(() =>
            {
                progress?.Report(new BibtexLoadProgress
                {
                    Summary = "Reading BibTeX file...",
                    Details = fileName,
                    IsIndeterminate = true
                });

                BibtexParser parser = new BibtexParser();
                var entries = parser.ParseFile(File.ReadAllText(fileName));

                return new BibtexLoadResult
                {
                    Entries = entries == null ? new List<BibtexEntry>() : new List<BibtexEntry>(entries),
                    SourcePath = fileName,
                    IsFolderLoad = false
                };
            });
        }

        public Task<BibtexLoadResult> LoadFolderAsync(string folderPath, IProgress<BibtexLoadProgress> progress = null)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Folder path must not be empty.", nameof(folderPath));

            return Task.Run(() =>
            {
                var list = new List<BibtexEntry>();
                string[] files = Directory.GetFiles(folderPath, "*.bib", SearchOption.AllDirectories);
                BibtexParser parser = new BibtexParser();

                progress?.Report(new BibtexLoadProgress
                {
                    Summary = "Scanning folder...",
                    Details = folderPath,
                    IsIndeterminate = true
                });

                for (int index = 0; index < files.Length; index++)
                {
                    string file = files[index];
                    list.AddRange(parser.ParseFile(File.ReadAllText(file)));
                    progress?.Report(new BibtexLoadProgress
                    {
                        Summary = $"Reading file {index + 1}/{files.Length}",
                        Details = file,
                        IsIndeterminate = true
                    });
                }

                return new BibtexLoadResult
                {
                    Entries = list,
                    SourcePath = folderPath,
                    IsFolderLoad = true
                };
            });
        }
    }
}
