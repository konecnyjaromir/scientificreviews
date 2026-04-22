using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.JCR.Dto;
using ScientificReviews.Logs;
using ScientificReviews.Reports;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
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
        private const int RecordPanelToggleWidth = 18;
        private const int MinimumExpandedGridWidth = 360;
        private const int MinimumExpandedRecordPanelWidth = 280;

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string left, string right);

        public MainForm()
        {
            InitializeComponent();
            InitializeOpenAddModeUi();
            InitializeAutofixModeUi();
            InitializeSearchUi();
            InitializePipelinesUi();
            _operationManager = new StatusStripOperationManager(statusStrip1, toolStripStatusLabel1, this);
            _operationManager.BlockingOperationsChanged += OperationManager_BlockingOperationsChanged;
            InitializeReportCenter();
            UpdateWindowTitle();
            InitializeRecordContextMenu();
            InitializeRecordPanelToggleUi();
            dataGridView1.CellMouseDoubleClick += dataGridView1_CellMouseDoubleClick;
            dataGridView1.CellMouseDown += dataGridView1_CellMouseDown;
            dataGridView1.ColumnHeaderMouseClick += dataGridView1_ColumnHeaderMouseClick;
            dataGridView1.DataBindingComplete += dataGridView1_DataBindingComplete;
            dataGridView1.MouseDown += dataGridView1_MouseDown;
            splitter1.SplitterMoved += splitter1_SplitterMoved;
        }

        private bool IsBlockingUiActive => _operationManager?.HasBlockingOperations ?? false;

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

        private void SetMainInteractionEnabled(bool enabled)
        {
            menuStrip1.Enabled = enabled;
            toolStrip1.Enabled = enabled;
            toolStrip2.Enabled = enabled;
            dataGridView1.Enabled = enabled;
            panelRecordPanelToggle.Enabled = enabled;
            panel1.Enabled = enabled;
            splitter1.Enabled = enabled;
            splitter2.Enabled = enabled;
            UseWaitCursor = !enabled;
        }

        private void OperationManager_BlockingOperationsChanged(object sender, StatusStripBlockingOperationsChangedEventArgs e)
        {
            SetMainInteractionEnabled(e == null || !e.HasBlockingOperations);
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
        private string _currentSortColumnName;
        private ListSortDirection? _currentSortDirection;
        private bool _isRecordPanelCollapsed;
        private int _expandedGridWidth;
        private int _expandedRecordPanelWidth;
        private bool DatabaseChanged { get; set; }
        private ContextMenuStrip _recordContextMenu;
        private ContextMenuStrip _gridBackgroundContextMenu;
        private ToolStripMenuItem _contextEditMenuItem;
        private ToolStripMenuItem _contextCopyMenuItem;
        private ToolStripMenuItem _contextCutMenuItem;
        private ToolStripMenuItem _contextPasteMenuItem;
        private ToolStripMenuItem _contextDuplicateMenuItem;
        private ToolStripMenuItem _contextPdfActionsMenuItem;
        private ToolStripMenuItem _contextTryAutopairPdfMenuItem;
        private ToolStripMenuItem _contextRebindPdfMenuItem;
        private ToolStripMenuItem _contextUnbindPdfMenuItem;
        private ToolStripMenuItem _contextFlagsMenuItem;
        private ToolStripMenuItem _contextNoFlagMenuItem;
        private ToolStripMenuItem _contextFlagGreenMenuItem;
        private ToolStripMenuItem _contextFlagOrangeMenuItem;
        private ToolStripMenuItem _contextFlagPurpleMenuItem;
        private ToolStripMenuItem _contextFlagRedMenuItem;
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

        private void InitializeRecordPanelToggleUi()
        {
            _expandedGridWidth = Math.Max(MinimumExpandedGridWidth, dataGridView1.Width);
            _expandedRecordPanelWidth = Math.Max(MinimumExpandedRecordPanelWidth, panel1.Width);
            panelRecordPanelToggle.Width = RecordPanelToggleWidth;
            ApplyRecordPanelLayout();
        }

        private void btnToggleRecordPanel_Click(object sender, EventArgs e)
        {
            ToggleRecordPanelVisibility();
        }

        private void splitter1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (_isRecordPanelCollapsed)
                return;

            StoreExpandedRecordPanelMetrics();
        }

        private void ToggleRecordPanelVisibility()
        {
            if (_isRecordPanelCollapsed)
            {
                _isRecordPanelCollapsed = false;
                ApplyRecordPanelLayout();
                lblStatus.Text = "Record panel expanded.";
                return;
            }

            StoreExpandedRecordPanelMetrics();
            _isRecordPanelCollapsed = true;
            ApplyRecordPanelLayout();
            lblStatus.Text = "Record panel hidden.";
        }

        private void StoreExpandedRecordPanelMetrics()
        {
            if (_isRecordPanelCollapsed)
                return;

            if (dataGridView1.Width >= MinimumExpandedGridWidth)
                _expandedGridWidth = dataGridView1.Width;

            if (panel1.Width >= MinimumExpandedRecordPanelWidth)
                _expandedRecordPanelWidth = panel1.Width;
        }

        private void ApplyRecordPanelLayout()
        {
            SuspendLayout();
            try
            {
            if (_isRecordPanelCollapsed)
            {
                panel1.Visible = false;
                splitter1.Visible = false;
                panelRecordPanelToggle.Dock = DockStyle.Right;
                btnToggleRecordPanel.Text = "<";
                dataGridView1.Dock = DockStyle.Fill;
                RestoreCollapsedRecordPanelDockOrder();
                panelRecordPanelToggle.BringToFront();
                return;
            }

            dataGridView1.Dock = DockStyle.Left;
            dataGridView1.Width = CalculateExpandedGridWidth();
            splitter1.Visible = true;
            panelRecordPanelToggle.Dock = DockStyle.Left;
            panel1.Visible = true;
            panel1.Dock = DockStyle.Fill;
            btnToggleRecordPanel.Text = ">";
            RestoreExpandedRecordPanelDockOrder();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private void RestoreExpandedRecordPanelDockOrder()
        {
            Controls.SetChildIndex(panel1, 0);
            Controls.SetChildIndex(panelRecordPanelToggle, 1);
            Controls.SetChildIndex(splitter1, 2);
            Controls.SetChildIndex(dataGridView1, 3);
        }

        private void RestoreCollapsedRecordPanelDockOrder()
        {
            Controls.SetChildIndex(panelRecordPanelToggle, 0);
            Controls.SetChildIndex(dataGridView1, 1);
        }

        private int CalculateExpandedGridWidth()
        {
            int availableWidth = ClientSize.Width - splitter1.Width - panelRecordPanelToggle.Width - _expandedRecordPanelWidth;
            int maxGridWidth = Math.Max(MinimumExpandedGridWidth, availableWidth);
            return Math.Max(MinimumExpandedGridWidth, Math.Min(_expandedGridWidth, maxGridWidth));
        }

        private StatusStripOperationHandle StartTrackedOperation(string key, string name, string details = null, bool silentIfAlreadyRunning = false, Action cancelAction = null, bool isBlocking = false)
        {
            StatusStripOperationHandle operation = _operationManager.StartOperation(key, name, details, isBlocking);
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
            entries = ApplyCurrentSort(entries);

            visibleEntries.Clear();
            visibleEntries.AddRange(entries);

            bindingSource1.DataSource = null;
            dataGridView1.DataSource = null;
            DataTable dt = BuildTable(entries, Program.AppSettings.Data.Columns);
            bindingSource1.DataSource = dt;
            dataGridView1.DataSource = bindingSource1;
            dataGridView1.Columns["Entry"].Visible = false;
            ConfigureGridSorting();
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

            LoadData(entries.ToArray(), txtSearch.Text);

            if (selectedEntries.Length > 0)
                SelectEntriesInGrid(selectedEntries);
            else if (currentEntry != null)
                SelectEntriesInGrid(new[] { currentEntry });

            if (statusMessage != null)
                lblStatus.Text = statusMessage;
        }

        private BibtexEntry[] ApplyCurrentSort(IEnumerable<BibtexEntry> sourceEntries)
        {
            BibtexEntry[] sourceArray = sourceEntries?.ToArray() ?? Array.Empty<BibtexEntry>();
            if (string.IsNullOrWhiteSpace(_currentSortColumnName) || !_currentSortDirection.HasValue || sourceArray.Length <= 1)
                return sourceArray;

            bool sortAsNumeric = IsNumericColumn(sourceArray, _currentSortColumnName);
            List<BibtexEntry> sorted = sourceArray.ToList();
            sorted.Sort((left, right) =>
            {
                int comparison = CompareEntryValues(left, right, _currentSortColumnName, sortAsNumeric);
                return _currentSortDirection == ListSortDirection.Descending
                    ? -comparison
                    : comparison;
            });

            return sorted.ToArray();
        }

        private void ConfigureGridSorting()
        {
            if (dataGridView1 == null)
                return;

            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                if (column == null)
                    continue;

                column.SortMode = column.Visible
                    ? DataGridViewColumnSortMode.Programmatic
                    : DataGridViewColumnSortMode.NotSortable;

                column.HeaderCell.SortGlyphDirection = SortOrder.None;
            }

            if (string.IsNullOrWhiteSpace(_currentSortColumnName) || !_currentSortDirection.HasValue)
                return;

            DataGridViewColumn sortedColumn = dataGridView1.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(column => string.Equals(GetSortColumnName(column), _currentSortColumnName, StringComparison.OrdinalIgnoreCase));

            if (sortedColumn != null)
            {
                sortedColumn.HeaderCell.SortGlyphDirection = _currentSortDirection == ListSortDirection.Ascending
                    ? SortOrder.Ascending
                    : SortOrder.Descending;
            }
        }

        private void dataGridView1_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || dataGridView1 == null || e.ColumnIndex >= dataGridView1.Columns.Count)
                return;

            DataGridViewColumn clickedColumn = dataGridView1.Columns[e.ColumnIndex];
            string sortColumnName = GetSortColumnName(clickedColumn);
            if (string.IsNullOrWhiteSpace(sortColumnName) || string.Equals(sortColumnName, "Entry", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.Equals(_currentSortColumnName, sortColumnName, StringComparison.OrdinalIgnoreCase))
            {
                _currentSortDirection = _currentSortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                _currentSortColumnName = sortColumnName;
                _currentSortDirection = ListSortDirection.Ascending;
            }

            string directionLabel = _currentSortDirection == ListSortDirection.Ascending ? "ascending" : "descending";
            RefreshGrid(statusMessage: $"Sorted by {sortColumnName} ({directionLabel}).");
        }

        private string GetSortColumnName(DataGridViewColumn column)
        {
            if (column == null)
                return null;

            return string.IsNullOrWhiteSpace(column.DataPropertyName)
                ? column.Name
                : column.DataPropertyName;
        }

        private bool IsNumericColumn(IEnumerable<BibtexEntry> sourceEntries, string columnName)
        {
            bool hasNumericValue = false;

            foreach (BibtexEntry entry in sourceEntries ?? Array.Empty<BibtexEntry>())
            {
                string value = GetSortableValue(entry, columnName);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (!TryParseSortableNumber(value, out _))
                    return false;

                hasNumericValue = true;
            }

            return hasNumericValue;
        }

        private int CompareEntryValues(BibtexEntry left, BibtexEntry right, string columnName, bool sortAsNumeric)
        {
            string leftValue = GetSortableValue(left, columnName);
            string rightValue = GetSortableValue(right, columnName);

            int comparison = CompareSortValues(leftValue, rightValue, sortAsNumeric);
            if (comparison != 0)
                return comparison;

            comparison = CompareLogicalText(left?.Key, right?.Key);
            if (comparison != 0)
                return comparison;

            return CompareLogicalText(left?.Type, right?.Type);
        }

        private string GetSortableValue(BibtexEntry entry, string columnName)
        {
            if (entry == null || string.IsNullOrWhiteSpace(columnName))
                return null;

            if (string.Equals(columnName, "Key", StringComparison.OrdinalIgnoreCase))
                return entry.Key;

            if (string.Equals(columnName, "Entry Type", StringComparison.OrdinalIgnoreCase))
                return entry.Type;

            return entry.GetTagValue(columnName);
        }

        private int CompareSortValues(string leftValue, string rightValue, bool sortAsNumeric)
        {
            bool leftEmpty = string.IsNullOrWhiteSpace(leftValue);
            bool rightEmpty = string.IsNullOrWhiteSpace(rightValue);

            if (leftEmpty && rightEmpty)
                return 0;

            if (leftEmpty)
                return 1;

            if (rightEmpty)
                return -1;

            if (sortAsNumeric &&
                TryParseSortableNumber(leftValue, out decimal leftNumber) &&
                TryParseSortableNumber(rightValue, out decimal rightNumber))
            {
                return leftNumber.CompareTo(rightNumber);
            }

            return CompareLogicalText(leftValue, rightValue);
        }

        private int CompareLogicalText(string left, string right)
        {
            left = left ?? string.Empty;
            right = right ?? string.Empty;

            return StrCmpLogicalW(left, right);
        }

        private bool TryParseSortableNumber(string value, out decimal parsed)
        {
            string normalized = (value ?? string.Empty).Trim();
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed) ||
                   decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed);
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

        private async void autoupdateJCRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await StartAutoupdateJcrOperationAsync();
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
                    Dictionary<string, JournalReportsDto> reportsByName = (Program.JournalsDatabase.Data.JournalReports ?? new List<JournalReportsDto>())
                        .Where(report => string.IsNullOrWhiteSpace(NormalizeJournalNameForLookup(report?.Journal?.Name)) == false)
                        .GroupBy(report => NormalizeJournalNameForLookup(report.Journal.Name))
                        .ToDictionary(group => group.Key, group => group.First());
                    Dictionary<string, string> lookupReasonsByJournal = (result.LookupIssues ?? new List<JcrJournalLookupIssue>())
                        .Where(item => string.IsNullOrWhiteSpace(NormalizeJournalNameForLookup(item?.JournalName)) == false)
                        .GroupBy(item => NormalizeJournalNameForLookup(item.JournalName))
                        .ToDictionary(group => group.Key, group => group.Last().Reason);
                    JcrCoverageReport coverageReport = BuildJcrCoverageReport(entries.ToArray(), reportsByName, lookupReasonsByJournal);

                    string summary = result.MissingJournalCount == 0
                        ? "No missing journals."
                        : $"Resolved {result.ResolvedJournalCount}/{result.MissingJournalCount} missing journals.";

                    operation.Complete(summary, "Click to see details.");

                    if (result.MissingJournalCount == 0)
                        lblStatus.Text = "Journal database is already up to date.";
                    else if (result.NotFoundJournalCount > 0)
                        lblStatus.Text = $"JCR update finished. Resolved {result.ResolvedJournalCount}/{result.MissingJournalCount}, not found {result.NotFoundJournalCount}.";
                    else
                        lblStatus.Text = $"Journal database updated. Resolved {result.ResolvedJournalCount}/{result.MissingJournalCount}.";

                    log.Complete($"{summary} Added to local DB: {result.AddedJournalCount}. Missing: {result.MissingJournalCount}, not found: {result.NotFoundJournalCount}.");
                    string details = BuildJcrCoverageDetails(
                        coverageReport.ResolvedEntries,
                        coverageReport.MissingJcrTagEntries,
                        coverageReport.MissingJournalEntries,
                        coverageReport.UnresolvedEntries,
                        coverageReport.ErrorEntries,
                        coverageReport.ResolvedRecordDetails,
                        coverageReport.UnresolvedRecordDetails,
                        coverageReport.MissingJournalRecordDetails,
                        coverageReport.ErrorRecordDetails,
                        "These records have journal and their JCR data is now available");
                    PublishReport(
                        "Update JCR",
                        summary,
                        details,
                        result.NotFoundJournalCount > 0 || coverageReport.ErrorEntries > 0 ? OperationReportSeverity.Warning : OperationReportSeverity.Info,
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

        private async Task StartAutoupdateJcrOperationAsync(CancellationToken externalCancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(Program.AppSettings.Data.JcrApiKey))
            {
                lblStatus.Text = "JCR Api key is not set.";
                return;
            }

            StatusStripOperationHandle operation = StartTrackedOperation(
                "autoupdate-jcr",
                "Autoupdate JCR",
                "Update Journals Database -> Create extra JCR tags");
            if (operation == null)
                return;

            ProcessLogScope log = BeginProcessLog("Autoupdate JCR", "Update Journals Database -> Create extra JCR tags");
            EntryChangeSnapshot overallChangeSnapshot = CaptureEntryChanges(entries.ToArray());
            using (CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken))
            using (ReportScopeContext reportScope = BeginReportScope(
                "Autoupdate JCR",
                "Autoupdate JCR started.",
                $"Records: {entries.Count}{Environment.NewLine}Subtasks: Update Journals Database -> Create extra JCR tags"))
            {
                operation.RegisterCancellation(cancellation.Cancel);

                try
                {
                    lblStatus.Text = "Autoupdate JCR started.";

                    JcrUpdateResult updateResult;
                    string updateSummary;
                    string updateDetails;
                    OperationReportSeverity updateSeverity;
                    using (ReportScopeContext updateScope = BeginReportScope(
                        "Update Journals Database",
                        "Update Journals Database started.",
                        "Subtask 1/2"))
                    {
                        operation.Report("Update Journals Database", "Subtask 1/2 running as visible task.", 1, 2, false);
                        updateResult = await RunVisibleAutoupdateJcrUpdateSubtaskAsync(cancellation.Token, cancellation.Cancel);

                        Dictionary<string, JournalReportsDto> updatedReportsByName = BuildJcrReportsByName();
                        Dictionary<string, string> lookupReasonsByJournal = BuildJcrLookupReasonsByJournal(updateResult);
                        JcrCoverageReport updateCoverageReport = BuildJcrCoverageReport(entries.ToArray(), updatedReportsByName, lookupReasonsByJournal);
                        updateSummary = updateResult.MissingJournalCount == 0
                            ? "No missing journals."
                            : $"Resolved {updateResult.ResolvedJournalCount}/{updateResult.MissingJournalCount} missing journals.";
                        updateDetails = BuildJcrCoverageDetails(
                            updateCoverageReport.ResolvedEntries,
                            updateCoverageReport.MissingJcrTagEntries,
                            updateCoverageReport.MissingJournalEntries,
                            updateCoverageReport.UnresolvedEntries,
                            updateCoverageReport.ErrorEntries,
                            updateCoverageReport.ResolvedRecordDetails,
                            updateCoverageReport.UnresolvedRecordDetails,
                            updateCoverageReport.MissingJournalRecordDetails,
                            updateCoverageReport.ErrorRecordDetails,
                            "These records have journal and their JCR data is now available");
                        updateSeverity =
                            updateResult.NotFoundJournalCount > 0 || updateCoverageReport.ErrorEntries > 0
                                ? OperationReportSeverity.Warning
                                : OperationReportSeverity.Info;

                        updateScope.Complete(
                            updateSummary,
                            updateDetails,
                            updateSeverity);
                        operation.Report("Update Journals Database", "Subtask 1/2 completed.", 1, 2, false);
                        LogProcessProgress(log, "Update Journals Database completed.");
                    }

                    JcrTagCreationResult tagResult;
                    EntryChangeReport changeReport;
                    string tagSummary;
                    string tagDetails;
                    OperationReportSeverity tagSeverity;
                    using (ReportScopeContext createScope = BeginReportScope(
                        "Create extra JCR tags",
                        "Create extra JCR tags started.",
                        "Subtask 2/2"))
                    {
                        operation.Report("Create extra JCR tags", "Subtask 2/2 running as visible task.", 2, 2, false);
                        (tagResult, changeReport, tagSummary, tagDetails, tagSeverity) = await RunVisibleAutoupdateCreateExtraJcrTagsSubtaskAsync(cancellation.Token, cancellation.Cancel);

                        createScope.Complete(tagSummary, tagDetails, tagSeverity, changeReport);
                        operation.Report("Create extra JCR tags", "Subtask 2/2 completed.", 2, 2, false);
                        LogProcessProgress(log, "Create extra JCR tags completed.");
                    }

                    EntryChangeReport overallChangeReport = BuildEntryChangeReport(overallChangeSnapshot);
                    string summary = updateResult.MissingJournalCount == 0
                        ? $"Autoupdate JCR finished. {tagSummary}"
                        : $"Autoupdate JCR finished. Resolved {updateResult.ResolvedJournalCount}/{updateResult.MissingJournalCount} journals and updated {tagResult.UpdatedEntries} record(s).";
                    string details =
                        $"Subtask 1/2: Update Journals Database{Environment.NewLine}" +
                        $"Resolved journals: {updateResult.ResolvedJournalCount}/{updateResult.MissingJournalCount}{Environment.NewLine}{Environment.NewLine}" +
                        $"Subtask 2/2: Create extra JCR tags{Environment.NewLine}" +
                        $"Updated records: {tagResult.UpdatedEntries}{Environment.NewLine}" +
                        $"Records still missing JCR tags: {tagResult.MissingJcrTagEntries}";

                    operation.Complete(summary, details);
                    lblStatus.Text = summary;
                    log.Complete(summary);
                    reportScope.Complete(
                        summary,
                        details,
                        (updateResult.NotFoundJournalCount > 0 || tagSeverity != OperationReportSeverity.Info)
                            ? OperationReportSeverity.Warning
                            : OperationReportSeverity.Info,
                        overallChangeReport);
                }
                catch (OperationCanceledException)
                {
                    operation.Cancel("Cancelled", "Autoupdate JCR was stopped by user.");
                    lblStatus.Text = "Autoupdate JCR cancelled.";
                    log.Complete("Autoupdate JCR cancelled.");
                    reportScope.Complete("Autoupdate JCR cancelled.", null, OperationReportSeverity.Warning);
                }
                catch (Exception ex)
                {
                    operation.Fail(ex, "Failed");
                    lblStatus.Text = ex.Message;
                    log.Fail(ex, "Autoupdate JCR failed.");
                    reportScope.Complete("Autoupdate JCR failed.", ex.Message, OperationReportSeverity.Error);
                }
                finally
                {
                    log.Dispose();
                }
            }
        }

        private async Task<JcrUpdateResult> RunVisibleAutoupdateJcrUpdateSubtaskAsync(CancellationToken cancellationToken, Action cancelAction)
        {
            StatusStripOperationHandle childOperation = StartTrackedOperation(
                "update-jcr",
                "Update Journals Database",
                "Subtask 1/2 of Autoupdate JCR",
                silentIfAlreadyRunning: true);
            if (childOperation == null)
                throw new InvalidOperationException("Update Journals Database is already running.");

            childOperation.RegisterCancellation(cancelAction);

            try
            {
                childOperation.Report("Update Journals Database", "Subtask 1/2 of Autoupdate JCR", null, null, true);
                JcrUpdateResult result = await RunUpdateJcrAsync(childOperation, cancellationToken);
                string summary = result.MissingJournalCount == 0
                    ? "No missing journals."
                    : $"Resolved {result.ResolvedJournalCount}/{result.MissingJournalCount} missing journals.";
                childOperation.Complete(summary, "Click to see details.");
                return result;
            }
            catch (OperationCanceledException)
            {
                childOperation.Cancel("Cancelled", "Update Journals Database was stopped by user.");
                throw;
            }
            catch (Exception ex)
            {
                childOperation.Fail(ex, "Failed");
                throw;
            }
        }

        private async Task<(JcrTagCreationResult Result, EntryChangeReport ChangeReport, string Summary, string Details, OperationReportSeverity Severity)> RunVisibleAutoupdateCreateExtraJcrTagsSubtaskAsync(CancellationToken cancellationToken, Action cancelAction)
        {
            StatusStripOperationHandle childOperation = StartTrackedOperation(
                "create-extra-jcr-tags",
                "Create extra JCR tags",
                "Subtask 2/2 of Autoupdate JCR",
                silentIfAlreadyRunning: true);
            if (childOperation == null)
                throw new InvalidOperationException("Create extra JCR tags is already running.");

            childOperation.RegisterCancellation(cancelAction);

            try
            {
                BibtexEntry[] targetEntries = entries.ToArray();
                Dictionary<string, JournalReportsDto> reportsByName = BuildJcrReportsByName();
                EntryChangeSnapshot tagChangeSnapshot = CaptureEntryChanges(targetEntries);

                childOperation.Report("Create extra JCR tags", "Matching records against local JCR data", null, null, true);
                await Task.Yield();
                JcrTagCreationResult result = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CreateExtraJcrTags(targetEntries, reportsByName);
                }, cancellationToken);

                EntryChangeReport changeReport = BuildEntryChangeReport(tagChangeSnapshot);

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

                childOperation.Complete(summary, "Click to see details.");
                return (result, changeReport, summary, details, severity);
            }
            catch (OperationCanceledException)
            {
                childOperation.Cancel("Cancelled", "Create extra JCR tags was stopped by user.");
                throw;
            }
            catch (Exception ex)
            {
                childOperation.Fail(ex, "Failed");
                throw;
            }
        }

        private Dictionary<string, JournalReportsDto> BuildJcrReportsByName()
        {
            return (Program.JournalsDatabase.Data.JournalReports ?? new List<JournalReportsDto>())
                .Where(report => string.IsNullOrWhiteSpace(NormalizeJournalNameForLookup(report?.Journal?.Name)) == false)
                .GroupBy(report => NormalizeJournalNameForLookup(report.Journal.Name))
                .ToDictionary(group => group.Key, group => group.First());
        }

        private Dictionary<string, string> BuildJcrLookupReasonsByJournal(JcrUpdateResult result)
        {
            return (result?.LookupIssues ?? new List<JcrJournalLookupIssue>())
                .Where(item => string.IsNullOrWhiteSpace(NormalizeJournalNameForLookup(item?.JournalName)) == false)
                .GroupBy(item => NormalizeJournalNameForLookup(item.JournalName))
                .ToDictionary(group => group.Key, group => group.Last().Reason);
        }

        private async Task<JcrUpdateResult> RunUpdateJcrAsync(
            StatusStripOperationHandle operation,
            CancellationToken cancellationToken,
            int subtaskIndex = 1,
            int totalSubtasks = 1)
        {
            ProcessLogScope log = BeginProcessLog("Update JCR inner", "Progress tracking");
            Progress<JcrUpdateProgress> progress = new Progress<JcrUpdateProgress>(update =>
            {
                string details = update?.Details;
                if (totalSubtasks > 1)
                {
                    details = string.IsNullOrWhiteSpace(details)
                        ? $"Subtask {subtaskIndex}/{totalSubtasks}"
                        : $"Subtask {subtaskIndex}/{totalSubtasks}{Environment.NewLine}{details}";

                    operation.Report(update?.Summary, details, subtaskIndex, totalSubtasks, false);
                }
                else
                {
                    operation.Report(update?.Summary, details, update?.Completed, update?.Total, false);
                }

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
                ReloadSettingsIntoUi("Settings updated.");
        }

        private void importSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.CheckFileExists = true;
                openFileDialog.CheckPathExists = true;
                openFileDialog.Filter = "Settings JSON (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.Title = "Import settings";
                openFileDialog.InitialDirectory = GetSettingsImportInitialDirectory();

                if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                    return;

                DialogResult confirmResult = MessageBox.Show(
                    this,
                    "This will replace the current application settings. A backup of the current settings file will be created automatically. Continue?",
                    Program.APP_NAME,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes)
                    return;

                if (Program.TryImportSettings(openFileDialog.FileName, out SettingsImportResult result, out string errorMessage) == false)
                {
                    MessageBox.Show(this, errorMessage, Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Settings import failed.";
                    return;
                }

                ReloadSettingsIntoUi("Settings imported.");

                string migrationSuffix = result.WasNormalizedOrMigrated
                    ? " Imported settings were normalized and migrated to the current version."
                    : string.Empty;
                string backupMessage = string.IsNullOrWhiteSpace(result.BackupFilePath)
                    ? "No previous settings file existed, so no backup file was needed."
                    : $"Backup created: {result.BackupFilePath}";

                MessageBox.Show(
                    this,
                    $"Settings were imported successfully from:{Environment.NewLine}{result.SourceFilePath}{Environment.NewLine}{Environment.NewLine}{backupMessage}{migrationSuffix}",
                    Program.APP_NAME,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                lblStatus.Text = string.IsNullOrWhiteSpace(result.BackupFilePath)
                    ? "Settings imported."
                    : $"Settings imported. Backup: {Path.GetFileName(result.BackupFilePath)}";
            }
        }

        private void ReloadSettingsIntoUi(string statusMessage = null)
        {
            Program.AppSettings.LoadSettings();
            Program.PrepareSettingsData(Program.AppSettings.Data);
            UpdateOpenAddModeUi();
            UpdateAutofixModeUi();
            UpdateSearchModeUi();
            RefreshPipelinesRunMenu();
            RefreshGrid(statusMessage: statusMessage);
            UpdateSearchValidationStatus(statusMessage);
        }

        private string GetSettingsImportInitialDirectory()
        {
            string settingsDirectory = Path.GetDirectoryName(Program.SettingsFilePath);
            if (string.IsNullOrWhiteSpace(settingsDirectory) == false && Directory.Exists(settingsDirectory))
                return settingsDirectory;

            if (string.IsNullOrWhiteSpace(Program.AppSettings?.Data?.LastDirectory) == false &&
                Directory.Exists(Program.AppSettings.Data.LastDirectory))
            {
                return Program.AppSettings.Data.LastDirectory;
            }

            return Application.StartupPath;
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
