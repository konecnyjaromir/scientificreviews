using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.JCR.Dto;
using ScientificReviews.Logs;
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
        private sealed class DoiNormalizationResult
        {
            public int ChangedEntries { get; set; }
            public int NormalizedEntries { get; set; }
            public int CopiedFromEprintEntries { get; set; }
            public int EnrichedEprintEntries { get; set; }
            public int InvalidEntries { get; set; }
            public List<string> InvalidRecordKeys { get; } = new List<string>();
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
                List<string> tagsToLeave = frm.GetSelected().ToList();
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

                RefreshGrid();
                Changed();
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
                List<string> typesToLeave = frm.GetSelected().ToList();
                Program.AppSettings.Data.SelectedTypes = typesToLeave.ToArray();
                List<BibtexEntry> list = new List<BibtexEntry>();
                foreach (BibtexEntry entry in entries)
                {
                    if (typesToLeave.Contains(entry.Type))
                        list.Add(entry);
                }

                entries = list;
                LoadData(entries.ToArray());
                Changed();
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

            if (string.IsNullOrWhiteSpace(currentFile) == false)
            {
                string currentDirectory = Path.GetDirectoryName(currentFile);
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

            return saveFileDialog;
        }

        private async Task SaveBibtexToFileAsync(BibtexEntry[] entriesToSave, string fileName, string processName, string successMessage, bool updateCurrentFile)
        {
            ProcessLogScope log = BeginProcessLog(processName, fileName);
            try
            {
                lblStatus.Text = "Saving...";
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

                lblStatus.Text = successMessage;
                log.Complete($"Saved {entriesToSave?.Length ?? 0} record(s) to {fileName}.");
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, $"{processName} failed.");
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
            ProcessLogScope log = BeginProcessLog(processName, $"Records: {entriesToExport.Length}, mode: {options.Mode}");
            IProgress<DatabaseExportProgress> compositeProgress = new Progress<DatabaseExportProgress>(update =>
            {
                progress?.Report(update);
                LogProcessProgress(log, update?.StatusText, null, update?.Completed, update?.Total);
            });

            try
            {
                lblStatus.Text = "Exporting...";
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

                lblStatus.Text = result.Cancelled
                    ? $"Export cancelled after {result.Completed}/{result.Total}."
                    : "Export done.";

                log.Complete(result.Cancelled
                    ? $"Export cancelled after {result.Completed}/{result.Total}."
                    : $"Exported {result.Completed} record(s) to {options.OutputFilePath}.");

                return result;
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, $"{processName} failed.");
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
            List<BibtexEntry> list = new List<BibtexEntry>();
            foreach (BibtexEntry entry in entries)
            {
                if (entry.GetTagValue("doi") != null)
                    list.Add(entry);
            }

            entries = list;
            LoadData(entries.ToArray());
            Changed();
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
            if (ConfirmAutofix() == false)
                return;

            await StartAutofixOperationAsync();
        }

        private async Task StartAutofixOperationAsync()
        {
            await StartPreprocessingPipelineAsync(
                AutoPreprocessingMode.Deep,
                startedAutomatically: false,
                operationKey: "autofix",
                operationName: "Autofix");
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
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                operation.RegisterCancellation(cancellation.Cancel);

                try
                {
                    currentStep++;
                    operation.Report("Normalize DOI", "Preparing DOI values", currentStep, totalSteps, false);
                    cancellation.Token.ThrowIfCancellationRequested();
                    RunNormalizeDoiOperation(entries.ToArray());
                    LogProcessProgress(log, "Normalize DOI completed.");

                    if (mode == AutoPreprocessingMode.Deep)
                    {
                        currentStep++;
                        operation.Report("Fetch missing metadata", "Querying metadata services", currentStep, totalSteps, false);
                        await StartFetchMissingMetadataOperationAsync(false, MetadataScreenMode.All, cancellation.Token);
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

                    if (mode == AutoPreprocessingMode.Deep)
                    {
                        currentStep++;
                        if (string.IsNullOrWhiteSpace(Program.AppSettings.Data.JcrApiKey))
                        {
                            skippedSteps.Add("Update JCR");
                            operation.Report("Update JCR", "Skipped: JCR API key is not set.", currentStep, totalSteps, false);
                            LogProcessProgress(log, "Skipped Update JCR", "JCR API key is not set.");
                        }
                        else
                        {
                            operation.Report("Update JCR", "Fetching missing journals from Clarivate", currentStep, totalSteps, false);
                            await StartUpdateJcrOperationAsync(startedAutomatically, cancellation.Token);
                            LogProcessProgress(log, "Update JCR completed.");
                        }
                    }

                    string details = skippedSteps.Count == 0
                        ? $"All {operationName.ToLowerInvariant()} steps completed."
                        : "Skipped: " + string.Join(", ", skippedSteps) + ".";

                    operation.Complete($"{operationName} finished.", details);
                    log.Complete(details);
                    lblStatus.Text = skippedSteps.Count == 0
                        ? $"{operationName} finished."
                        : $"{operationName} finished. Skipped: {string.Join(", ", skippedSteps)}.";
                }
                catch (OperationCanceledException)
                {
                    operation.Cancel("Cancelled", $"{operationName} was stopped by user.");
                    lblStatus.Text = $"{operationName} cancelled.";
                    log.Complete($"{operationName} cancelled.");
                }
                catch (Exception ex)
                {
                    operation.Fail(ex, "Failed");
                    lblStatus.Text = ex.Message;
                    log.Fail(ex, $"{operationName} failed.");
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
                    return "Normalize DOI -> Fetch metadata -> Remove duplicates by title -> Remove duplicates by DOI -> Normalize page-tag -> Create entry keys -> Auto-pair PDFs -> Update JCR";
                case AutoPreprocessingMode.Fast:
                    return "Normalize DOI -> Normalize page-tag -> Create entry keys -> Auto-pair PDFs";
                default:
                    return "No preprocessing";
            }
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

            if (normalizeDoiFirst)
                RunNormalizeDoiOperation(targetEntries);

            string details = $"Fetching metadata for records: {targetEntries.Length}";

            StatusStripOperationHandle operation = StartTrackedOperation(
                operationKey,
                operationName,
                details);
            if (operation == null)
                return null;

            ProcessLogScope log = BeginProcessLog(operationName, details);
            using (CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken))
            {
                operation.RegisterCancellation(cancellation.Cancel);

                try
                {
                    lblStatus.Text = $"Fetching metadata using {GetConfiguredThreadCount()} thread(s)...";
                    MetadataUpdateResult result = await RunFetchMissingMetadataAsync(targetEntries, operation, screenModeOverride, optionOverrides, cancellation.Token);

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
                    return result;
                }
                catch (OperationCanceledException)
                {
                    operation.Cancel("Cancelled", "Metadata fetch was stopped by user.");
                    lblStatus.Text = "Metadata fetch cancelled.";
                    log.Complete("Metadata fetch cancelled.");
                    return null;
                }
                catch (Exception ex)
                {
                    operation.Fail(ex, "Failed");
                    lblStatus.Text = ex.Message;
                    log.Fail(ex, "Metadata fetch failed.");
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
                "Autofix will automatically modify record metadata and run multiple repair/update steps, including DOI normalization, metadata fetching, duplicate removal by title and DOI, page-tag normalization, entry key generation, PDF auto-pairing, and JCR update when configured.\r\n\r\nThis operation is irreversible and may damage records.\r\n\r\nDo you want to continue?",
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
                int changedEntries = CreateEntryKeys(targetEntries);
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
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "Entry key generation failed.");
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
                DoiNormalizationResult normalization = NormalizeDoisForMetadataFetch(targetEntries);
                if (normalization.ChangedEntries > 0)
                {
                    RefreshGrid();
                    Changed();
                }

                string summary = BuildDoiNormalizationSummary(normalization);
                log.Complete(summary);
                AppLog.Log(summary, AppLog.MessageType.Info);
                lblStatus.Text = summary;
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "DOI normalization failed.");
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
                int changedEntries = BibtexUtils.UpdatePages(targetEntries.ToList());
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
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "Page-tag normalization failed.");
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
                int originalCount = entries.Count;
                entries = BibtexUtils.RemoveDuplicateEntriesByTag(entries, normalizedTagName);
                int removedCount = Math.Max(0, originalCount - entries.Count);

                RefreshGrid();

                string summary = removedCount > 0
                    ? $"Removed {removedCount} duplicate record(s) by {normalizedTagName}."
                    : $"No duplicate records found by {normalizedTagName}.";

                if (removedCount > 0)
                    Changed();

                lblStatus.Text = summary;
                log.Complete(summary);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, $"Duplicate removal by {normalizedTagName} failed.");
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
            List<JournalReportsDto> journalReports = Program.JournalsDatabase.Data.JournalReports;
            Dictionary<string, JournalReportsDto> dic = journalReports.ToDictionary(rep => rep.Journal.Name.ToLower());
            foreach (BibtexEntry entry in entries)
            {
                if (entry.Type == "article")
                {
                    string journalValue = entry.GetTagValue("journal");
                    if (string.IsNullOrWhiteSpace(journalValue))
                        continue;

                    string journalName = BibtexUtils.RemoveLatex(journalValue).ToLower();
                    if (dic.ContainsKey(journalName))
                    {
                        JournalReportsDto report = dic[journalName];
                        double percentile = 0;
                        foreach (var jif in report.Ranks.Jif)
                        {
                            percentile += jif.JifPercentile;
                        }

                        percentile = percentile / report.Ranks.Jif.Count;
                        string quartile = percentile >= 75 ? "Q1" : percentile >= 50 ? "Q2" : percentile >= 25 ? "Q3" : "Q4";
                        string percentileText = Math.Round(percentile, 1).ToString(CultureInfo.InvariantCulture);
                        BibtexTagService.SetSingleTagValue(entry, "jif", percentileText);
                        BibtexTagService.SetSingleTagValue(entry, "jif_" + report.Year.ToString(), percentileText);
                        BibtexTagService.SetSingleTagValue(entry, "jif_Q", quartile);
                    }
                }
            }

            RefreshGrid();
            Changed();
        }

        private void removeDuplicateTagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
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

            RefreshGrid();

            if (removedTags > 0)
            {
                Changed();
                lblStatus.Text = $"Removed {removedTags} duplicate tag(s) in {changedEntries} record(s).";
            }
            else
            {
                lblStatus.Text = "No duplicate tags found.";
            }
        }

        private void removeQ3Q4ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<BibtexEntry> list = new List<BibtexEntry>();
            foreach (BibtexEntry entry in entries)
            {
                string jif = entry.GetTagValue("jif");
                if (jif == null)
                    continue;

                double jifVal = double.Parse(jif, CultureInfo.InvariantCulture);
                if (jifVal >= 50)
                    list.Add(entry);
            }

            entries = list;
            LoadData(entries.ToArray());
            Changed();
        }

        private async void excludeEntriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
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
                    await Task.Run(() =>
                    {
                        BibtexParser parser = new BibtexParser();
                        toExclude.AddRange(parser.ParseFile(File.ReadAllText(fileName)));
                    });
                }

                entries = BibtexUtils.ExcludeEntries(entries, toExclude);
                LoadData(entries.ToArray());
                Changed();
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private void excludeEntriesByTitleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InputBoxForm frm = InputBoxForm.Show("Enter a pattern to filter items:", this);
            if (frm.DialogResult == DialogResult.OK)
            {
                string[] patterns = frm.GetText().Split(',');
                foreach (string item in patterns)
                {
                    string pattern = item.Trim().ToLower();
                    List<BibtexEntry> filtered = new List<BibtexEntry>();
                    foreach (BibtexEntry entry in entries)
                    {
                        if (entry.GetTagValue("title").ToLower().Contains(pattern) == false)
                            filtered.Add(entry);
                    }

                    entries = filtered;
                }

                LoadData(entries.ToArray());
                Changed();
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            LoadData(entries.ToArray(), txtSearch.Text);
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
