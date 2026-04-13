namespace ScientificReviews.Forms
{
    public enum DatabaseExportScope
    {
        Visible,
        Selected,
        All
    }

    public enum DatabaseExportFormat
    {
        Bib,
        Csv
    }

    public enum DatabaseExportMode
    {
        Normal,
        AsColumns,
        AsStandard
    }

    public sealed class DatabaseExportOptions
    {
        public DatabaseExportScope Scope { get; set; }
        public DatabaseExportFormat Format { get; set; }
        public DatabaseExportMode Mode { get; set; }
        public string CsvSeparator { get; set; }
        public string OutputFilePath { get; set; }
    }

    public sealed class DatabaseExportProgress
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public string StatusText { get; set; }
    }

    public sealed class DatabaseExportRunResult
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public bool Cancelled { get; set; }
        public string OutputFilePath { get; set; }
        public DatabaseExportFormat Format { get; set; }
    }
}
