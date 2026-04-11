using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.Logs;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            _operationManager = new StatusStripOperationManager(statusStrip1, toolStripStatusLabel1, this);
            UpdateWindowTitle();
            InitializeRecordContextMenu();
            dataGridView1.CellMouseDoubleClick += dataGridView1_CellMouseDoubleClick;
            dataGridView1.CellMouseDown += dataGridView1_CellMouseDown;
            dataGridView1.MouseDown += dataGridView1_MouseDown;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F))
            {
                txtSearch.Focus();
                txtSearch.SelectAll();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
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
        private readonly PdfMatchingService _pdfMatchingService = new PdfMatchingService();
        private ContextMenuStrip _recordContextMenu;
        private ContextMenuStrip _gridBackgroundContextMenu;
        private ToolStripMenuItem _contextEditMenuItem;
        private ToolStripMenuItem _contextCopyMenuItem;
        private ToolStripMenuItem _contextCutMenuItem;
        private ToolStripMenuItem _contextPasteMenuItem;
        private ToolStripMenuItem _contextDuplicateMenuItem;
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
                ThreadCount = GetConfiguredThreadCount()
            };
        }

        private StatusStripOperationHandle StartTrackedOperation(string key, string name, string details = null, bool silentIfAlreadyRunning = false)
        {
            StatusStripOperationHandle operation = _operationManager.StartOperation(key, name, details);
            if (operation == null && !silentIfAlreadyRunning)
                lblStatus.Text = $"{name} is already running.";

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
            if (search != "")
            {
                search = search.ToLower();
                string[] searches = search.Split(',');
                List<BibtexEntry> list = new List<BibtexEntry>();
                foreach (string s in searches)
                {
                    foreach (BibtexEntry entry in entries)
                    {
                        string en = bibtexExporter.EntryToString(entry);
                        if (en.ToLower().Contains(s) && list.Contains(entry) == false)
                            list.Add(entry);
                    }
                }

                entries = list.ToArray();
            }

            visibleEntries.Clear();
            visibleEntries.AddRange(entries);

            bindingSource1.DataSource = null;
            dataGridView1.DataSource = null;
            DataTable dt = BuildTable(entries, Program.AppSettings.Data.Columns);
            bindingSource1.DataSource = dt;
            dataGridView1.DataSource = bindingSource1;
            dataGridView1.Columns["Entry"].Visible = false;
            lblInfo.Text = $"{entries.Length} entries";
        }

        private DataTable BuildTable(BibtexEntry[] entries, string[] userColumns)
        {
            DataTable table = new DataTable();
            table.Columns.Add("Key", typeof(string));
            table.Columns.Add("Entry Type", typeof(string));

            if (userColumns == null || userColumns.Length == 0)
            {
                HashSet<string> uniqueTags = new HashSet<string>();
                foreach (BibtexEntry entry in entries)
                {
                    foreach (BibtexTag tag in entry.Tags)
                    {
                        uniqueTags.Add(tag.Key);
                    }
                }

                userColumns = uniqueTags.ToArray();
            }

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

        private async void Changed()
        {
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

        private async Task StartUpdateJcrOperationAsync(bool startedAutomatically)
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

            try
            {
                lblStatus.Text = "Updating database...";
                JcrUpdateResult result = await RunUpdateJcrAsync(operation);

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
            }
            catch (Exception ex)
            {
                operation.Fail(ex, "Failed");
                lblStatus.Text = ex.Message;
                log.Fail(ex, "JCR update failed.");
            }
            finally
            {
                log.Dispose();
            }
        }

        private async Task<JcrUpdateResult> RunUpdateJcrAsync(StatusStripOperationHandle operation)
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
                    progress);
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
                Program.AppSettings.LoadSettings();
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
                lblStatus.Text = "Loaded latest autosave backup.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to load autosave: " + ex.Message;
            }
        }
    }
}
