namespace ScientificReviews.Forms
{
    public enum PdfExportFileNameMode
    {
        Key,
        KeyTitle,
        Custom
    }

    public sealed class ExportPdfsRunOptions
    {
        public bool ExportSelectedOnly { get; set; }
        public bool InjectDoiMetadata { get; set; }
        public bool PackToFolder { get; set; }
        public string OutputDirectory { get; set; }
        public PdfExportFileNameMode FileNameMode { get; set; }
        public string CustomPattern { get; set; }
    }

    public sealed class ExportPdfsProgress
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Exported { get; set; }
        public int Skipped { get; set; }
        public int Injected { get; set; }
        public string StatusText { get; set; }
    }

    public sealed class ExportPdfsRunResult
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Exported { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public int Injected { get; set; }
        public bool Cancelled { get; set; }
        public string LastErrorMessage { get; set; }
    }
}
