using ScientificReviews.Settings.Editors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private const string GENERAL_CAT = "0: General";
        private const string BACKUP_CAT = "1: Backup";

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("JCR Api key")]
        [Description("Type your JCR Api key to get JCR functions")]
        public string JcrApiKey { get; set; }

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("Pdf Folder")]
        [Description("Set the path, where you have PDFs with [Title].pdf or [Key].pdf")]
        [Editor(typeof(PathEditor), typeof(UITypeEditor))]
        public string PdfFolder { get; set; }

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("Recursive PDF search")]
        [Description("If true, PDFs are searched in Pdf Folder including all subfolders")]
        public bool RecursivePdfSearch { get; set; } = false;

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("PDF auto-pair threshold (%)")]
        [Description("Similarity percentage used by Auto-pair with PDFs")]
        public int PdfAutoPairThresholdPercent { get; set; } = 95;

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("Threads")]
        [Description("Maximum number of threads used by multithreaded operations")]
        public int Threads { get; set; } = 4;

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("Metadata contact email")]
        [Description("Optional email used in metadata API User-Agent headers (recommended for Crossref polite pool)")]
        public string MetadataContactEmail { get; set; }

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("Metadata Screen Mode")]
        [Description("Select which records should be processed by metadata fetching")]
        public MetadataScreenMode MetadataScreenMode { get; set; } = MetadataScreenMode.OnlyMissing;

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("Allow unsafe save")]
        [Description("If true, Save overwrites the current BibTeX file without confirmation")]
        public bool SaveWithoutApprove { get; set; } = false;

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("Allow unsafe closing")]
        [Description("If true, closing the application bypasses the unsaved database changes warning")]
        public bool UnsafeClosing { get; set; } = false;

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("Custom columns")]
        [Description("Columns shown in the main grid and used by Export Mode = As columns.")]
        [Editor(typeof(StringArrayEditor), typeof(UITypeEditor))]
        public string[] Columns { get; set; }

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("Standard columns")]
        [Description("Columns used by Export Mode = As standard.")]
        [Editor(typeof(StringArrayEditor), typeof(UITypeEditor))]
        public string[] StandardColumns { get; set; } = new[] { "title", "author", "year", "doi" };

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("Default CSV separator")]
        [Description("Default separator used by CSV export. Use ',', ';', 'TAB', or your own custom separator text.")]
        public string DefaultCsvSeparator { get; set; } = ",";


        [Browsable(true)]
        [Category(BACKUP_CAT)]
        [DisplayName("Allow backup")]
        [Description("If you want to create backups")]
        [Editor(typeof(PathEditor), typeof(UITypeEditor))]
        public bool AllowBackup { get; set; } = true;
        
        
        [Browsable(true)]
        [Category(BACKUP_CAT)]
        [DisplayName("Number of backups")]
        [Description("Number of backups to keep")]
        public int NumberOfBackups { get; set; } = 10;

        [Browsable(true)]
        [Category(BACKUP_CAT)]
        [DisplayName("Backup folder")]
        [Description("Set the path, where you will have backup bibtexes")]
        [Editor(typeof(PathEditor), typeof(UITypeEditor))]
        public string BackupFolder { get; set; }


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
