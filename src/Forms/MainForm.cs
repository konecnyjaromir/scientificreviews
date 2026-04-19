using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.Logs;
using ScientificReviews.Reports;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm : Form
    {
        private const int WM_CUT = 0x0300;
        private const int WM_COPY = 0x0301;
        private const int WM_PASTE = 0x0302;
        private int _blockingOperationCount;

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public MainForm()
        {
            InitializeComponent();
            InitializeOpenAddModeUi();
            InitializeAutofixModeUi();
            InitializeSearchUi();
            _operationManager = new StatusStripOperationManager(statusStrip1, toolStripStatusLabel1, this);
            InitializeReportCenter();
            UpdateWindowTitle();
            InitializeRecordContextMenu();
            dataGridView1.CellMouseDoubleClick += dataGridView1_CellMouseDoubleClick;
            dataGridView1.CellMouseDown += dataGridView1_CellMouseDown;
            dataGridView1.MouseDown += dataGridView1_MouseDown;
        }

        private bool IsBlockingUiActive => _blockingOperationCount > 0;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (IsBlockingUiActive)
                return true;

            if (keyData == (Keys.Control | Keys.F))
            {
                txtSearch.Focus();
                txtSearch.SelectAll();
                return true;
            }

            if (HandleClipboardShortcut(keyData))
                return true;

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private IDisposable BeginBlockingUiScope()
        {
            _blockingOperationCount++;
            if (_blockingOperationCount == 1)
                SetMainInteractionEnabled(false);

            return new BlockingUiScope(this);
        }

        private void EndBlockingUiScope()
        {
            if (_blockingOperationCount <= 0)
                return;

            _blockingOperationCount--;
            if (_blockingOperationCount == 0)
                SetMainInteractionEnabled(true);
        }

        private void SetMainInteractionEnabled(bool enabled)
        {
            menuStrip1.Enabled = enabled;
            toolStrip1.Enabled = enabled;
            toolStrip2.Enabled = enabled;
            dataGridView1.Enabled = enabled;
            panel1.Enabled = enabled;
            splitter1.Enabled = enabled;
            splitter2.Enabled = enabled;
            UseWaitCursor = !enabled;
        }

        private sealed class BlockingUiScope : IDisposable
        {
            private MainForm _owner;

            public BlockingUiScope(MainForm owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner == null)
                    return;

                _owner.EndBlockingUiScope();
                _owner = null;
            }
        }

        private bool HandleClipboardShortcut(Keys keyData)
        {
            if (keyData != (Keys.Control | Keys.C) &&
                keyData != (Keys.Control | Keys.X) &&
                keyData != (Keys.Control | Keys.V) &&
                keyData != (Keys.Control | Keys.Shift | Keys.V))
                return false;

            if (TryHandleTextClipboardShortcut(keyData))
                return true;

            if (!ShouldHandleRecordClipboardShortcut())
                return false;

            if (keyData == (Keys.Control | Keys.C))
                CopySelectedRecordsToClipboard();
            else if (keyData == (Keys.Control | Keys.X))
                CutSelectedRecordsToClipboard();
            else if (keyData == (Keys.Control | Keys.Shift | Keys.V))
                PasteRecordsFromClipboard(true);
            else
                PasteRecordsFromClipboard();

            return true;
        }

        private bool TryHandleTextClipboardShortcut(Keys keyData)
        {
            if (!IsTextClipboardContext())
                return false;

            IntPtr focusedHandle = GetFocus();
            if (focusedHandle == IntPtr.Zero)
                return false;

            int message = keyData == (Keys.Control | Keys.C)
                ? WM_COPY
                : keyData == (Keys.Control | Keys.X)
                    ? WM_CUT
                    : keyData == (Keys.Control | Keys.V) || keyData == (Keys.Control | Keys.Shift | Keys.V)
                        ? WM_PASTE
                        : 0;

            if (message == 0)
                return false;

            SendMessage(focusedHandle, message, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        private bool IsTextClipboardContext()
        {
            return (txtSearch?.TextBox?.ContainsFocus ?? false) ||
                   (txtKey?.TextBox?.ContainsFocus ?? false) ||
                   (txtValue?.TextBox?.ContainsFocus ?? false) ||
                   (richTextBox1?.ContainsFocus ?? false) ||
                   (propertyGrid1?.ContainsFocus ?? false) ||
                   (dataGridView1?.IsCurrentCellInEditMode ?? false);
        }

        private bool ShouldHandleRecordClipboardShortcut()
        {
            return dataGridView1 != null &&
                   dataGridView1.ContainsFocus &&
                   !dataGridView1.IsCurrentCellInEditMode;
        }

        List<BibtexEntry> entries = new List<BibtexEntry>();
        List<BibtexEntry> visibleEntries = new List<BibtexEntry>();
        BibtexExporter bibtexExporter = new BibtexExporter();

        private CancellationTokenSource _changedCts;
        private readonly SemaphoreSlim _autosaveLock = new SemaphoreSlim(1, 1);
        private readonly StatusStripOperationManager _operationManager;
        private readonly BibtexLoadService _bibtexLoadService = new BibtexLoadService();
        private readonly MetadataFetchService _metadataFetchService = new MetadataFetchService();
        private readonly JcrUpdateService _jcrUpdateService = new JcrUpdateService();
        private readonly PdfExportService _pdfExportService = new PdfExportService();
        private readonly DatabaseExportService _databaseExportService = new DatabaseExportService();
        private readonly PdfMatchingService _pdfMatchingService = new PdfMatchingService();
        private readonly PasteAnythingService _pasteAnythingService = new PasteAnythingService();
        private readonly OperationReportCenter _reportCenter = new OperationReportCenter();
        private string _currentBibTexPath;
        private readonly List<string> _currentBibTexSourcePaths = new List<string>();
        private bool DatabaseChanged { get; set; }
        private ContextMenuStrip _recordContextMenu;
        private ContextMenuStrip _gridBackgroundContextMenu;
        private ToolStripMenuItem _contextEditMenuItem;
        private ToolStripMenuItem _contextCopyMenuItem;
        private ToolStripMenuItem _contextCutMenuItem;
        private ToolStripMenuItem _contextPasteMenuItem;
        private ToolStripMenuItem _contextDuplicateMenuItem;
        private ToolStripMenuItem _contextRebindPdfMenuItem;
        private ToolStripMenuItem _contextUnbindPdfMenuItem;
        private ToolStripMenuItem _contextRefreshMenuItem;

        private int GetConfiguredThreadCount()
        {
            return Math.Max(1, Program.AppSettings.Data.Threads);
        }

        private PdfMatchingOptions CreatePdfMatchingOptions()
        {
            return new PdfMatchingOptions
            {
                PdfFolder = Program.AppSettings.Data.PdfFolder,
                RecursiveSearch = Program.AppSettings.Data.RecursivePdfSearch,
                AutoPairThresholdPercent = Program.AppSettings.Data.PdfAutoPairThresholdPercent,
                ThreadCount = GetConfiguredThreadCount(),
                SourceMatchMode = Program.AppSettings.Data.PdfSourceMatchMode
            };
        }

        private StatusStripOperationHandle StartTrackedOperation(string key, string name, string details = null, bool silentIfAlreadyRunning = false, Action cancelAction = null)
        {
            StatusStripOperationHandle operation = _operationManager.StartOperation(key, name, details);
            if (operation == null && !silentIfAlreadyRunning)
                lblStatus.Text = $"{name} is already running.";

            operation?.RegisterCancellation(cancelAction);

            return operation;
        }

        private ProcessLogScope BeginProcessLog(string processName, string details = null)
        {
            return ProcessLogger.Begin(processName, details);
        }

        private void LogProcessProgress(ProcessLogScope log, string summary, string details = null, int? completed = null, int? total = null)
        {
            if (log == null)
                return;

            string message = summary ?? string.Empty;
            if (completed.HasValue && total.HasValue)
                message += $" ({completed.Value}/{total.Value})";

            if (string.IsNullOrWhiteSpace(details) == false)
                message += $" | {details}";

            log.Step(message.Trim());
        }

        private void LoadData(BibtexEntry[] entries, string search = "")
        {
            string searchValidationMessage;
            entries = ApplySearchFilter(entries, search, out searchValidationMessage);

            visibleEntries.Clear();
            visibleEntries.AddRange(entries);

            bindingSource1.DataSource = null;
            dataGridView1.DataSource = null;
            DataTable dt = BuildTable(entries, Program.AppSettings.Data.Columns);
            bindingSource1.DataSource = dt;
            dataGridView1.DataSource = bindingSource1;
            dataGridView1.Columns["Entry"].Visible = false;
            lblInfo.Text = $"{entries.Length} entries";

            if (string.IsNullOrWhiteSpace(searchValidationMessage) == false)
                lblStatus.Text = searchValidationMessage;
        }

        private DataTable BuildTable(BibtexEntry[] entries, string[] userColumns)
        {
            DataTable table = new DataTable();
            table.Columns.Add("Key", typeof(string));
            table.Columns.Add("Entry Type", typeof(string));

            if (userColumns == null || userColumns.Length == 0)
                userColumns = GetOrderedTagKeys(entries).ToArray();

            userColumns = SanitizeColumnList(userColumns);

            foreach (string col in userColumns)
                table.Columns.Add(col, typeof(string));

            table.Columns.Add("Entry", typeof(BibtexEntry));

            foreach (BibtexEntry entry in entries)
            {
                DataRow row = table.NewRow();
                foreach (string col in userColumns)
                    row[col] = entry.GetTagValue(col);

                row["Entry"] = entry;
                row["Key"] = entry.Key;
                row["Entry Type"] = entry.Type;
                table.Rows.Add(row);
            }

            return table;
        }

        private string[] SanitizeColumnList(IEnumerable<string> columns)
        {
            List<string> sanitized = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string column in columns ?? Array.Empty<string>())
            {
                string value = (column ?? string.Empty).Trim();
                if (value.Length == 0 || !seen.Add(value))
                    continue;

                sanitized.Add(value);
            }

            return sanitized.ToArray();
        }

        private IEnumerable<string> GetOrderedTagKeys(IEnumerable<BibtexEntry> sourceEntries)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> orderedKeys = new List<string>();

            foreach (BibtexEntry entry in sourceEntries ?? Array.Empty<BibtexEntry>())
            {
                foreach (BibtexTag tag in entry?.Tags ?? Array.Empty<BibtexTag>())
                {
                    string key = (tag?.Key ?? string.Empty).Trim();
                    if (key.Length == 0 || !seen.Add(key))
                        continue;

                    orderedKeys.Add(key);
                }
            }

            return orderedKeys;
        }

        private void SetDatabaseChanged(bool isChanged)
        {
            DatabaseChanged = isChanged;
        }

        private async void Changed(bool markDatabaseChanged = true)
        {
            if (markDatabaseChanged)
                SetDatabaseChanged(true);

            AppSettingsData s = Program.AppSettings.Data;
            if (!s.AllowBackup)
                return;

            int keepBackups = s.NumberOfBackups;
            if (keepBackups <= 0)
                return;

            if (string.IsNullOrWhiteSpace(s.BackupFolder))
            {
                lblStatus.Text = "Backup folder is not set.";
                return;
            }

            _changedCts?.Cancel();
            CancellationTokenSource cts = new CancellationTokenSource();
            _changedCts = cts;

            try
            {
                await Task.Delay(700, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await _autosaveLock.WaitAsync();
            ProcessLogScope log = BeginProcessLog("Autosave backup", s.BackupFolder);
            try
            {
                string content = new BibtexExporter().EntriesToString(entries.ToArray());
                await Task.Run(() => BibtexAutosaveManager.SaveSnapshot(s.BackupFolder, keepBackups, content));
                lblStatus.Text = "Autosave backup created";
                log.Complete($"Snapshot saved. Entries: {entries.Count}, keepBackups: {keepBackups}.");
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Autosave failed: " + ex.Message;
                log.Fail(ex, $"Snapshot failed. Entries: {entries.Count}, keepBackups: {keepBackups}.");
            }
            finally
            {
                log.Dispose();
                _autosaveLock.Release();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (IsBlockingUiActive)
            {
                lblStatus.Text = "A blocking operation is still running.";
                e.Cancel = true;
                return;
            }

            if (DatabaseChanged && !Program.AppSettings.Data.UnsafeClosing)
            {
                DialogResult result = MessageBox.Show(
                    this,
                    "The current database contains unsaved changes.\r\n\r\nDo you really want to close the application without saving?",
                    Program.APP_NAME,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (result != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            Program.AppSettings.SaveSettings();
        }

        private void RefreshGrid(IEnumerable<BibtexEntry> preferredSelection = null, string statusMessage = null)
        {
            BibtexEntry[] selectedEntries = preferredSelection?
                .Where(entry => entry != null)
                .Distinct()
                .ToArray()
                ?? GetSelectedOrdered();

            BibtexEntry currentEntry = null;
            if (selectedEntries.Length == 0 && bindingSource1.Current is DataRowView currentView && currentView.Row != null)
                currentEntry = currentView.Row["Entry"] as BibtexEntry;

            string sortColumnName = dataGridView1?.SortedColumn?.DataPropertyName;
            if (string.IsNullOrWhiteSpace(sortColumnName))
                sortColumnName = dataGridView1?.SortedColumn?.Name;

            ListSortDirection? sortDirection =
                dataGridView1?.SortOrder == SortOrder.Ascending
                    ? ListSortDirection.Ascending
                    : dataGridView1?.SortOrder == SortOrder.Descending
                        ? ListSortDirection.Descending
                        : (ListSortDirection?)null;

            LoadData(entries.ToArray(), txtSearch.Text);
            ReapplyGridSort(sortColumnName, sortDirection);

            if (selectedEntries.Length > 0)
                SelectEntriesInGrid(selectedEntries);
            else if (currentEntry != null)
                SelectEntriesInGrid(new[] { currentEntry });

            if (statusMessage != null)
                lblStatus.Text = statusMessage;
        }

        private void ReapplyGridSort(string sortColumnName, ListSortDirection? sortDirection)
        {
            if (string.IsNullOrWhiteSpace(sortColumnName) || !sortDirection.HasValue || dataGridView1 == null)
                return;

            DataGridViewColumn sortColumn = dataGridView1.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(column =>
                    string.Equals(column.DataPropertyName, sortColumnName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(column.Name, sortColumnName, StringComparison.OrdinalIgnoreCase));

            if (sortColumn == null || sortColumn.SortMode == DataGridViewColumnSortMode.NotSortable)
                return;

            dataGridView1.Sort(sortColumn, sortDirection.Value);
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RefreshGrid(statusMessage: "Grid refreshed.");
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (AboutForm aboutForm = new AboutForm())
            {
                aboutForm.ShowDialog(this);
            }
        }

        private async void updateJournalsDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await StartUpdateJcrOperationAsync(false);
        }

        private async Task StartUpdateJcrOperationAsync(bool startedAutomatically, CancellationToken externalCancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(Program.AppSettings.Data.JcrApiKey))
            {
                if (!startedAutomatically)
                    lblStatus.Text = "JCR Api key is not set.";
                return;
            }

            StatusStripOperationHandle operation = StartTrackedOperation(
                "update-jcr",
                "Update JCR",
                "Fetching missing journals from Clarivate",
                startedAutomatically);
            if (operation == null)
                return;

            ProcessLogScope log = BeginProcessLog("Update JCR", "Fetching missing journals from Clarivate");
            EntryChangeSnapshot changeSnapshot = null;
            using (CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken))
            {
                operation.RegisterCancellation(cancellation.Cancel);

                try
                {
                    lblStatus.Text = "Updating database...";
                    changeSnapshot = CaptureEntryChanges(entries.ToArray());
                    JcrUpdateResult result = await RunUpdateJcrAsync(operation, cancellation.Token);
                    EntryChangeReport changeReport = BuildEntryChangeReport(changeSnapshot);

                    string summary = result.MissingJournalCount == 0
                        ? "No missing journals."
                        : $"Added {result.AddedJournalCount}, missing {result.NotFoundJournalCount}";

                    operation.Complete(summary, "Click to see details.");

                    if (result.MissingJournalCount == 0)
                        lblStatus.Text = "Journal database is already up to date.";
                    else if (result.NotFoundJournalCount > 0)
                        lblStatus.Text = $"Some journals were not found. Added {result.AddedJournalCount}/{result.MissingJournalCount}.";
                    else
                        lblStatus.Text = "Journal database updated.";

                    log.Complete($"{summary}. Missing: {result.MissingJournalCount}, not found: {result.NotFoundJournalCount}.");
                    string notFound = BuildReportList(result.NotFoundJournals, "Journals not found");
                    PublishReport(
                        "Update JCR",
                        summary,
                        $"Missing journals: {result.MissingJournalCount}{Environment.NewLine}Not found: {result.NotFoundJournalCount}" +
                        (string.IsNullOrWhiteSpace(notFound) ? string.Empty : Environment.NewLine + Environment.NewLine + notFound),
                        result.NotFoundJournalCount > 0 ? OperationReportSeverity.Warning : OperationReportSeverity.Info,
                        changeReport);
                }
                catch (OperationCanceledException)
                {
                    operation.Cancel("Cancelled", "JCR update was stopped by user.");
                    lblStatus.Text = "JCR update cancelled.";
                    log.Complete("JCR update cancelled.");
                    PublishReport("Update JCR", "JCR update cancelled.", null, OperationReportSeverity.Warning);
                }
                catch (Exception ex)
                {
                    operation.Fail(ex, "Failed");
                    lblStatus.Text = ex.Message;
                    log.Fail(ex, "JCR update failed.");
                    PublishReport("Update JCR", "JCR update failed.", ex.Message, OperationReportSeverity.Error);
                }
                finally
                {
                    log.Dispose();
                }
            }
        }

        private async Task<JcrUpdateResult> RunUpdateJcrAsync(StatusStripOperationHandle operation, CancellationToken cancellationToken)
        {
            ProcessLogScope log = BeginProcessLog("Update JCR inner", "Progress tracking");
            Progress<JcrUpdateProgress> progress = new Progress<JcrUpdateProgress>(update =>
            {
                operation.Report(update?.Summary, update?.Details, update?.Completed, update?.Total, false);
                LogProcessProgress(log, update?.Summary, update?.Details, update?.Completed, update?.Total);
            });

            try
            {
                JcrUpdateResult result = await _jcrUpdateService.UpdateMissingJournalsAsync(
                    entries,
                    Program.JournalsDatabase.Data.JournalReports,
                    Program.AppSettings.Data.JcrApiKey,
                    DateTime.Now.Year - 1,
                    () => Program.JournalsDatabase.Save(),
                    message => AppLog.Log(message, AppLog.MessageType.Error),
                    progress,
                    cancellationToken);
                log.Complete("JCR inner process completed.");
                return result;
            }
            catch (Exception ex)
            {
                log.Fail(ex, "JCR inner process failed.");
                throw;
            }
            finally
            {
                log.Dispose();
            }
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (new SettingsForm().ShowDialog(this) == DialogResult.OK)
            {
                Program.AppSettings.LoadSettings();
                UpdateOpenAddModeUi();
                UpdateAutofixModeUi();
            }
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            try
            {
                UpdateWindowTitle();

                if (entries != null && entries.Count > 0)
                    return;

                AppSettingsData s = Program.AppSettings.Data;
                if (!s.AllowBackup)
                    return;

                if (string.IsNullOrWhiteSpace(s.BackupFolder))
                {
                    lblStatus.Text = "Backup folder is not set.";
                    return;
                }

                string latestPath = BibtexAutosaveManager.GetLatestBackupPath(s.BackupFolder);
                if (string.IsNullOrWhiteSpace(latestPath) || !File.Exists(latestPath))
                {
                    lblStatus.Text = "No autosave found.";
                    return;
                }

                string content = File.ReadAllText(latestPath);
                if (string.IsNullOrWhiteSpace(content))
                {
                    lblStatus.Text = "Autosave is empty.";
                    return;
                }

                BibtexParser importer = new BibtexParser();
                BibtexEntry[] loadedEntries = importer.ParseFile(content);
                if (loadedEntries == null || loadedEntries.Length == 0)
                {
                    lblStatus.Text = "Autosave contains no entries.";
                    return;
                }

                entries = loadedEntries.ToList();
                visibleEntries = entries;
                LoadData(visibleEntries.ToArray());
                SetDatabaseChanged(true);
                lblStatus.Text = "Loaded latest autosave backup.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to load autosave: " + ex.Message;
            }
        }
    }
}
