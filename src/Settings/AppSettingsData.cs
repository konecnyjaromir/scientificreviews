using ScientificReviews.Settings.Editors;
using System.ComponentModel;
using System.Drawing.Design;

namespace ScientificReviews
{
    public enum MetadataScreenMode
    {
        OnlyMissing,
        All,
        OnlyMissingAndArxivDois
    }

    public class LastExportSettingsData
    {
        public string Scope { get; set; } = "All";
        public string Format { get; set; } = "Bib";
        public string Mode { get; set; } = "Normal";
        public string CsvSeparator { get; set; } = ",";
        public string OutputFilePath { get; set; }
    }

    public class AppSettingsData
    {
        private const string APPLICATION_CAT = "0: Application";
        private const string PDF_CAT = "1: PDFs";
        private const string METADATA_CAT = "2: Metadata";
        private const string EXPORT_CAT = "3: Export";
        private const string BACKUP_CAT = "4: Backup";

        [Browsable(true)]
        [Category(APPLICATION_CAT)]
        [DisplayName("Worker threads")]
        [Description("Maximum number of worker threads used by multithreaded operations.")]
        public int Threads { get; set; } = 4;

        [Browsable(true)]
        [Category(APPLICATION_CAT)]
        [DisplayName("Allow unsafe save")]
        [Description("If enabled, Save overwrites the currently opened BibTeX file without confirmation.")]
        public bool SaveWithoutApprove { get; set; } = false;

        [Browsable(true)]
        [Category(APPLICATION_CAT)]
        [DisplayName("Allow unsafe closing")]
        [Description("If enabled, closing the application bypasses the warning about unsaved database changes.")]
        public bool UnsafeClosing { get; set; } = false;

        [Browsable(true)]
        [Category(PDF_CAT)]
        [DisplayName("PDF folder")]
        [Description("Folder that contains source PDF files for pairing and PDF export.")]
        [Editor(typeof(PathEditor), typeof(UITypeEditor))]
        public string PdfFolder { get; set; }

        [Browsable(true)]
        [Category(PDF_CAT)]
        [DisplayName("Recursive PDF search")]
        [Description("If enabled, PDFs are searched in the PDF folder including all subfolders.")]
        public bool RecursivePdfSearch { get; set; } = false;

        [Browsable(true)]
        [Category(PDF_CAT)]
        [DisplayName("PDF auto-pair threshold (%)")]
        [Description("Similarity threshold used by Auto-pair with PDFs.")]
        public int PdfAutoPairThresholdPercent { get; set; } = 95;

        [Browsable(true)]
        [Category(METADATA_CAT)]
        [DisplayName("JCR API key")]
        [Description("API key used by JCR update functions.")]
        public string JcrApiKey { get; set; }

        [Browsable(true)]
        [Category(METADATA_CAT)]
        [DisplayName("Metadata contact email")]
        [Description("Optional contact e-mail used in metadata API User-Agent headers, recommended for the Crossref polite pool.")]
        public string MetadataContactEmail { get; set; }

        [Browsable(true)]
        [Category(METADATA_CAT)]
        [DisplayName("Metadata fetch scope")]
        [Description("Controls which records are processed by metadata fetching.")]
        public MetadataScreenMode MetadataScreenMode { get; set; } = MetadataScreenMode.OnlyMissing;

        [Browsable(true)]
        [Category(EXPORT_CAT)]
        [DisplayName("Custom columns")]
        [Description("Columns shown in the main grid and used by Export mode = As columns.")]
        [Editor(typeof(StringArrayEditor), typeof(UITypeEditor))]
        public string[] Columns { get; set; } = new string[0];

        [Browsable(true)]
        [Category(EXPORT_CAT)]
        [DisplayName("Standard columns")]
        [Description("Columns used by Export mode = As standard.")]
        [Editor(typeof(StringArrayEditor), typeof(UITypeEditor))]
        public string[] StandardColumns { get; set; } = new[] { "title", "author", "year", "doi" };

        [Browsable(true)]
        [Category(EXPORT_CAT)]
        [DisplayName("Default CSV separator")]
        [Description("Default separator used by CSV export. Use ',', ';', 'TAB', or your own custom separator text.")]
        public string DefaultCsvSeparator { get; set; } = ",";

        [Browsable(true)]
        [Category(BACKUP_CAT)]
        [DisplayName("Enable backup")]
        [Description("If enabled, automatic backup snapshots are created after database changes.")]
        public bool AllowBackup { get; set; } = true;

        [Browsable(true)]
        [Category(BACKUP_CAT)]
        [DisplayName("Backup folder")]
        [Description("Folder where automatic backup BibTeX snapshots are stored.")]
        [Editor(typeof(PathEditor), typeof(UITypeEditor))]
        public string BackupFolder { get; set; }

        [Browsable(true)]
        [Category(BACKUP_CAT)]
        [DisplayName("Maximum backup files")]
        [Description("Maximum number of backup snapshot files to keep.")]
        public int NumberOfBackups { get; set; } = 10;

        [Browsable(false)]
        public string[] SelectedTags { get; set; }

        [Browsable(false)]
        public string[] SelectedTypes { get; set; }

        [Browsable(false)]
        public string LastDirectory { get; set; }

        [Browsable(false)]
        public string LastFile { get; set; }

        [Browsable(false)]
        public string LastBibTex { get; set; }

        [Browsable(false)]
        public LastExportSettingsData LastExportSettings { get; set; } = new LastExportSettingsData();
    }
}
