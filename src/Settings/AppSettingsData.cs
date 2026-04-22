using ScientificReviews.Pipelines;
using ScientificReviews.Settings.Editors;
using System.Collections.Generic;
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

    public enum AutoPreprocessingMode
    {
        Off = 0,
        Fast = 1,
        Deep = 2,
        Normal = 3
    }

    public enum PdfSourceMatchMode
    {
        TitleOnly,
        KeyOnly,
        KeyOrTitle
    }

    public enum PasteAnythingMode
    {
        Simple,
        Auto,
        Deep
    }

    public enum OpenAddMode
    {
        Normal,
        Raw
    }

    public enum LowQuantileDeletingMode
    {
        OnlyRecordsWithValidJifTags,
        AllRecords
    }

    public enum PerformanceOptimizationMode
    {
        OptimizeForPerformance,
        OptimizeForQualityPerformanceRatio,
        OptimizeForQuality,
        NoOptimization
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
        public const int CURRENT_SETTINGS_VERSION = 7;

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
        [DisplayName("Allow unsafe saving")]
        [Description("If enabled, Save overwrites the currently opened BibTeX file without confirmation.")]
        public bool SaveWithoutApprove { get; set; } = false;

        [Browsable(true)]
        [Category(APPLICATION_CAT)]
        [DisplayName("Allow unsafe closing")]
        [Description("If enabled, closing the application bypasses the warning about unsaved database changes.")]
        public bool UnsafeClosing { get; set; } = false;

        [Browsable(true)]
        [Category(APPLICATION_CAT)]
        [DisplayName("Auto-preprocessing mode")]
        [Description("Selects which automatic preprocessing steps run after opening a BibTeX database: Off = none, Fast = local quick fixes and PDF pairing, Normal = the full preprocessing pipeline using the current settings of individual procedures, Deep = the full preprocessing pipeline with the most exhaustive metadata options.")]
        [TypeConverter(typeof(AutoPreprocessingModeConverter))]
        public AutoPreprocessingMode AutoPreprocessingMode { get; set; } = AutoPreprocessingMode.Fast;

        [Browsable(true)]
        [Category(APPLICATION_CAT)]
        [DisplayName("Autofix mode")]
        [Description("Selects which preprocessing mode is used by Database -> Autofix. Off = disabled, Fast = local quick fixes and PDF pairing, Normal = the full preprocessing pipeline using the current settings of individual procedures, Deep = the full preprocessing pipeline with the most exhaustive metadata options.")]
        [TypeConverter(typeof(AutoPreprocessingModeConverter))]
        public AutoPreprocessingMode AutofixMode { get; set; } = AutoPreprocessingMode.Normal;

        [Browsable(true)]
        [Category(APPLICATION_CAT)]
        [DisplayName("Open/Add Mode")]
        [Description("Controls the default mode used by Project -> Open/Add file/folder. Normal (classic) keeps standard post-load preprocessing, Raw (origin data) uses raw import without post-load preprocessing.")]
        [TypeConverter(typeof(OpenAddModeConverter))]
        public OpenAddMode OpenAddMode { get; set; } = OpenAddMode.Normal;

        [Browsable(true)]
        [Category(APPLICATION_CAT)]
        [DisplayName("Enable Paste Anything")]
        [Description("If enabled, pasting non-BibTeX text into the record grid can create records from DOI, URL, or title-like text.")]
        public bool EnablePasteAnything { get; set; } = true;

        [Browsable(true)]
        [Category(APPLICATION_CAT)]
        [DisplayName("Performance Optimization")]
        [Description("Controls UI performance optimizations used by the application. Optimize For Performance switches large report rendering to async plain-text earlier, Optimize For Quality / Performance ratio keeps the balanced default behavior, Optimize For Quality delays that optimization, and No optimization disables it completely (not recommended).")]
        [TypeConverter(typeof(PerformanceOptimizationModeConverter))]
        public PerformanceOptimizationMode PerformanceOptimizationMode { get; set; } = PerformanceOptimizationMode.OptimizeForQualityPerformanceRatio;

        [Browsable(true)]
        [Category(APPLICATION_CAT)]
        [DisplayName("Paste Anything mode")]
        [Description("Simple = parse only, Auto = parse and fetch metadata safely, Deep = fetch more aggressively including DOI hints from web metadata.")]
        public PasteAnythingMode PasteAnythingMode { get; set; } = PasteAnythingMode.Auto;

        [Browsable(true)]
        [Category(PDF_CAT)]
        [DisplayName("PDF source folder")]
        [Description("Folder that contains source PDF files for pairing and PDF export.")]
        [Editor(typeof(PathEditor), typeof(UITypeEditor))]
        public string PdfFolder { get; set; }

        [Browsable(true)]
        [Category(PDF_CAT)]
        [DisplayName("Recursive PDF search")]
        [Description("If enabled, PDFs are searched in the PDF folder including all subfolders.")]
        public bool RecursivePdfSearch { get; set; } = true;

        [Browsable(true)]
        [Category(PDF_CAT)]
        [DisplayName("Autoopening PDF when attach")]
        [Description("If enabled, manually attaching or rebinding a PDF automatically opens the selected PDF file afterwards.")]
        public bool AutoOpenPdfWhenAttach { get; set; } = true;

        [Browsable(true)]
        [Category(PDF_CAT)]
        [DisplayName("PDF auto-pair threshold (%)")]
        [Description("Similarity threshold used by Auto-pair with PDFs.")]
        public int PdfAutoPairThresholdPercent { get; set; } = 95;

        [Browsable(true)]
        [Category(PDF_CAT)]
        [DisplayName("PDF source match mode")]
        [Description("Controls whether PDF pairing matches filenames by title only, key only, or key/title together.")]
        public PdfSourceMatchMode PdfSourceMatchMode { get; set; } = PdfSourceMatchMode.TitleOnly;

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
        public MetadataScreenMode MetadataScreenMode { get; set; } = MetadataScreenMode.All;

        [Browsable(true)]
        [Category(METADATA_CAT)]
        [DisplayName("Low Quantile (Q3,Q4) Deleting Mode")]
        [Description("Controls how Journal Citation Reports -> Remove Q3 Q4 treats records without a valid JCR quantile tag. Only Records With Valid Jif Tags removes only records confirmed as Q3/Q4. All records also removes records without valid JCR tags.")]
        [TypeConverter(typeof(LowQuantileDeletingModeConverter))]
        public LowQuantileDeletingMode LowQuantileDeletingMode { get; set; } = LowQuantileDeletingMode.OnlyRecordsWithValidJifTags;

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

        [Browsable(false)]
        public bool UseSmartSearch { get; set; } = true;

        [Browsable(false)]
        public List<CustomPipelineDefinition> CustomPipelines { get; set; } = new List<CustomPipelineDefinition>();

        [Browsable(false)]
        public int SettingsVersion { get; set; } = CURRENT_SETTINGS_VERSION;
    }
}
