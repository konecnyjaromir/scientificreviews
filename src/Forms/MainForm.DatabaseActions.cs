using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.JCR.Dto;
using ScientificReviews.Logs;
using ScientificReviews.Reports;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private bool _isInitializingAutofixModeUi;

        private sealed class DoiNormalizationResult
        {
            public int ChangedEntries { get; set; }
            public int NormalizedEntries { get; set; }
            public int CopiedFromEprintEntries { get; set; }
            public int EnrichedEprintEntries { get; set; }
            public int InvalidEntries { get; set; }
            public List<string> InvalidRecordKeys { get; } = new List<string>();
        }

        private sealed class JcrTagCreationResult
        {
            public int UpdatedEntries { get; set; }
            public int MatchedJournalEntries { get; set; }
            public int MissingJournalTagEntries { get; set; }
            public int MissingJcrReportEntries { get; set; }
            public int InvalidJcrDataEntries { get; set; }
            public int ErrorEntries { get; set; }
            public int MissingJcrTagEntries => MissingJournalTagEntries + MissingJcrReportEntries + InvalidJcrDataEntries + ErrorEntries;
            public List<string> ResolvedRecordDetails { get; } = new List<string>();
            public List<string> UnresolvedRecordDetails { get; } = new List<string>();
            public List<string> MissingJournalRecordDetails { get; } = new List<string>();
            public List<string> ErrorRecordDetails { get; } = new List<string>();
        }

        private sealed class JcrCoverageReport
        {
            public int ResolvedEntries { get; set; }
            public int MissingJournalEntries { get; set; }
            public int UnresolvedEntries { get; set; }
            public int ErrorEntries { get; set; }
            public int MissingJcrTagEntries => MissingJournalEntries + UnresolvedEntries + ErrorEntries;
            public List<string> ResolvedRecordDetails { get; } = new List<string>();
            public List<string> UnresolvedRecordDetails { get; } = new List<string>();
            public List<string> MissingJournalRecordDetails { get; } = new List<string>();
            public List<string> ErrorRecordDetails { get; } = new List<string>();
        }

        private AutoPreprocessingMode AutofixMode
        {
            get => Program.AppSettings?.Data?.AutofixMode ?? AutoPreprocessingMode.Normal;
            set
            {
                if (Program.AppSettings?.Data != null)
                    Program.AppSettings.Data.AutofixMode = value;
            }
        }

        private LowQuantileDeletingMode ConfiguredLowQuantileDeletingMode =>
            Program.AppSettings?.Data?.LowQuantileDeletingMode ?? LowQuantileDeletingMode.OnlyRecordsWithValidJifTags;

        private void InitializeAutofixModeUi()
        {
            UpdateAutofixModeUi();
        }

        private void UpdateAutofixModeUi()
        {
            if (autofixModeOffToolStripMenuItem == null)
                return;

            _isInitializingAutofixModeUi = true;
            try
            {
                autofixModeOffToolStripMenuItem.Checked = AutofixMode == AutoPreprocessingMode.Off;
                autofixModeFastToolStripMenuItem.Checked = AutofixMode == AutoPreprocessingMode.Fast;
                autofixModeNormalToolStripMenuItem.Checked = AutofixMode == AutoPreprocessingMode.Normal;
                autofixModeDeepToolStripMenuItem.Checked = AutofixMode == AutoPreprocessingMode.Deep;
            }
            finally
            {
                _isInitializingAutofixModeUi = false;
            }
        }

        private void createEntryKeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunCreateEntryKeysOperation(entries.ToArray());
        }

        private void removeTagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> tags = new List<string>();
            foreach (BibtexEntry entry in entries)
            {
                foreach (BibtexTag tag in entry.Tags)
                {
                    if (tags.Contains(tag.Key) == false)
                        tags.Add(tag.Key);
                }
            }

            SelectForm frm = new SelectForm();
            frm.SetData(tags.ToArray());
            frm.SetSelection(Program.AppSettings.Data.SelectedTags);
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                ProcessLogScope log = BeginProcessLog("Remove tags", $"Records: {entries.Count}");
                List<string> tagsToLeave = frm.GetSelected().ToList();
                try
                {
                    EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(entries.ToArray());
                    Program.AppSettings.Data.SelectedTags = tagsToLeave.ToArray();
                    foreach (BibtexEntry entry in entries)
                    {
                        List<BibtexTag> list = new List<BibtexTag>();
                        foreach (BibtexTag tag in entry.Tags)
                        {
                            if (tagsToLeave.Contains(tag.Key))
                                list.Add(tag);
                        }

                        entry.Tags = list.ToArray();
                    }

                    EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                    string summary = changeReport != null && changeReport.HasChanges
                        ? $"Remove tags finished. Updated {changeReport.TotalChangedEntries} record(s)."
                        : "Remove tags finished. No tags required removal.";
                    string details = tagsToLeave.Count > 0
                        ? "Kept tags: " + string.Join(", ", tagsToLeave.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
                        : "Kept tags: none";

                    if (changeReport != null && changeReport.HasChanges)
                    {
                        RefreshGrid();
                        Changed();
                    }

                    log.Complete(summary);
                    lblStatus.Text = summary;
                    PublishReport("Remove tags", summary, details, OperationReportSeverity.Info, changeReport);
                }
                catch (Exception ex)
                {
                    lblStatus.Text = ex.Message;
                    log.Fail(ex, "Remove tags failed.");
                    PublishReport("Remove tags", "Remove tags failed.", ex.Message, OperationReportSeverity.Error);
                }
                finally
                {
                    log.Dispose();
                }
            }
        }

        private void removeTypesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> types = new List<string>();
            foreach (BibtexEntry entry in entries)
            {
                if (types.Contains(entry.Type) == false)
                    types.Add(entry.Type);
            }

            SelectForm frm = new SelectForm();
            frm.SetData(types.ToArray());
            frm.SetSelection(Program.AppSettings.Data.SelectedTypes);
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                ProcessLogScope log = BeginProcessLog("Remove types", $"Records: {entries.Count}");
                try
                {
                    EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(entries.ToArray());
                    List<string> typesToLeave = frm.GetSelected().ToList();
                    Program.AppSettings.Data.SelectedTypes = typesToLeave.ToArray();
                    List<BibtexEntry> list = new List<BibtexEntry>();
                    foreach (BibtexEntry entry in entries)
                    {
                        if (typesToLeave.Contains(entry.Type))
                            list.Add(entry);
                    }

                    entries = list;
                    EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                    string summary = changeReport != null && changeReport.HasChanges
                        ? $"Remove types finished. Removed {changeReport.RemovedEntries} record(s)."
                        : "Remove types finished. No record types required removal.";
                    string details = typesToLeave.Count > 0
                        ? "Kept types: " + string.Join(", ", typesToLeave.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
                        : "Kept types: none";

                    LoadData(entries.ToArray());
                    if (changeReport != null && changeReport.HasChanges)
                        Changed();

                    log.Complete(summary);
                    lblStatus.Text = summary;
                    PublishReport("Remove types", summary, details, changeReport != null && changeReport.RemovedEntries > 0 ? OperationReportSeverity.Warning : OperationReportSeverity.Info, changeReport);
                }
                catch (Exception ex)
                {
                    lblStatus.Text = ex.Message;
                    log.Fail(ex, "Remove types failed.");
                    PublishReport("Remove types", "Remove types failed.", ex.Message, OperationReportSeverity.Error);
                }
                finally
                {
                    log.Dispose();
                }
            }
        }

        private async void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await SaveCurrentArchiveAsync();
        }

        private async void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await SaveArchiveAsAsync();
        }

        private async void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ShowExportDialogAsync();
        }

        private async void exportVisibleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ShowExportDialogAsync(DatabaseExportScope.Visible, DatabaseExportFormat.Bib);
        }

        private async Task SaveCurrentArchiveAsync()
        {
            string currentFile = _currentBibTexPath;
            if (string.IsNullOrWhiteSpace(currentFile))
            {
                await SaveArchiveAsAsync();
                return;
            }

            if (Program.AppSettings.Data.SaveWithoutApprove == false)
            {
                DialogResult result = MessageBox.Show(
                    this,
                    $"Save will overwrite the currently opened file:\r\n{currentFile}\r\n\r\nThis operation cannot be undone. Do you want to continue?",
                    "Confirm Save",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1);

                if (result != DialogResult.Yes)
                {
                    lblStatus.Text = "Save cancelled.";
                    return;
                }
            }

            await SaveBibtexToFileAsync(entries.ToArray(), currentFile, "Save BibTeX", "Saved.", true);
        }

        private async Task SaveArchiveAsAsync()
        {
            using (SaveFileDialog saveFileDialog = CreateBibtexSaveFileDialog())
            {
                if (saveFileDialog.ShowDialog(this) != DialogResult.OK)
                {
                    lblStatus.Text = "Save As cancelled.";
                    return;
                }

                await SaveBibtexToFileAsync(entries.ToArray(), saveFileDialog.FileName, "Save BibTeX As", "Saved.", true);
            }
        }

        private SaveFileDialog CreateBibtexSaveFileDialog()
        {
            string initialDirectory = Program.AppSettings.Data.LastDirectory;
            string currentFile = _currentBibTexPath;
            string sessionFileName = GetCurrentBibTexSessionSaveName();

            if (string.IsNullOrWhiteSpace(currentFile) == false)
            {
                string currentDirectory = Path.GetDirectoryName(currentFile);
                if (string.IsNullOrWhiteSpace(currentDirectory) == false)
                    initialDirectory = currentDirectory;
            }
            else if (_currentBibTexSourcePaths.Count > 0)
            {
                string currentDirectory = Path.GetDirectoryName(_currentBibTexSourcePaths[0]);
                if (string.IsNullOrWhiteSpace(currentDirectory) == false)
                    initialDirectory = currentDirectory;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                CheckPathExists = true,
                Filter = "Bibtex database *.bib|*.bib",
                Title = "Save BibTeX As"
            };

            if (string.IsNullOrWhiteSpace(initialDirectory) == false)
                saveFileDialog.InitialDirectory = initialDirectory;

            if (string.IsNullOrWhiteSpace(currentFile) == false)
                saveFileDialog.FileName = Path.GetFileName(currentFile);
            else if (string.IsNullOrWhiteSpace(sessionFileName) == false)
                saveFileDialog.FileName = sessionFileName + ".bib";

            return saveFileDialog;
        }

        private async Task SaveBibtexToFileAsync(BibtexEntry[] entriesToSave, string fileName, string processName, string successMessage, bool updateCurrentFile)
        {
            StatusStripOperationHandle operation = StartTrackedOperation(
                "save-bibtex",
                processName,
                fileName,
                isBlocking: true);
            if (operation == null)
                return;

            ProcessLogScope log = BeginProcessLog(processName, fileName);
            try
            {
                operation.Report("Saving...", fileName, 0, 1, false);
                lblStatus.Text = "Saving... (blocking)";
                await Task.Run(() =>
                {
                    BibtexExporter exporter = new BibtexExporter();
                    string content = exporter.EntriesToString(entriesToSave ?? Array.Empty<BibtexEntry>());
                    File.WriteAllText(fileName, content);
                });

                if (updateCurrentFile)
                {
                    Program.AppSettings.Data.LastDirectory = Path.GetDirectoryName(fileName);
                    SetCurrentBibTex(fileName);
                    SetDatabaseChanged(false);
                    Program.AppSettings.SaveSettings();
                }

                operation.Complete($"Saved {entriesToSave?.Length ?? 0} record(s).", fileName);
                lblStatus.Text = successMessage;
                log.Complete($"Saved {entriesToSave?.Length ?? 0} record(s) to {fileName}.");
                PublishReport(
                    processName,
                    successMessage,
                    $"File: {fileName}{Environment.NewLine}Records: {entriesToSave?.Length ?? 0}",
                    OperationReportSeverity.Info);
            }
            catch (Exception ex)
            {
                operation.Fail(ex, "Failed");
                lblStatus.Text = ex.Message;
                log.Fail(ex, $"{processName} failed.");
                PublishReport(processName, $"{processName} failed.", ex.Message, OperationReportSeverity.Error);
            }
            finally
            {
                log.Dispose();
            }
        }

        private async Task ShowExportDialogAsync(
            DatabaseExportScope? defaultScope = null,
            DatabaseExportFormat? defaultFormat = null,
            DatabaseExportMode? defaultMode = null)
        {
            DatabaseExportOptions initialOptions = BuildInitialExportOptions(defaultScope, defaultFormat, defaultMode);

            using (ExportDatabaseForm form = new ExportDatabaseForm(initialOptions, RunDatabaseExportAsync))
            {
                form.ShowDialog(this);
            }

            await Task.CompletedTask;
        }

        private DatabaseExportOptions BuildInitialExportOptions(
            DatabaseExportScope? defaultScope,
            DatabaseExportFormat? defaultFormat,
            DatabaseExportMode? defaultMode)
        {
            LastExportSettingsData last = Program.AppSettings.Data.LastExportSettings ?? new LastExportSettingsData();

            DatabaseExportOptions options = new DatabaseExportOptions
            {
                Scope = ParseExportScope(last.Scope, DatabaseExportScope.All),
                Format = ParseExportFormat(last.Format, DatabaseExportFormat.Bib),
                Mode = ParseExportMode(last.Mode, DatabaseExportMode.Normal),
                CsvSeparator = NormalizeCsvSeparator(string.IsNullOrWhiteSpace(last.CsvSeparator) ? GetDefaultCsvSeparator() : last.CsvSeparator),
                OutputFilePath = string.IsNullOrWhiteSpace(last.OutputFilePath)
                    ? null
                    : last.OutputFilePath
            };

            if (defaultScope.HasValue)
                options.Scope = defaultScope.Value;
            if (defaultFormat.HasValue)
                options.Format = defaultFormat.Value;
            if (defaultMode.HasValue)
                options.Mode = defaultMode.Value;

            if (string.IsNullOrWhiteSpace(options.CsvSeparator))
                options.CsvSeparator = GetDefaultCsvSeparator();
            if (string.IsNullOrWhiteSpace(options.OutputFilePath))
                options.OutputFilePath = GetDefaultExportOutputPath(options.Format);

            return options;
        }

        private void SaveLastExportSettings(DatabaseExportOptions options)
        {
            if (options == null)
                return;

            Program.AppSettings.Data.LastExportSettings = new LastExportSettingsData
            {
                Scope = options.Scope.ToString(),
                Format = options.Format.ToString(),
                Mode = options.Mode.ToString(),
                CsvSeparator = NormalizeCsvSeparator(options.CsvSeparator),
                OutputFilePath = options.OutputFilePath
            };

            Program.AppSettings.SaveSettings();
        }

        private DatabaseExportScope ParseExportScope(string value, DatabaseExportScope fallback)
        {
            DatabaseExportScope parsed;
            return Enum.TryParse(value, true, out parsed) ? parsed : fallback;
        }

        private DatabaseExportFormat ParseExportFormat(string value, DatabaseExportFormat fallback)
        {
            DatabaseExportFormat parsed;
            return Enum.TryParse(value, true, out parsed) ? parsed : fallback;
        }

        private DatabaseExportMode ParseExportMode(string value, DatabaseExportMode fallback)
        {
            DatabaseExportMode parsed;
            return Enum.TryParse(value, true, out parsed) ? parsed : fallback;
        }

        private async Task<DatabaseExportRunResult> RunDatabaseExportAsync(
            DatabaseExportOptions options,
            IProgress<DatabaseExportProgress> progress,
            CancellationToken cancellationToken)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            BibtexEntry[] entriesToExport = ResolveEntriesForExportScope(options.Scope);
            if (entriesToExport.Length == 0)
                throw new InvalidOperationException(options.Scope == DatabaseExportScope.Selected
                    ? "No records selected for export."
                    : "No records available for export.");

            string[] exportColumns = GetColumnsForExportMode(options.Mode, entriesToExport);
            if (options.Mode == DatabaseExportMode.AsColumns && exportColumns.Length == 0)
                throw new InvalidOperationException("No custom columns configured. Configure them in Settings first.");

            SaveLastExportSettings(options);

            string processName = options.Format == DatabaseExportFormat.Csv ? "Export CSV" : "Export BibTeX";
            StatusStripOperationHandle operation = StartTrackedOperation(
                "export-database",
                processName,
                options.OutputFilePath,
                isBlocking: true);
            if (operation == null)
                throw new InvalidOperationException($"{processName} is already running.");

            ProcessLogScope log = BeginProcessLog(processName, $"Records: {entriesToExport.Length}, mode: {options.Mode}");
            IProgress<DatabaseExportProgress> compositeProgress = new Progress<DatabaseExportProgress>(update =>
            {
                progress?.Report(update);
                operation.Report(
                    update?.StatusText,
                    options.OutputFilePath,
                    update?.Completed,
                    update?.Total,
                    update == null || update.Total <= 0);
                LogProcessProgress(log, update?.StatusText, null, update?.Completed, update?.Total);
            });

            try
            {
                operation.Report("Exporting...", options.OutputFilePath, 0, entriesToExport.Length, false);
                lblStatus.Text = "Exporting... (blocking)";
                DatabaseExportRunResult result = await _databaseExportService.RunExportAsync(
                    entriesToExport,
                    new DatabaseExportOptions
                    {
                        Scope = options.Scope,
                        Format = options.Format,
                        Mode = options.Mode,
                        CsvSeparator = NormalizeCsvSeparator(options.CsvSeparator),
                        OutputFilePath = options.OutputFilePath
                    },
                    exportColumns,
                    compositeProgress,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(options.OutputFilePath) == false)
                    Program.AppSettings.Data.LastDirectory = Path.GetDirectoryName(options.OutputFilePath);

                Program.AppSettings.SaveSettings();

                if (result.Cancelled)
                    operation.Cancel("Cancelled", $"Stopped after {result.Completed}/{result.Total} record(s).");
                else
                    operation.Complete($"Exported {result.Completed} record(s).", options.OutputFilePath);

                lblStatus.Text = result.Cancelled
                    ? $"Export cancelled after {result.Completed}/{result.Total}."
                    : "Export done.";

                log.Complete(result.Cancelled
                    ? $"Export cancelled after {result.Completed}/{result.Total}."
                    : $"Exported {result.Completed} record(s) to {options.OutputFilePath}.");

                PublishReport(
                    processName,
                    result.Cancelled
                        ? $"Export cancelled after {result.Completed}/{result.Total}."
                        : $"Exported {result.Completed} record(s).",
                    $"File: {options.OutputFilePath}{Environment.NewLine}Format: {options.Format}{Environment.NewLine}Scope: {options.Scope}{Environment.NewLine}Mode: {options.Mode}",
                    result.Cancelled ? OperationReportSeverity.Warning : OperationReportSeverity.Info);

                return result;
            }
            catch (Exception ex)
            {
                operation.Fail(ex, "Failed");
                lblStatus.Text = ex.Message;
                log.Fail(ex, $"{processName} failed.");
                PublishReport(processName, $"{processName} failed.", ex.Message, OperationReportSeverity.Error);
                throw;
            }
            finally
            {
                log.Dispose();
            }
        }

        private string GetDefaultExportOutputPath(DatabaseExportFormat format)
        {
            string currentFile = _currentBibTexPath;
            string initialDirectory = Program.AppSettings.Data.LastDirectory;
            string defaultFileName = "export";

            if (string.IsNullOrWhiteSpace(currentFile) == false)
            {
                string currentDirectory = Path.GetDirectoryName(currentFile);
                if (string.IsNullOrWhiteSpace(currentDirectory) == false)
                    initialDirectory = currentDirectory;

                defaultFileName = Path.GetFileNameWithoutExtension(currentFile);
            }

            if (string.IsNullOrWhiteSpace(initialDirectory))
                return defaultFileName + (format == DatabaseExportFormat.Csv ? ".csv" : ".bib");

            return Path.Combine(initialDirectory, defaultFileName + (format == DatabaseExportFormat.Csv ? ".csv" : ".bib"));
        }

        private BibtexEntry[] ResolveEntriesForExportScope(DatabaseExportScope scope)
        {
            switch (scope)
            {
                case DatabaseExportScope.Visible:
                    return visibleEntries.ToArray();
                case DatabaseExportScope.Selected:
                    return GetSelectedOrdered();
                default:
                    return entries.ToArray();
            }
        }

        private string[] GetColumnsForExportMode(DatabaseExportMode mode, IEnumerable<BibtexEntry> sourceEntries)
        {
            switch (mode)
            {
                case DatabaseExportMode.AsColumns:
                    return SanitizeColumnList(Program.AppSettings.Data.Columns);
                case DatabaseExportMode.AsStandard:
                    return GetStandardColumns();
                default:
                    return GetOrderedTagKeys(sourceEntries).ToArray();
            }
        }

        private string[] GetStandardColumns()
        {
            string[] standardColumns = SanitizeColumnList(Program.AppSettings.Data.StandardColumns);
            return standardColumns.Length > 0
                ? standardColumns
                : new[] { "title", "author", "year", "doi" };
        }

        private BibtexEntry[] ProjectEntriesForBibtexExport(IEnumerable<BibtexEntry> sourceEntries, IEnumerable<string> orderedColumns)
        {
            List<BibtexEntry> projected = new List<BibtexEntry>();
            foreach (BibtexEntry entry in sourceEntries ?? Array.Empty<BibtexEntry>())
                projected.Add(CreateProjectedEntry(entry, orderedColumns));

            return projected.ToArray();
        }

        private BibtexEntry CreateProjectedEntry(BibtexEntry entry, IEnumerable<string> orderedColumns)
        {
            List<BibtexTag> tags = new List<BibtexTag>();
            HashSet<string> added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string column in orderedColumns ?? Array.Empty<string>())
            {
                BibtexTag matchingTag = (entry?.Tags ?? Array.Empty<BibtexTag>())
                    .FirstOrDefault(tag => tag != null && string.Equals(tag.Key, column, StringComparison.OrdinalIgnoreCase));

                if (matchingTag == null || !added.Add(matchingTag.Key))
                    continue;

                tags.Add(new BibtexTag(matchingTag.Key, matchingTag.Value));
            }

            return new BibtexEntry
            {
                Key = entry?.Key,
                Type = entry?.Type,
                Tags = tags.ToArray()
            };
        }

        private string GetDefaultCsvSeparator()
        {
            return NormalizeCsvSeparator(Program.AppSettings.Data.DefaultCsvSeparator);
        }

        private string NormalizeCsvSeparator(string separator)
        {
            if (string.IsNullOrWhiteSpace(separator))
                return ",";

            if (string.Equals(separator, "TAB", StringComparison.OrdinalIgnoreCase) || string.Equals(separator, "\\t", StringComparison.OrdinalIgnoreCase))
                return "\t";

            return separator;
        }

        private void exportDOIsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProcessLogScope log = BeginProcessLog("Export DOI list", $"Records: {entries.Count}");
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog()
                {
                    CheckPathExists = true,
                    Filter = "CSV files *.csv|*.csv"
                };
                if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    string fileName = saveFileDialog.FileName;
                    BibtexExporter exporter = new BibtexExporter();
                    string[] content = exporter.GetDois(entries.ToArray());
                    File.WriteAllLines(fileName, content);
                    log.Complete($"Exported {content.Length} DOI(s) to {fileName}.");
                }
                else
                {
                    log.Step("Cancelled by user.");
                    log.Complete("No file selected.");
                }

                lblStatus.Text = "Export done.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "DOI export failed.");
            }
            finally
            {
                log.Dispose();
            }
        }

        private void removeDuplicitiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunRemoveDuplicateEntriesByTagOperation("title");
        }

        private void removeWithoutDOIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProcessLogScope log = BeginProcessLog("Remove without DOI", $"Records: {entries.Count}");
            try
            {
                EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(entries.ToArray());
                List<BibtexEntry> list = new List<BibtexEntry>();
                foreach (BibtexEntry entry in entries)
                {
                    if (entry.GetTagValue("doi") != null)
                        list.Add(entry);
                }

                entries = list;
                EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                string summary = changeReport != null && changeReport.RemovedEntries > 0
                    ? $"Remove without DOI finished. Removed {changeReport.RemovedEntries} record(s)."
                    : "Remove without DOI finished. No records without DOI were found.";

                LoadData(entries.ToArray());
                if (changeReport != null && changeReport.HasChanges)
                    Changed();

                log.Complete(summary);
                lblStatus.Text = summary;
                PublishReport(
                    "Remove without DOI",
                    summary,
                    "Criteria: missing doi tag",
                    changeReport != null && changeReport.RemovedEntries > 0 ? OperationReportSeverity.Warning : OperationReportSeverity.Info,
                    changeReport);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "Remove without DOI failed.");
                PublishReport("Remove without DOI", "Remove without DOI failed.", ex.Message, OperationReportSeverity.Error);
            }
            finally
            {
                log.Dispose();
            }
        }

        private void removeDuplicitiesByDOIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunRemoveDuplicateEntriesByTagOperation("doi");
        }

        private void normalizePageTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunNormalizePageTagOperation(entries.ToArray());
        }

        private async void fetchMissingMetadataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ConfirmMetadataFetch() == false)
                return;

            await StartFetchMissingMetadataOperationAsync(true, null);
        }

        private void normalizeDoiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunNormalizeDoiOperation(entries.ToArray());
        }

        private async void autofixToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AutofixMode == AutoPreprocessingMode.Off)
            {
                lblStatus.Text = "Autofix mode is Off.";
                return;
            }

            if (ConfirmAutofix() == false)
                return;

            await StartAutofixOperationAsync();
        }

        private async Task StartAutofixOperationAsync()
        {
            await StartPreprocessingPipelineAsync(
                AutofixMode,
                startedAutomatically: false,
                operationKey: "autofix",
                operationName: "Autofix");
        }

        private void autofixModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_isInitializingAutofixModeUi)
                return;

            ToolStripMenuItem clickedItem = sender as ToolStripMenuItem;
            AutoPreprocessingMode selectedMode;
            if (clickedItem == null ||
                Enum.TryParse(clickedItem.Tag as string, true, out selectedMode) == false)
                return;

            AutofixMode = selectedMode;
            Program.AppSettings.SaveSettings();
            UpdateAutofixModeUi();

            lblStatus.Text = $"Autofix mode set to {selectedMode}.";
        }

        private async Task StartPreprocessingPipelineAsync(
            AutoPreprocessingMode mode,
            bool startedAutomatically,
            string operationKey,
            string operationName)
        {
            if (mode == AutoPreprocessingMode.Off)
                return;

            if (entries.Count == 0)
            {
                if (!startedAutomatically)
                    lblStatus.Text = $"No records available for {operationName.ToLowerInvariant()}.";
                return;
            }

            StatusStripOperationHandle operation = StartTrackedOperation(
                operationKey,
                operationName,
                BuildPreprocessingSummary(mode),
                startedAutomatically);
            if (operation == null)
                return;

            ProcessLogScope log = BeginProcessLog(operationName, $"Records: {entries.Count}, mode: {mode}");
            List<string> skippedSteps = new List<string>();
            int totalSteps = GetPreprocessingStepCount(mode);
            int currentStep = 0;
            EntryChangeSnapshot overallChangeSnapshot = CaptureEntryChanges(entries.ToArray());
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            using (ReportScopeContext reportScope = BeginReportScope(
                operationName,
                $"{operationName} started.",
                $"Records: {entries.Count}{Environment.NewLine}Mode: {mode}"))
            {
                operation.RegisterCancellation(cancellation.Cancel);

                try
                {
                    bool runFullPipeline = RequiresFullPreprocessingPipeline(mode);
                    MetadataScreenMode? metadataScreenModeOverride = mode == AutoPreprocessingMode.Deep
                        ? MetadataScreenMode.All
                        : (MetadataScreenMode?)null;

                    currentStep++;
                    operation.Report("Normalize DOI", "Preparing DOI values", currentStep, totalSteps, false);
                    cancellation.Token.ThrowIfCancellationRequested();
                    RunNormalizeDoiOperation(entries.ToArray());
                    LogProcessProgress(log, "Normalize DOI completed.");

                    if (runFullPipeline)
                    {
                        currentStep++;
                        operation.Report("Fetch missing metadata", "Querying metadata services", currentStep, totalSteps, false);
                        await StartFetchMissingMetadataOperationAsync(false, metadataScreenModeOverride, cancellation.Token);
                        LogProcessProgress(log, "Fetch missing metadata completed.");

                        currentStep++;
                        operation.Report("Remove duplicates by title", "Removing records with duplicate titles", currentStep, totalSteps, false);
                        cancellation.Token.ThrowIfCancellationRequested();
                        RunRemoveDuplicateEntriesByTagOperation("title");
                        LogProcessProgress(log, "Remove duplicates by title completed.");

                        currentStep++;
                        operation.Report("Remove duplicates by DOI", "Removing records with duplicate DOI values", currentStep, totalSteps, false);
                        cancellation.Token.ThrowIfCancellationRequested();
                        RunRemoveDuplicateEntriesByTagOperation("doi");
                        LogProcessProgress(log, "Remove duplicates by DOI completed.");
                    }

                    currentStep++;
                    operation.Report("Normalize page-tag", "Normalizing pages ranges", currentStep, totalSteps, false);
                    cancellation.Token.ThrowIfCancellationRequested();
                    RunNormalizePageTagOperation(entries.ToArray());
                    LogProcessProgress(log, "Normalize page-tag completed.");

                    currentStep++;
                    operation.Report("Create entry keys", "Generating keys from updated metadata", currentStep, totalSteps, false);
                    cancellation.Token.ThrowIfCancellationRequested();
                    RunCreateEntryKeysOperation(entries.ToArray());
                    LogProcessProgress(log, "Create entry keys completed.");

                    currentStep++;
                    if (string.IsNullOrWhiteSpace(Program.AppSettings.Data.PdfFolder))
                    {
                        skippedSteps.Add("Auto-pair PDFs");
                        operation.Report("Auto-pair PDFs", "Skipped: PDF folder is not set.", currentStep, totalSteps, false);
                        LogProcessProgress(log, "Skipped Auto-pair PDFs", "PDF folder is not set.");
                    }
                    else
                    {
                        operation.Report("Auto-pair PDFs", "Matching records with PDFs", currentStep, totalSteps, false);
                        await StartAutoPairOperationAsync(startedAutomatically, cancellation.Token);
                        LogProcessProgress(log, "Auto-pair PDFs completed.");
                    }

                    if (runFullPipeline)
                    {
                        currentStep++;
                        if (string.IsNullOrWhiteSpace(Program.AppSettings.Data.JcrApiKey))
                        {
                            skippedSteps.Add("Autoupdate JCR");
                            operation.Report("Autoupdate JCR", "Skipped: JCR API key is not set.", currentStep, totalSteps, false);
                            LogProcessProgress(log, "Skipped Autoupdate JCR", "JCR API key is not set.");
                        }
                        else
                        {
                            operation.Report("Autoupdate JCR", "Running as a separate visible subtask.", currentStep, totalSteps, false);
                            await StartAutoupdateJcrOperationAsync(cancellation.Token);
                            LogProcessProgress(log, "Autoupdate JCR completed.");
                        }
                    }

                    string details = skippedSteps.Count == 0
                        ? $"All {operationName.ToLowerInvariant()} steps completed."
                        : "Skipped: " + string.Join(", ", skippedSteps) + ".";
                    EntryChangeReport overallChangeReport = BuildEntryChangeReport(overallChangeSnapshot);

                    operation.Complete($"{operationName} finished.", details);
                    log.Complete(details);
                    lblStatus.Text = skippedSteps.Count == 0
                        ? $"{operationName} finished."
                        : $"{operationName} finished. Skipped: {string.Join(", ", skippedSteps)}.";
                    reportScope.Complete(
                        $"{operationName} finished.",
                        details,
                        skippedSteps.Count == 0 ? OperationReportSeverity.Info : OperationReportSeverity.Warning,
                        overallChangeReport);
                }
                catch (OperationCanceledException)
                {
                    operation.Cancel("Cancelled", $"{operationName} was stopped by user.");
                    lblStatus.Text = $"{operationName} cancelled.";
                    log.Complete($"{operationName} cancelled.");
                    reportScope.Complete($"{operationName} cancelled.", null, OperationReportSeverity.Warning);
                }
                catch (Exception ex)
                {
                    operation.Fail(ex, "Failed");
                    lblStatus.Text = ex.Message;
                    log.Fail(ex, $"{operationName} failed.");
                    reportScope.Complete($"{operationName} failed.", ex.Message, OperationReportSeverity.Error);
                }
                finally
                {
                    log.Dispose();
                }
            }
        }

        private static int GetPreprocessingStepCount(AutoPreprocessingMode mode)
        {
            switch (mode)
            {
                case AutoPreprocessingMode.Deep:
                case AutoPreprocessingMode.Normal:
                    return 8;
                case AutoPreprocessingMode.Fast:
                    return 4;
                default:
                    return 0;
            }
        }

        private static string BuildPreprocessingSummary(AutoPreprocessingMode mode)
        {
            switch (mode)
            {
                case AutoPreprocessingMode.Deep:
                    return "Normalize DOI -> Fetch metadata (forced All) -> Remove duplicates by title -> Remove duplicates by DOI -> Normalize page-tag -> Create entry keys -> Auto-pair PDFs -> Autoupdate JCR";
                case AutoPreprocessingMode.Normal:
                    return "Normalize DOI -> Fetch metadata (settings) -> Remove duplicates by title -> Remove duplicates by DOI -> Normalize page-tag -> Create entry keys -> Auto-pair PDFs -> Autoupdate JCR";
                case AutoPreprocessingMode.Fast:
                    return "Normalize DOI -> Normalize page-tag -> Create entry keys -> Auto-pair PDFs";
                default:
                    return "No preprocessing";
            }
        }

        private static bool RequiresFullPreprocessingPipeline(AutoPreprocessingMode mode)
        {
            return mode == AutoPreprocessingMode.Normal || mode == AutoPreprocessingMode.Deep;
        }

        private async Task StartFetchMissingMetadataOperationAsync(
            bool normalizeDoiFirst,
            MetadataScreenMode? screenModeOverride = null,
            CancellationToken externalCancellationToken = default(CancellationToken))
        {
            await StartFetchMetadataOperationAsync(
                entries.ToArray(),
                normalizeDoiFirst,
                "fetch-metadata",
                "Fetch metadata",
                screenModeOverride,
                null,
                externalCancellationToken);
        }

        private async Task<MetadataUpdateResult> StartFetchMetadataOperationAsync(
            IEnumerable<BibtexEntry> sourceEntries,
            bool normalizeDoiFirst,
            string operationKey,
            string operationName,
            MetadataScreenMode? screenModeOverride = null,
            MetadataUpdateOptions optionOverrides = null,
            CancellationToken externalCancellationToken = default(CancellationToken))
        {
            BibtexEntry[] targetEntries = sourceEntries as BibtexEntry[] ?? sourceEntries?.Where(entry => entry != null).ToArray() ?? Array.Empty<BibtexEntry>();

            if (targetEntries.Length == 0)
            {
                lblStatus.Text = "No records available for metadata fetch.";
                return new MetadataUpdateResult();
            }

            string details = $"Fetching metadata for records: {targetEntries.Length}";

            StatusStripOperationHandle operation = StartTrackedOperation(
                operationKey,
                operationName,
                details);
            if (operation == null)
                return null;

            ProcessLogScope log = BeginProcessLog(operationName, details);
            EntryChangeSnapshot overallChangeSnapshot = CaptureEntryChanges(targetEntries);
            using (CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken))
            using (ReportScopeContext reportScope = BeginReportScope(operationName, $"{operationName} started.", details))
            {
                operation.RegisterCancellation(cancellation.Cancel);

                try
                {
                    if (normalizeDoiFirst)
                        RunNormalizeDoiOperation(targetEntries);

                    lblStatus.Text = $"Fetching metadata using {GetConfiguredThreadCount()} thread(s)...";
                    MetadataUpdateResult result = await RunFetchMissingMetadataAsync(targetEntries, operation, screenModeOverride, optionOverrides, cancellation.Token);
                    EntryChangeReport changeReport = BuildEntryChangeReport(overallChangeSnapshot);

                    RefreshGrid(targetEntries);

                    if (result.UpdatedEntries > 0)
                        Changed();

                    string summary = $"Updated {result.UpdatedEntries}, unresolved {result.UnresolvedEntries}, failed {result.FailedEntries}";
                    string detailMessage = $"Already complete: {result.AlreadyCompleteEntries}.";
                    operation.Complete(summary, detailMessage);
                    log.Complete($"{summary} {detailMessage}");

                    lblStatus.Text = result.UpdatedEntries > 0
                        ? $"Metadata fetch finished. Updated {result.UpdatedEntries} record(s)."
                        : "Metadata fetch finished. No missing metadata could be resolved.";

                    List<string> detailParts = new List<string>
                    {
                        detailMessage
                    };
                    string unresolved = BuildReportList(result.UnresolvedRecordKeys, "Unresolved records");
                    string failed = BuildReportList(result.FailedRecordKeys, "Failed records");
                    if (string.IsNullOrWhiteSpace(unresolved) == false)
                        detailParts.Add(unresolved);
                    if (string.IsNullOrWhiteSpace(failed) == false)
                        detailParts.Add(failed);

                    reportScope.Complete(
                        summary,
                        string.Join(Environment.NewLine + Environment.NewLine, detailParts.Where(part => string.IsNullOrWhiteSpace(part) == false)),
                        result.FailedEntries > 0
                            ? OperationReportSeverity.Error
                            : result.UnresolvedEntries > 0
                                ? OperationReportSeverity.Warning
                                : OperationReportSeverity.Info,
                        changeReport);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    operation.Cancel("Cancelled", "Metadata fetch was stopped by user.");
                    lblStatus.Text = "Metadata fetch cancelled.";
                    log.Complete("Metadata fetch cancelled.");
                    reportScope.Complete("Metadata fetch cancelled.", null, OperationReportSeverity.Warning);
                    return null;
                }
                catch (Exception ex)
                {
                    operation.Fail(ex, "Failed");
                    lblStatus.Text = ex.Message;
                    log.Fail(ex, "Metadata fetch failed.");
                    reportScope.Complete("Metadata fetch failed.", ex.Message, OperationReportSeverity.Error);
                    throw;
                }
                finally
                {
                    log.Dispose();
                }
            }
        }

        private bool ConfirmMetadataFetch()
        {
            DialogResult response = MessageBox.Show(
                this,
                "This process will automatically modify record metadata and download the newest metadata versions from external databases.\r\n\r\nThis operation is irreversible and may damage records.\r\n\r\nBefore fetching metadata, the application will also run DOI normalization.\r\n\r\nDo you want to continue?",
                "Fetch Missing Metadata",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return response == DialogResult.Yes;
        }

        private bool ConfirmAutofix()
        {
            DialogResult response = MessageBox.Show(
                this,
                "Autofix will automatically modify record metadata and run multiple repair/update steps, including DOI normalization, metadata fetching, duplicate removal by title and DOI, page-tag normalization, entry key generation, PDF auto-pairing, and JCR autoupdate when configured.\r\n\r\nThis operation is irreversible and may damage records.\r\n\r\nDo you want to continue?",
                "Autofix",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return response == DialogResult.Yes;
        }

        private void RunCreateEntryKeysOperation(IEnumerable<BibtexEntry> sourceEntries)
        {
            BibtexEntry[] targetEntries = sourceEntries as BibtexEntry[] ?? sourceEntries?.ToArray() ?? Array.Empty<BibtexEntry>();
            if (targetEntries.Length == 0)
            {
                lblStatus.Text = "No records available for entry key generation.";
                return;
            }

            ProcessLogScope log = BeginProcessLog("Create entry keys", $"Records: {targetEntries.Length}");
            try
            {
                EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(targetEntries);
                int changedEntries = CreateEntryKeys(targetEntries);
                EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                if (changedEntries > 0)
                {
                    RefreshGrid();
                    Changed();
                }

                string summary = changedEntries > 0
                    ? $"Entry key generation finished. Updated {changedEntries} record(s)."
                    : "Entry key generation finished. No entry keys required changes.";

                log.Complete(summary);
                AppLog.Log(summary, AppLog.MessageType.Info);
                lblStatus.Text = summary;
                PublishReport("Create entry keys", summary, null, OperationReportSeverity.Info, changeReport);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "Entry key generation failed.");
                PublishReport("Create entry keys", "Entry key generation failed.", ex.Message, OperationReportSeverity.Error);
            }
            finally
            {
                log.Dispose();
            }
        }

        private void RunNormalizeDoiOperation(IEnumerable<BibtexEntry> sourceEntries)
        {
            BibtexEntry[] targetEntries = sourceEntries as BibtexEntry[] ?? sourceEntries?.ToArray() ?? Array.Empty<BibtexEntry>();
            if (targetEntries.Length == 0)
            {
                lblStatus.Text = "No records available for DOI normalization.";
                return;
            }

            ProcessLogScope log = BeginProcessLog("Normalize DOI", $"Records: {targetEntries.Length}");
            try
            {
                EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(targetEntries);
                DoiNormalizationResult normalization = NormalizeDoisForMetadataFetch(targetEntries);
                EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                if (normalization.ChangedEntries > 0)
                {
                    RefreshGrid();
                    Changed();
                }

                string summary = BuildDoiNormalizationSummary(normalization);
                log.Complete(summary);
                AppLog.Log(summary, AppLog.MessageType.Info);
                lblStatus.Text = summary;
                string invalidRecords = BuildReportList(normalization.InvalidRecordKeys, "Records with invalid DOI after normalization");
                PublishReport(
                    "Normalize DOI",
                    summary,
                    invalidRecords,
                    normalization.InvalidEntries > 0 ? OperationReportSeverity.Warning : OperationReportSeverity.Info,
                    changeReport);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "DOI normalization failed.");
                PublishReport("Normalize DOI", "DOI normalization failed.", ex.Message, OperationReportSeverity.Error);
            }
            finally
            {
                log.Dispose();
            }
        }

        private void RunNormalizePageTagOperation(IEnumerable<BibtexEntry> sourceEntries)
        {
            BibtexEntry[] targetEntries = sourceEntries as BibtexEntry[] ?? sourceEntries?.ToArray() ?? Array.Empty<BibtexEntry>();
            if (targetEntries.Length == 0)
            {
                lblStatus.Text = "No records available for page-tag normalization.";
                return;
            }

            ProcessLogScope log = BeginProcessLog("Normalize page-tag", $"Records: {targetEntries.Length}");
            try
            {
                EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(targetEntries);
                int changedEntries = BibtexUtils.UpdatePages(targetEntries.ToList());
                EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                if (changedEntries > 0)
                {
                    RefreshGrid();
                    Changed();
                }

                string summary = changedEntries > 0
                    ? $"Page-tag normalization finished. Updated {changedEntries} record(s)."
                    : "Page-tag normalization finished. No page tags required changes.";

                log.Complete(summary);
                AppLog.Log(summary, AppLog.MessageType.Info);
                lblStatus.Text = summary;
                PublishReport("Normalize page-tag", summary, null, OperationReportSeverity.Info, changeReport);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "Page-tag normalization failed.");
                PublishReport("Normalize page-tag", "Page-tag normalization failed.", ex.Message, OperationReportSeverity.Error);
            }
            finally
            {
                log.Dispose();
            }
        }

        private void RunRemoveDuplicateEntriesByTagOperation(string tagName)
        {
            string normalizedTagName = (tagName ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedTagName))
            {
                lblStatus.Text = "Duplicate removal tag is not set.";
                return;
            }

            ProcessLogScope log = BeginProcessLog($"Remove duplicates by {normalizedTagName}", $"Records: {entries.Count}");
            try
            {
                EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(entries.ToArray());
                int originalCount = entries.Count;
                entries = BibtexUtils.RemoveDuplicateEntriesByTag(entries, normalizedTagName);
                EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                int removedCount = Math.Max(0, originalCount - entries.Count);

                RefreshGrid();

                string summary = removedCount > 0
                    ? $"Removed {removedCount} duplicate record(s) by {normalizedTagName}."
                    : $"No duplicate records found by {normalizedTagName}.";

                if (removedCount > 0)
                    Changed();

                lblStatus.Text = summary;
                log.Complete(summary);
                PublishReport(
                    $"Remove duplicates by {normalizedTagName}",
                    summary,
                    null,
                    removedCount > 0 ? OperationReportSeverity.Warning : OperationReportSeverity.Info,
                    changeReport);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, $"Duplicate removal by {normalizedTagName} failed.");
                PublishReport(
                    $"Remove duplicates by {normalizedTagName}",
                    $"Duplicate removal by {normalizedTagName} failed.",
                    ex.Message,
                    OperationReportSeverity.Error);
            }
            finally
            {
                log.Dispose();
            }
        }

        private int CreateEntryKeys(IEnumerable<BibtexEntry> sourceEntries)
        {
            BibtexEntry[] targetEntries = sourceEntries as BibtexEntry[] ?? sourceEntries?.ToArray() ?? Array.Empty<BibtexEntry>();
            List<string> keys = new List<string>();
            int changedEntries = 0;

            foreach (BibtexEntry entry in targetEntries)
            {
                string authors = entry.GetTagValue("author");
                string year = entry.GetTagValue("year");
                if (authors == null || year == null)
                    continue;

                string key = BibtexUtils.GetFirstAuthorLastName(authors).Replace(" ", "") + year;
                key = key.ToLowerInvariant();
                string myKey = key;
                int i = 1;
                while (keys.Contains(myKey))
                {
                    myKey = key + "_" + i.ToString();
                    i++;
                }

                if (!string.Equals(entry.Key, myKey, StringComparison.Ordinal))
                {
                    entry.Key = myKey;
                    changedEntries++;
                }

                keys.Add(myKey);
            }

            return changedEntries;
        }

        private DoiNormalizationResult NormalizeDoisForMetadataFetch(IEnumerable<BibtexEntry> sourceEntries)
        {
            DoiNormalizationResult result = new DoiNormalizationResult();
            if (sourceEntries == null)
                return result;

            foreach (BibtexEntry entry in sourceEntries)
            {
                if (entry == null)
                    continue;

                bool changed = false;
                string currentDoi = BibtexTagService.GetTagValueIgnoreCase(entry, "doi");
                string normalizedDoi = DoiNormalizationHelper.NormalizeDoiValue(currentDoi);

                if (string.IsNullOrWhiteSpace(normalizedDoi))
                {
                    string eprint = BibtexTagService.GetTagValueIgnoreCase(entry, "eprint");
                    string normalizedFromEprint = DoiNormalizationHelper.BuildArxivDoi(eprint);
                    if (string.IsNullOrWhiteSpace(normalizedFromEprint) == false)
                    {
                        BibtexTagService.SetSingleTagValue(entry, "doi", normalizedFromEprint);
                        result.CopiedFromEprintEntries++;
                        changed = true;
                    }
                }
                else if (string.Equals(currentDoi?.Trim(), normalizedDoi, StringComparison.Ordinal) == false)
                {
                    BibtexTagService.SetSingleTagValue(entry, "doi", normalizedDoi);
                    result.NormalizedEntries++;
                    changed = true;
                }

                string finalDoi = BibtexTagService.GetTagValueIgnoreCase(entry, "doi");
                string currentEprint = BibtexTagService.GetTagValueIgnoreCase(entry, "eprint");
                string normalizedEprint = DoiNormalizationHelper.TryExtractArxivIdentifier(currentEprint);
                if (string.IsNullOrWhiteSpace(normalizedEprint))
                    normalizedEprint = DoiNormalizationHelper.TryExtractArxivIdentifier(finalDoi);

                if (string.IsNullOrWhiteSpace(normalizedEprint) == false &&
                    string.Equals(currentEprint?.Trim(), normalizedEprint, StringComparison.Ordinal) == false)
                {
                    BibtexTagService.SetSingleTagValue(entry, "eprint", normalizedEprint);
                    result.EnrichedEprintEntries++;
                    changed = true;
                }

                if (DoiNormalizationHelper.GetDoiValueKind(finalDoi) == DoiValueKind.Invalid)
                {
                    result.InvalidEntries++;
                    result.InvalidRecordKeys.Add(GetDoiRecordLabel(entry));
                }

                if (changed)
                    result.ChangedEntries++;
            }

            return result;
        }

        private List<string> GetInvalidDoiRecordKeys(IEnumerable<BibtexEntry> sourceEntries)
        {
            List<string> invalidRecordKeys = new List<string>();
            if (sourceEntries == null)
                return invalidRecordKeys;

            foreach (BibtexEntry entry in sourceEntries)
            {
                if (entry == null)
                    continue;

                string doi = BibtexTagService.GetTagValueIgnoreCase(entry, "doi");
                if (DoiNormalizationHelper.GetDoiValueKind(doi) == DoiValueKind.Invalid)
                    invalidRecordKeys.Add(GetDoiRecordLabel(entry));
            }

            return invalidRecordKeys;
        }

        private static string GetDoiRecordLabel(BibtexEntry entry)
        {
            if (entry == null)
                return "<null>";

            string title = BibtexTagService.GetTagValueIgnoreCase(entry, "title");
            if (string.IsNullOrWhiteSpace(title) == false)
                return BibtexUtils.RemoveLatex(title).Trim();

            if (string.IsNullOrWhiteSpace(entry.Key) == false)
                return entry.Key.Trim();

            return "<unnamed record>";
        }

        private static string BuildDoiNormalizationSummary(DoiNormalizationResult normalization)
        {
            string summary = $"DOI normalization updated {normalization.ChangedEntries} record(s).";
            if (normalization.CopiedFromEprintEntries > 0)
                summary += $" Copied eprint to doi in {normalization.CopiedFromEprintEntries} record(s).";
            if (normalization.EnrichedEprintEntries > 0)
                summary += $" Filled or normalized eprint in {normalization.EnrichedEprintEntries} record(s).";
            if (normalization.InvalidEntries > 0)
                summary += $" {normalization.InvalidEntries} record(s) still have invalid DOI.";

            return summary;
        }

        private async Task<MetadataUpdateResult> RunFetchMissingMetadataAsync(
            IEnumerable<BibtexEntry> targetEntries,
            StatusStripOperationHandle operation,
            MetadataScreenMode? screenModeOverride,
            MetadataUpdateOptions optionOverrides,
            CancellationToken cancellationToken)
        {
            BibtexEntry[] targetArray = targetEntries as BibtexEntry[] ?? targetEntries.ToArray();
            ProcessLogScope log = BeginProcessLog("Fetch metadata inner", $"Records: {targetArray.Length}");
            Progress<MetadataUpdateProgress> progress = new Progress<MetadataUpdateProgress>(update =>
            {
                operation.Report(
                    update?.Summary,
                    update?.Details,
                    update?.Completed,
                    update?.Total,
                    false);
                LogProcessProgress(log, update?.Summary, update?.Details, update?.Completed, update?.Total);
            });

            try
            {
                MetadataUpdateOptions options = new MetadataUpdateOptions
                {
                    ContactEmail = Program.AppSettings.Data.MetadataContactEmail,
                    ThreadCount = GetConfiguredThreadCount(),
                    ScreenMode = screenModeOverride ?? Program.AppSettings.Data.MetadataScreenMode
                };

                if (optionOverrides != null)
                {
                    options.AllowUrlLookup = optionOverrides.AllowUrlLookup;
                    options.AllowUrlDoiExtraction = optionOverrides.AllowUrlDoiExtraction;
                }

                MetadataUpdateResult result = await _metadataFetchService.PopulateMissingMetadataAsync(
                    targetArray,
                    options,
                    progress,
                    cancellationToken);
                log.Complete("Metadata inner process completed.");
                return result;
            }
            catch (Exception ex)
            {
                log.Fail(ex, "Metadata inner process failed.");
                throw;
            }
            finally
            {
                log.Dispose();
            }
        }

        private void createExtraJCRTagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (entries.Count == 0)
            {
                lblStatus.Text = "No records available for JCR tag creation.";
                return;
            }

            if (_operationManager.IsActive("create-extra-jcr-tags"))
            {
                lblStatus.Text = "Create extra JCR tags is already running.";
                return;
            }

            ProcessLogScope log = BeginProcessLog("Create extra JCR tags", $"Records: {entries.Count}");
            try
            {
                Dictionary<string, JournalReportsDto> reportsByName = (Program.JournalsDatabase.Data.JournalReports ?? new List<JournalReportsDto>())
                    .Where(report => string.IsNullOrWhiteSpace(NormalizeJournalNameForLookup(report?.Journal?.Name)) == false)
                    .GroupBy(report => NormalizeJournalNameForLookup(report.Journal.Name))
                    .ToDictionary(group => group.Key, group => group.First());

                EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(entries.ToArray());
                JcrTagCreationResult result = CreateExtraJcrTags(entries.ToArray(), reportsByName);
                EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);

                if (result.UpdatedEntries > 0)
                {
                    RefreshGrid();
                    Changed();
                }

                string summary = result.UpdatedEntries > 0
                    ? $"Create extra JCR tags finished. Updated {result.UpdatedEntries} record(s)."
                    : "Create extra JCR tags finished. No records required JCR tag changes.";
                string details = BuildJcrCoverageDetails(
                    result.MatchedJournalEntries,
                    result.MissingJcrTagEntries,
                    result.MissingJournalTagEntries,
                    result.MissingJcrReportEntries + result.InvalidJcrDataEntries,
                    result.ErrorEntries,
                    result.ResolvedRecordDetails,
                    result.UnresolvedRecordDetails,
                    result.MissingJournalRecordDetails,
                    result.ErrorRecordDetails,
                    "These records have journal and JCR tags were created or confirmed.");

                OperationReportSeverity severity =
                    result.InvalidJcrDataEntries > 0 || result.MissingJcrReportEntries > 0 || result.ErrorEntries > 0
                        ? OperationReportSeverity.Warning
                        : OperationReportSeverity.Info;

                log.Complete(summary);
                AppLog.Log(summary, AppLog.MessageType.Info);
                lblStatus.Text = summary;
                PublishReport("Create extra JCR tags", summary, details, severity, changeReport);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "Create extra JCR tags failed.");
                PublishReport("Create extra JCR tags", "Create extra JCR tags failed.", ex.Message, OperationReportSeverity.Error);
            }
            finally
            {
                log.Dispose();
            }
        }

        private void removeDuplicateTagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProcessLogScope log = BeginProcessLog("Remove duplicate tags", $"Records: {entries.Count}");
            try
            {
                EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(entries.ToArray());
                int changedEntries = 0;
                int removedTags = 0;

                foreach (BibtexEntry entry in entries)
                {
                    int removedForEntry = BibtexTagService.RemoveDuplicateTags(entry);
                    if (removedForEntry > 0)
                    {
                        changedEntries++;
                        removedTags += removedForEntry;
                    }
                }

                EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                string summary = removedTags > 0
                    ? $"Removed {removedTags} duplicate tag(s) in {changedEntries} record(s)."
                    : "No duplicate tags found.";
                string details =
                    $"Changed records: {changedEntries}{Environment.NewLine}" +
                    $"Removed duplicate tags: {removedTags}";

                RefreshGrid();

                if (removedTags > 0)
                    Changed();

                log.Complete(summary);
                lblStatus.Text = summary;
                PublishReport(
                    "Remove duplicate tags",
                    summary,
                    details,
                    removedTags > 0 ? OperationReportSeverity.Warning : OperationReportSeverity.Info,
                    changeReport);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "Remove duplicate tags failed.");
                PublishReport("Remove duplicate tags", "Remove duplicate tags failed.", ex.Message, OperationReportSeverity.Error);
            }
            finally
            {
                log.Dispose();
            }
        }

        private void clearFlagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProcessLogScope log = BeginProcessLog("Clear flags", $"Records: {entries.Count}");
            try
            {
                EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(entries.ToArray());
                int changedEntries = 0;

                foreach (BibtexEntry entry in entries)
                {
                    if (entry == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(BibtexTagService.GetTagValueIgnoreCase(entry, "flag")))
                        continue;

                    BibtexTagService.RemoveAllTagsByKey(entry, "flag");
                    changedEntries++;
                }

                EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                string summary = changedEntries > 0
                    ? $"Cleared flags from {changedEntries} record(s)."
                    : "No flags found.";
                string details =
                    $"Changed records: {changedEntries}{Environment.NewLine}" +
                    "Removed tag: flag";

                RefreshGrid();

                if (changedEntries > 0)
                    Changed();

                log.Complete(summary);
                lblStatus.Text = summary;
                PublishReport(
                    "Clear flags",
                    summary,
                    details,
                    changedEntries > 0 ? OperationReportSeverity.Warning : OperationReportSeverity.Info,
                    changeReport);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "Clear flags failed.");
                PublishReport("Clear flags", "Clear flags failed.", ex.Message, OperationReportSeverity.Error);
            }
            finally
            {
                log.Dispose();
            }
        }

        private void removeQ3Q4ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (entries.Count == 0)
            {
                lblStatus.Text = "No records available for Q3/Q4 cleanup.";
                return;
            }

            ProcessLogScope log = BeginProcessLog("Remove Q3 Q4", $"Records: {entries.Count}, mode: {ConfiguredLowQuantileDeletingMode}");
            try
            {
                EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(entries.ToArray());
                int originalCount = entries.Count;
                int removedWithoutValidJifTags = 0;
                List<BibtexEntry> keptEntries = new List<BibtexEntry>();

                foreach (BibtexEntry entry in entries)
                {
                    if (TryGetEntryJcrQuartile(entry, out string quartile) == false)
                    {
                        if (ConfiguredLowQuantileDeletingMode == LowQuantileDeletingMode.AllRecords)
                        {
                            removedWithoutValidJifTags++;
                            continue;
                        }

                        keptEntries.Add(entry);
                        continue;
                    }

                    if (string.Equals(quartile, "Q3", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(quartile, "Q4", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    keptEntries.Add(entry);
                }

                entries = keptEntries;
                EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                int removedEntries = Math.Max(0, originalCount - entries.Count);

                RefreshGrid();

                string summary = removedEntries > 0
                    ? $"Remove Q3 Q4 finished. Removed {removedEntries} record(s)."
                    : "Remove Q3 Q4 finished. No records matched the selected deletion mode.";
                string details =
                    $"Mode: {ConfiguredLowQuantileDeletingMode}{Environment.NewLine}" +
                    $"Removed without valid Jif tags: {removedWithoutValidJifTags}";

                if (removedEntries > 0)
                    Changed();

                lblStatus.Text = summary;
                log.Complete(summary);
                PublishReport(
                    "Remove Q3 Q4",
                    summary,
                    details,
                    removedEntries > 0 ? OperationReportSeverity.Warning : OperationReportSeverity.Info,
                    changeReport);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "Remove Q3 Q4 failed.");
                PublishReport("Remove Q3 Q4", "Remove Q3 Q4 failed.", ex.Message, OperationReportSeverity.Error);
            }
            finally
            {
                log.Dispose();
            }
        }

        private JcrTagCreationResult CreateExtraJcrTags(IEnumerable<BibtexEntry> sourceEntries, IDictionary<string, JournalReportsDto> reportsByName)
        {
            JcrTagCreationResult result = new JcrTagCreationResult();

            foreach (BibtexEntry entry in sourceEntries ?? Array.Empty<BibtexEntry>())
            {
                if (entry == null || string.Equals(entry.Type, "article", StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                try
                {
                    string journalValue = entry.GetTagValue("journal");
                    string recordLabel = GetRecordDisplayLabel(entry);
                    if (string.IsNullOrWhiteSpace(journalValue))
                    {
                        result.MissingJournalTagEntries++;
                        result.MissingJournalRecordDetails.Add(BuildJcrRecordDetail(recordLabel, null, "Record has no journal tag."));
                        continue;
                    }

                    string journalName = BibtexUtils.RemoveLatex(journalValue).Trim();
                    string journalKey = NormalizeJournalNameForLookup(journalName);
                    if (string.IsNullOrWhiteSpace(journalKey) || reportsByName == null || reportsByName.TryGetValue(journalKey, out JournalReportsDto report) == false)
                    {
                        result.MissingJcrReportEntries++;
                        result.UnresolvedRecordDetails.Add(BuildJcrRecordDetail(recordLabel, journalName, "Journal was not found in the local JCR database."));
                        continue;
                    }

                    if (TryGetJcrTagValues(report, out string percentileText, out string quartile) == false)
                    {
                        result.InvalidJcrDataEntries++;
                        result.UnresolvedRecordDetails.Add(BuildJcrRecordDetail(recordLabel, journalName, "Journal was found, but JCR rank data is unusable for tag creation."));
                        continue;
                    }

                    result.MatchedJournalEntries++;
                    bool changed = false;

                    if (string.IsNullOrWhiteSpace(percentileText) == false)
                    {
                        changed |= SetSingleTagValueIfChanged(entry, "jif", percentileText);
                        if (report.Year > 0)
                            changed |= SetSingleTagValueIfChanged(entry, "jif_" + report.Year.ToString(CultureInfo.InvariantCulture), percentileText);
                    }

                    changed |= SetSingleTagValueIfChanged(entry, "jif_Q", quartile);

                    string successDetail = string.IsNullOrWhiteSpace(percentileText)
                        ? $"Quartile: {quartile}"
                        : $"JIF percentile: {percentileText}, quartile: {quartile}";
                    result.ResolvedRecordDetails.Add(BuildJcrRecordDetail(recordLabel, journalName, successDetail));

                    if (changed)
                        result.UpdatedEntries++;
                }
                catch (Exception ex)
                {
                    result.ErrorEntries++;
                    result.ErrorRecordDetails.Add(BuildJcrRecordDetail(GetRecordDisplayLabel(entry), entry?.GetTagValue("journal"), ex.Message));
                }
            }

            return result;
        }

        private JcrCoverageReport BuildJcrCoverageReport(
            IEnumerable<BibtexEntry> sourceEntries,
            IDictionary<string, JournalReportsDto> reportsByName,
            IDictionary<string, string> unresolvedReasonsByJournal)
        {
            JcrCoverageReport result = new JcrCoverageReport();

            foreach (BibtexEntry entry in sourceEntries ?? Array.Empty<BibtexEntry>())
            {
                if (entry == null || string.Equals(entry.Type, "article", StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                try
                {
                    string recordLabel = GetRecordDisplayLabel(entry);
                    string journalValue = entry.GetTagValue("journal");
                    if (string.IsNullOrWhiteSpace(journalValue))
                    {
                        result.MissingJournalEntries++;
                        result.MissingJournalRecordDetails.Add(BuildJcrRecordDetail(recordLabel, null, "Record has no journal tag."));
                        continue;
                    }

                    string journalName = BibtexUtils.RemoveLatex(journalValue).Trim();
                    string journalKey = NormalizeJournalNameForLookup(journalName);
                    if (string.IsNullOrWhiteSpace(journalKey))
                    {
                        result.MissingJournalEntries++;
                        result.MissingJournalRecordDetails.Add(BuildJcrRecordDetail(recordLabel, null, "Record has no usable journal value."));
                        continue;
                    }

                    if (reportsByName != null && reportsByName.TryGetValue(journalKey, out JournalReportsDto report))
                    {
                        if (TryGetJcrTagValues(report, out string percentileText, out string quartile))
                        {
                            result.ResolvedEntries++;
                            string detail = string.IsNullOrWhiteSpace(percentileText)
                                ? $"JCR data available, quartile: {quartile}"
                                : $"JCR data available, JIF percentile: {percentileText}, quartile: {quartile}";
                            result.ResolvedRecordDetails.Add(BuildJcrRecordDetail(recordLabel, journalName, detail));
                        }
                        else
                        {
                            result.UnresolvedEntries++;
                            result.UnresolvedRecordDetails.Add(BuildJcrRecordDetail(recordLabel, journalName, "Journal exists in the local JCR database, but its rank data is unusable for JCR tags."));
                        }

                        continue;
                    }

                    string reason = null;
                    if (unresolvedReasonsByJournal != null)
                        unresolvedReasonsByJournal.TryGetValue(journalKey, out reason);

                    result.UnresolvedEntries++;
                    result.UnresolvedRecordDetails.Add(BuildJcrRecordDetail(
                        recordLabel,
                        journalName,
                        string.IsNullOrWhiteSpace(reason) ? "Journal is still missing in the local JCR database." : reason));
                }
                catch (Exception ex)
                {
                    result.ErrorEntries++;
                    result.ErrorRecordDetails.Add(BuildJcrRecordDetail(GetRecordDisplayLabel(entry), entry?.GetTagValue("journal"), ex.Message));
                }
            }

            return result;
        }

        private static bool SetSingleTagValueIfChanged(BibtexEntry entry, string key, string value)
        {
            string currentValue = BibtexTagService.GetTagValueIgnoreCase(entry, key);
            if (string.Equals((currentValue ?? string.Empty).Trim(), (value ?? string.Empty).Trim(), StringComparison.Ordinal))
                return false;

            BibtexTagService.SetSingleTagValue(entry, key, value);
            return true;
        }

        private static bool TryGetEntryJcrQuartile(BibtexEntry entry, out string quartile)
        {
            quartile = NormalizeQuartile(BibtexTagService.GetTagValueIgnoreCase(entry, "jif_Q"));
            if (string.IsNullOrWhiteSpace(quartile) == false)
                return true;

            if (TryParseJifPercentile(BibtexTagService.GetTagValueIgnoreCase(entry, "jif"), out double percentile))
            {
                quartile = GetQuartileFromPercentile(percentile);
                return true;
            }

            quartile = null;
            return false;
        }

        private static bool TryGetJcrTagValues(JournalReportsDto report, out string percentileText, out string quartile)
        {
            percentileText = null;
            quartile = null;

            if (TryGetJcrPercentile(report, out double percentile))
            {
                percentileText = Math.Round(percentile, 1).ToString(CultureInfo.InvariantCulture);
                quartile = GetQuartileFromPercentile(percentile);
                return true;
            }

            quartile = GetQuartileFromReport(report);
            return string.IsNullOrWhiteSpace(quartile) == false;
        }

        private static bool TryGetJcrPercentile(JournalReportsDto report, out double percentile)
        {
            percentile = 0;
            List<double> percentiles = (report?.Ranks?.Jif ?? new List<JifRankDetailDto>())
                .Select(item => item?.JifPercentile ?? double.NaN)
                .Where(value => double.IsNaN(value) == false && value >= 0 && value <= 100)
                .ToList();

            if (percentiles.Count == 0)
                return false;

            percentile = percentiles.Average();
            return true;
        }

        private static string GetQuartileFromReport(JournalReportsDto report)
        {
            return (report?.Ranks?.Jif ?? new List<JifRankDetailDto>())
                .Select(item => NormalizeQuartile(item?.Quartile))
                .Where(value => string.IsNullOrWhiteSpace(value) == false)
                .OrderBy(value => value, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static bool TryParseJifPercentile(string value, out double percentile)
        {
            percentile = 0;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) == false)
                return false;

            if (parsed < 0 || parsed > 100)
                return false;

            percentile = parsed;
            return true;
        }

        private static string GetQuartileFromPercentile(double percentile)
        {
            return percentile >= 75 ? "Q1" : percentile >= 50 ? "Q2" : percentile >= 25 ? "Q3" : "Q4";
        }

        private static string NormalizeQuartile(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            return normalized == "Q1" || normalized == "Q2" || normalized == "Q3" || normalized == "Q4"
                ? normalized
                : null;
        }

        private static string NormalizeJournalNameForLookup(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            char[] normalized = BibtexUtils.RemoveLatex(value)
                .Trim()
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray();

            return normalized.Length == 0 ? null : new string(normalized);
        }

        private static string BuildJcrCoverageDetails(
            int resolvedEntries,
            int missingJcrTagEntries,
            int missingJournalEntries,
            int unresolvedEntries,
            int errorEntries,
            IEnumerable<string> resolvedRecordDetails,
            IEnumerable<string> unresolvedRecordDetails,
            IEnumerable<string> missingJournalRecordDetails,
            IEnumerable<string> errorRecordDetails,
            string resolvedHeader)
        {
            string details =
                $"Resolved records: {resolvedEntries}{Environment.NewLine}" +
                $"Records still missing JCR tags: {missingJcrTagEntries}{Environment.NewLine}" +
                $"Records without journal: {missingJournalEntries}{Environment.NewLine}" +
                $"Records with journal but unresolved JCR tags: {unresolvedEntries}{Environment.NewLine}" +
                $"Records with other errors: {errorEntries}";

            string resolved = BuildReportList(resolvedRecordDetails, resolvedHeader);
            string unresolved = BuildReportList(unresolvedRecordDetails, "These records have journal but JCR tags could not be created or resolved");
            string missingJournal = BuildReportList(missingJournalRecordDetails, "These records do not have journal, so JCR tags cannot be created");
            string errors = BuildReportList(errorRecordDetails, "These records failed with another error");

            if (string.IsNullOrWhiteSpace(resolved) == false)
                details += Environment.NewLine + Environment.NewLine + resolved;
            if (string.IsNullOrWhiteSpace(unresolved) == false)
                details += Environment.NewLine + Environment.NewLine + unresolved;
            if (string.IsNullOrWhiteSpace(missingJournal) == false)
                details += Environment.NewLine + Environment.NewLine + missingJournal;
            if (string.IsNullOrWhiteSpace(errors) == false)
                details += Environment.NewLine + Environment.NewLine + errors;

            return details;
        }

        private static string BuildJcrRecordDetail(string recordLabel, string journalName, string reasonOrDetails)
        {
            string detail = recordLabel ?? "<unnamed record>";

            if (string.IsNullOrWhiteSpace(journalName) == false)
                detail += $" | journal: {journalName.Trim()}";

            if (string.IsNullOrWhiteSpace(reasonOrDetails) == false)
                detail += $" | {reasonOrDetails.Trim()}";

            return detail;
        }

        private static string GetRecordDisplayLabel(BibtexEntry entry)
        {
            if (entry == null)
                return "<null>";

            string title = entry.GetTagValue("title");
            if (string.IsNullOrWhiteSpace(title) == false)
                return BibtexUtils.RemoveLatex(title).Trim();

            if (string.IsNullOrWhiteSpace(entry.Key) == false)
                return entry.Key.Trim();

            return "<unnamed record>";
        }

        private async void excludeEntriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProcessLogScope log = BeginProcessLog("Exclude entries", $"Records: {entries.Count}");
            try
            {
                OpenFileDialog ofd = new OpenFileDialog()
                {
                    CheckPathExists = true,
                    CheckFileExists = true,
                    Filter = "Bibtex database *.bib|*.bib"
                };

                var toExclude = new List<BibtexEntry>();
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    string fileName = ofd.FileName;
                    EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(entries.ToArray());
                    await Task.Run(() =>
                    {
                        BibtexParser parser = new BibtexParser();
                        toExclude.AddRange(parser.ParseFile(File.ReadAllText(fileName)));
                    });

                    entries = BibtexUtils.ExcludeEntries(entries, toExclude);
                    EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                    string summary = changeReport != null && changeReport.RemovedEntries > 0
                        ? $"Exclude entries finished. Removed {changeReport.RemovedEntries} record(s)."
                        : "Exclude entries finished. No matching records were removed.";
                    string details =
                        $"Source file: {fileName}{Environment.NewLine}" +
                        $"Exclude candidates: {toExclude.Count}";

                    LoadData(entries.ToArray());
                    if (changeReport != null && changeReport.HasChanges)
                        Changed();

                    log.Complete(summary);
                    lblStatus.Text = summary;
                    PublishReport(
                        "Exclude entries",
                        summary,
                        details,
                        changeReport != null && changeReport.RemovedEntries > 0 ? OperationReportSeverity.Warning : OperationReportSeverity.Info,
                        changeReport);
                    return;
                }

                log.Step("Cancelled by user.");
                log.Complete("Exclude entries cancelled.");
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "Exclude entries failed.");
                PublishReport("Exclude entries", "Exclude entries failed.", ex.Message, OperationReportSeverity.Error);
            }
            finally
            {
                log.Dispose();
            }
        }

        private void excludeEntriesByTitleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InputBoxForm frm = InputBoxForm.Show("Enter a pattern to filter items:", this);
            if (frm.DialogResult == DialogResult.OK)
            {
                ProcessLogScope log = BeginProcessLog("Exclude entries by title", $"Records: {entries.Count}");
                try
                {
                    EntryChangeSnapshot changeSnapshot = CaptureEntryChanges(entries.ToArray());
                    string[] patterns = frm.GetText().Split(',');
                    foreach (string item in patterns)
                    {
                        string pattern = item.Trim().ToLower();
                        if (string.IsNullOrWhiteSpace(pattern))
                            continue;

                        List<BibtexEntry> filtered = new List<BibtexEntry>();
                        foreach (BibtexEntry entry in entries)
                        {
                            string title = entry.GetTagValue("title") ?? string.Empty;
                            if (title.ToLower().Contains(pattern) == false)
                                filtered.Add(entry);
                        }

                        entries = filtered;
                    }

                    EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);
                    string summary = changeReport != null && changeReport.RemovedEntries > 0
                        ? $"Exclude entries by title finished. Removed {changeReport.RemovedEntries} record(s)."
                        : "Exclude entries by title finished. No matching records were removed.";
                    string[] normalizedPatterns = (patterns ?? Array.Empty<string>())
                        .Select(item => (item ?? string.Empty).Trim())
                        .Where(item => item.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    string details = normalizedPatterns.Length > 0
                        ? "Patterns: " + string.Join(", ", normalizedPatterns)
                        : "Patterns: none";

                    LoadData(entries.ToArray());
                    if (changeReport != null && changeReport.HasChanges)
                        Changed();

                    log.Complete(summary);
                    lblStatus.Text = summary;
                    PublishReport(
                        "Exclude entries by title",
                        summary,
                        details,
                        changeReport != null && changeReport.RemovedEntries > 0 ? OperationReportSeverity.Warning : OperationReportSeverity.Info,
                        changeReport);
                }
                catch (Exception ex)
                {
                    lblStatus.Text = ex.Message;
                    log.Fail(ex, "Exclude entries by title failed.");
                    PublishReport("Exclude entries by title", "Exclude entries by title failed.", ex.Message, OperationReportSeverity.Error);
                }
                finally
                {
                    log.Dispose();
                }
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            LoadData(entries.ToArray(), txtSearch.Text);
            UpdateSearchValidationStatus();
        }

        private void columnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditColumnsForm frm = new EditColumnsForm();
            frm.SetColumns(Program.AppSettings.Data.Columns);

            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                Program.AppSettings.Data.Columns = frm.GetColumns();
                Program.AppSettings.SaveSettings();
                RefreshGrid(statusMessage: "Custom columns updated.");
            }
        }

        private async void exportAsTableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ShowExportDialogAsync(DatabaseExportScope.All, DatabaseExportFormat.Csv);
        }
    }
}
