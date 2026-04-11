
using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.JCR;
using ScientificReviews.JCR.Dto;
using ScientificReviews.Logs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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
        }

        List<BibtexEntry> entries = new List<BibtexEntry>();
        List<BibtexEntry> visibleEntries = new List<BibtexEntry>();
        BibtexExporter bibtexExporter = new BibtexExporter();

        private CancellationTokenSource _changedCts;
        private readonly SemaphoreSlim _autosaveLock = new SemaphoreSlim(1, 1);
        private readonly StatusStripOperationManager _operationManager;
        private readonly BibtexLoadService _bibtexLoadService = new BibtexLoadService();
        private readonly JcrUpdateService _jcrUpdateService = new JcrUpdateService();
        private readonly PdfExportService _pdfExportService = new PdfExportService();
        private readonly PdfMatchingService _pdfMatchingService = new PdfMatchingService();
        private ContextMenuStrip _recordContextMenu;
        private ToolStripMenuItem _contextEditMenuItem;
        private ToolStripMenuItem _contextCopyMenuItem;
        private ToolStripMenuItem _contextCutMenuItem;
        private ToolStripMenuItem _contextPasteMenuItem;
        private ToolStripMenuItem _contextDuplicateMenuItem;

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
            var operation = _operationManager.StartOperation(key, name, details);
            if (operation == null && !silentIfAlreadyRunning)
            {
                lblStatus.Text = $"{name} is already running.";
            }

            return operation;
        }

        private void LoadData(BibtexEntry[] entries, string search = "")
        {
            
            if (search != "")
            {
                search = search.ToLower();  
                var searches = search.Split(',');
                List<BibtexEntry> list = new List<BibtexEntry>();
                foreach (var s in searches)
                {                    
                    foreach (BibtexEntry entry in entries)
                    {
                        string en = bibtexExporter.EntryToString(entry);
                        if (en.ToLower().Contains(s))
                        {
                            if (list.Contains(entry) == false)
                                list.Add(entry);
                        }
                    }
                }
                entries = list.ToArray();
            }

            visibleEntries.Clear();
            visibleEntries.AddRange(entries);

            bindingSource1.DataSource = null;
            dataGridView1.DataSource = null;
            var dt = BuildTable(entries, Program.AppSettings.Data.Columns);
            bindingSource1.DataSource = dt;
            dataGridView1.DataSource = bindingSource1;

            // skrýt interní sloupec
            dataGridView1.Columns["Entry"].Visible = false;

            lblInfo.Text = $"{entries.Length} entries";

        }

        private DataTable BuildTable(BibtexEntry[] entries, string[] userColumns)
        {
            var table = new DataTable();

            table.Columns.Add("Key", typeof(string));
            table.Columns.Add("Entry Type", typeof(string));

            // vytvoření sloupců
            if (userColumns == null || userColumns.Length == 0)
            {
                // pokud nejsou nastavené žádné sloupce, vytvoř všechny unikátní tagy jako sloupce
                HashSet<string> uniqueTags = new HashSet<string>();
                foreach (var entry in entries)
                {
                    foreach (var tag in entry.Tags)
                    {
                        uniqueTags.Add(tag.Key);
                    }
                }
                userColumns = uniqueTags.ToArray();
            }

            // vytvoření sloupců
            foreach (var col in userColumns)
                table.Columns.Add(col, typeof(string));

            table.Columns.Add("Entry", typeof(BibtexEntry)); // interní, skrytý sloupec

            // naplnění dat
            foreach (var entry in entries)
            {
                var row = table.NewRow();
                foreach (var col in userColumns)
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
            var s = Program.AppSettings.Data;

            if (!s.AllowBackup)
                return;

            // NOTE: NumberOfBackups must be INT in settings
            int keepBackups = s.NumberOfBackups;
            if (keepBackups <= 0)
                return;

            if (string.IsNullOrWhiteSpace(s.BackupFolder))
            {
                lblStatus.Text = "Backup folder is not set.";
                return;
            }

            // Debounce: delay autosave a bit (prevents hundreds of backups while typing)
            _changedCts?.Cancel();
            var cts = new CancellationTokenSource();
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
            try
            {
                // Export current in-memory database to string
                string content = new BibtexExporter().EntriesToString(entries.ToArray());

                // Write snapshot + rotate backups on background thread
                await Task.Run(() =>
                {
                    BibtexAutosaveManager.SaveSnapshot(s.BackupFolder, keepBackups, content);
                });

                lblStatus.Text = "Autosave backup created";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Autosave failed: " + ex.Message;
            }
            finally
            {
                _autosaveLock.Release();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Program.AppSettings.SaveSettings();            
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

            var operation = StartTrackedOperation(
                "update-jcr",
                "Update JCR",
                "Fetching missing journals from Clarivate",
                startedAutomatically);
            if (operation == null)
                return;

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
            }
            catch (Exception ex)
            {
                operation.Fail(ex, "Failed");
                lblStatus.Text = ex.Message;
            }
        }

        private async Task<JcrUpdateResult> RunUpdateJcrAsync(StatusStripOperationHandle operation)
        {
            var progress = new Progress<JcrUpdateProgress>(update =>
            {
                operation.Report(update?.Summary, update?.Details, update?.Completed, update?.Total, false);
            });

            return await _jcrUpdateService.UpdateMissingJournalsAsync(
                entries,
                Program.JournalsDatabase.Data.JournalReports,
                Program.AppSettings.Data.JcrApiKey,
                DateTime.Now.Year - 1,
                () => Program.JournalsDatabase.Save(),
                message => AppLog.Log(message, AppLog.MessageType.Error),
                progress);
        }



        private void addTag_Click(object sender, EventArgs e)
        {
            AddTag();
        }

        private void AddTag()
        {
            DataRowView drv = (DataRowView)bindingSource1.Current;
            if (drv == null)
                return;
            if (drv.Row != null)
            {
                var entry = (BibtexEntry)(drv.Row["Entry"]);

                InputGridForm frm = new InputGridForm();
                frm.Object = new BibtexTag();
                if (frm.ShowDialog(this) == DialogResult.OK)
                {
                    var newTag = frm.Object as BibtexTag;
                    if (string.IsNullOrEmpty(newTag.Key))
                    {
                        lblStatus.Text = "Key should not be empty!";
                        return;
                    }
                    var list = entry.Tags.ToList();
                    list.Add(newTag);
                    entry.Tags = list.ToArray();
                    lblStatus.Text = string.Empty;
                    LoadData(visibleEntries.ToArray());
                }
            }            
        }
        private void AddTagToSelected()
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return;
            
            InputGridForm frm = new InputGridForm();
            frm.Object = new BibtexTag();

            if (frm.ShowDialog(this) != DialogResult.OK)
                return;

            var newTag = frm.Object as BibtexTag;

            if (string.IsNullOrEmpty(newTag.Key))
            {
                lblStatus.Text = "Key should not be empty!";
                return;
            }
            
            foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows)
            {
                if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                {
                    var entry = (BibtexEntry)drv.Row["Entry"];
                    if (entry != null)
                    {
                        var list = entry.Tags.ToList();
                        list.Add(newTag.DeepClone());
                        entry.Tags = list.ToArray();
                    }
                }
            }
            lblStatus.Text = string.Empty;
            // reload dataset
            LoadData(visibleEntries.ToArray());


        }

        private async void exportVisibleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportDatabaseAsync(visibleEntries.ToArray());
        }

        private void addTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddTag();
        }

        private void removeTagsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            RemoveTags();               
        }

        private void RemoveTags()
        {
            List<string> tags = new List<string>();
            if (bindingSource1.Current != null)
            {
                int currentIndex = bindingSource1.Position;

                DataRowView drv = bindingSource1.Current as DataRowView;
                var row = drv.Row as DataRow;
                var entry = row["Entry"] as BibtexEntry;
                foreach (var item in entry.Tags)
                {
                    tags.Add(item.Key);
                }

                SelectForm frm = new SelectForm();
                frm.SetData(tags.ToArray());
                frm.SetSelection(tags.ToArray());
                if (frm.ShowDialog(this) == DialogResult.OK)
                {
                    var tagsToLeave = frm.GetSelected().ToList();
                    List<BibtexTag> list = new List<BibtexTag>();
                    foreach (var tag in entry.Tags)
                    {
                        if (tagsToLeave.Contains(tag.Key))
                            list.Add(tag);
                    }
                    entry.Tags = list.ToArray();
                    SelectEntry();
                }
                Changed();
            }
        }

        private void allowEditToolStripMenuItem_Click(object sender, EventArgs e)
        {
            allowEditToolStripMenuItem.Checked = !allowEditToolStripMenuItem.Checked;
            propertyGrid1.Enabled = allowEditToolStripMenuItem.Checked;
            SelectEntry();
        }

        private void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (propertyGrid1.SelectedObject is CustomClass customClass == false)
                return;

            if (propertyGrid1.Tag is BibtexEntry entry == false)
                return;            

            string name = e.ChangedItem.PropertyDescriptor.Name;
            object newValue = e.ChangedItem.Value;

            // přepis základních položek
            if (name == "entryKey")
                entry.Key = newValue?.ToString();

            else if (name == "entryType")
                entry.Type = newValue?.ToString();

            else
            {
                // přepis parametrů (tags)
                foreach (var tag in entry.Tags)
                {
                    if (tag.Key == name)
                    {
                        tag.Value = newValue?.ToString();
                    }
                }                
            }

            // UPDATE DO DATAROW
            var drv = (DataRowView)bindingSource1.Current;
            drv.Row["Entry"] = entry;  // uloží zpět upravený objekt

            ShowEntry(entry, txtSearch.Text);

            LoadData(visibleEntries.ToArray());
            Changed();
        }

        private void exportAsTableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog()
                {
                    CheckPathExists = true,
                    Filter = "Bibtex database *.csv|*.csv"
                };
                if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    string fileName = saveFileDialog.FileName;
                    var table = BuildTable(entries.ToArray(), Program.AppSettings.Data.Columns);
                    CsvExporter.ExportToCsv(table, fileName);                    
                }
                lblStatus.Text = "Export done.";
            }
            catch (Exception ex)
            {

                lblStatus.Text = ex.Message;
            }
        }

        private void addTagToSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddTagToSelected();
        }

        private void deleteSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RemoveSelectedRecords();
        }

        public void SearchEntryTitleOnGoogle(BibtexEntry entry)
        {
            if (entry == null || entry.Tags == null)
                return;

            // Najdi tag "title" (case-insensitive)
            var titleTag = entry.Tags
                .FirstOrDefault(t => string.Equals(t.Key, "title", StringComparison.OrdinalIgnoreCase));

            if (titleTag == null || string.IsNullOrWhiteSpace(titleTag.Value))
                return;

            OpenUrl(BuildGoogleSearchUrl(titleTag.Value));
        }

        public void SearchByDoi(BibtexEntry entry)
        {
            if (entry == null || entry.Tags == null)
                return;

            // Najdi tag "title" (case-insensitive)
            var dioTag = entry.Tags
                .FirstOrDefault(t => string.Equals(t.Key, "doi", StringComparison.OrdinalIgnoreCase));

            if (dioTag == null || string.IsNullOrWhiteSpace(dioTag.Value))
                return;

            string doiValue = dioTag.Value.Trim();
            string normalizedDoi = NormalizeDoi(doiValue);
            string normalizedArxivId = NormalizeArxivId(doiValue);

            if (IsClassicDoi(normalizedDoi))
            {
                OpenUrl($"https://doi.org/{Uri.EscapeDataString(normalizedDoi)}");
                return;
            }

            if (IsArxivIdentifier(normalizedArxivId))
            {
                OpenUrl($"https://arxiv.org/pdf/{Uri.EscapeDataString(normalizedArxivId)}");
                return;
            }

            MessageBox.Show(
                "Unsupported DOI format. The DOI will be opened using Google search.",
                Program.APP_NAME,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            lblStatus.Text = "Unsupported DOI format. Opened in Google search.";
            OpenUrl(BuildGoogleSearchUrl(doiValue));
        }

        private bool IsClassicDoi(string doi)
        {
            return string.IsNullOrWhiteSpace(doi) == false &&
                Regex.IsMatch(doi, @"^10\.\d{4,9}/\S+$", RegexOptions.IgnoreCase);
        }

        private bool IsArxivIdentifier(string doi)
        {
            return string.IsNullOrWhiteSpace(doi) == false &&
                Regex.IsMatch(doi, @"^\d{4}\.\d{4,5}(v\d+)?$", RegexOptions.IgnoreCase);
        }

        private string NormalizeArxivId(string doi)
        {
            if (string.IsNullOrWhiteSpace(doi))
                return null;

            string normalized = doi.Trim();
            normalized = Regex.Replace(normalized, @"^arxiv:\s*", string.Empty, RegexOptions.IgnoreCase);
            normalized = normalized.Trim();

            return normalized.ToLowerInvariant();
        }

        private string BuildGoogleSearchUrl(string query)
        {
            return $"https://www.google.com/search?q={Uri.EscapeDataString(query ?? string.Empty)}";
        }

        private void OpenUrl(string url)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private string GetPdfFileName(BibtexEntry entry)
        {
            string filename = FindPdfFile(entry);
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new Exception("File not exists for entry: " + (entry?.Key ?? "<null>"));
            }

            return filename;
        }

        private string FindPdfFile(BibtexEntry entry, string[] pdfFiles = null)
        {
            return _pdfMatchingService.FindPdfFile(entry, CreatePdfMatchingOptions(), pdfFiles);
        }

        private string FindStoredPdfFile(BibtexEntry entry)
        {
            return _pdfMatchingService.FindStoredPdfFile(entry, Program.AppSettings.Data.PdfFolder);
        }

        private string[] GetPdfFiles()
        {
            return _pdfMatchingService.GetPdfFiles(CreatePdfMatchingOptions());
        }

        private string NormalizeDoi(string doi)
        {
            if (string.IsNullOrWhiteSpace(doi))
                return null;

            string normalized = doi.Trim();
            normalized = Regex.Replace(normalized, @"^https?://(dx\.)?doi\.org/", string.Empty, RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"^doi:\s*", string.Empty, RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"^arxiv:\s*", string.Empty, RegexOptions.IgnoreCase);
            normalized = normalized.Trim().TrimEnd('/', '.', ',', ';');

            return normalized.ToLowerInvariant();
        }

        private void AssignPdfToEntry(BibtexEntry entry, string pdfFilePath)
        {
            _pdfMatchingService.AssignPdfToEntry(entry, pdfFilePath);
        }

        private void ClearPdfAssignment(BibtexEntry entry)
        {
            _pdfMatchingService.ClearPdfAssignment(entry);
        }

        private void UpdateHasPdfTag(BibtexEntry entry, bool hasPdf)
        {
            _pdfMatchingService.UpdateHasPdfTag(entry, hasPdf);
        }

        private void btnGoogle_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows)
                {
                    if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                    {
                        var entry = (BibtexEntry)drv.Row["Entry"];
                        if (entry != null)
                        {
                            SearchEntryTitleOnGoogle(entry);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }

        
        }

        private void btnPdf_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows)
                {
                    if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                    {
                        var entry = (BibtexEntry)drv.Row["Entry"];
                        if (entry != null)
                        {
                            OpenPdfOrPromptManualPair(entry);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private void btnDeleteTag_Click(object sender, EventArgs e)
        {
            RemoveTag();
        }

        private void RemoveTag()
        {
            bool readOnly = !allowEditToolStripMenuItem.Checked;


            if (propertyGrid1.Tag is BibtexEntry entry == false)
                return;

            var gi = propertyGrid1.SelectedGridItem;
            if (gi == null || gi.GridItemType != GridItemType.Property)
                return;

            // Název property (u dynamických položek je často nejlepší brát PropertyDescriptor.Name)
            string name = gi.PropertyDescriptor?.Name ?? gi.Label;
            if (string.IsNullOrWhiteSpace(name))
                return;

            // Základní položky nemaž (uprav si podle svých názvů)
            if (string.Equals(name, "entryKey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "entryType", StringComparison.OrdinalIgnoreCase))
                return;

            // Odebrání tagu z BibtexEntry
            // (přizpůsob podle typu entry.Tags: List<Tag>, Dictionary, apod.)
            int removed = 0;

            // Varianta A: entry.Tags je List<něco s Key/Value>
            var list = entry.Tags.ToList();
            removed = list.RemoveAll(t => string.Equals(t.Key, name, StringComparison.Ordinal));
            entry.Tags = list.ToArray();

            // Pokud nic neodebráno, skonči
            if (removed == 0)
                return;
            DataRowView drv = (DataRowView)bindingSource1.Current;
            if (drv.Row != null)
            {
                entry = (BibtexEntry)(drv.Row["Entry"]);
                ShowEntry(entry, txtSearch.Text);

                CustomClass customClass = new CustomClass();
                customClass.Add(new CustomProperty("entryKey", "Key", entry.Key, "Bibitem", readOnly, true));
                customClass.Add(new CustomProperty("entryType", "Type", entry.Type, "Bibitem", readOnly, true));
                foreach (var tag in entry.Tags)
                {
                    CustomProperty item = new CustomProperty(tag.Key, tag.Key, tag.Value, "Parameters", readOnly, true);
                    customClass.Add(item);
                }
                propertyGrid1.Tag = entry;
                propertyGrid1.SelectedObject = customClass;
            }
            lblSelected.Text = $"({dataGridView1.SelectedRows.Count.ToString()})";
            Changed();
        }

        private void btnRemoveTags_Click(object sender, EventArgs e)
        {
            RemoveTags();
        }

        private void removeTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RemoveTag();
        }

        private BibtexEntry[] GetSelected()
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return new BibtexEntry[0];

            List<BibtexEntry> toExport = new List<BibtexEntry>();

            foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows)
            {
                if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                {
                    var entry = (BibtexEntry)drv.Row["Entry"];
                    toExport.Add(entry);
                }
            }
            return toExport.ToArray();
        }

        private BibtexEntry[] GetSelectedOrdered()
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return new BibtexEntry[0];

            List<BibtexEntry> toExport = new List<BibtexEntry>();

            foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
            {
                if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                {
                    var entry = (BibtexEntry)drv.Row["Entry"];
                    toExport.Add(entry);
                }
            }

            return toExport.ToArray();
        }


        private async void exportSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
           
            await ExportDatabaseAsync(GetSelected());
        }

        private async void autoPairWithPdfsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await StartAutoPairOperationAsync(false);
        }

        private async Task StartAutoPairOperationAsync(bool startedAutomatically)
        {
            var operation = StartTrackedOperation(
                "auto-pair-pdfs",
                "Auto-pair PDFs",
                Program.AppSettings.Data.PdfFolder,
                startedAutomatically);
            if (operation == null)
                return;

            try
            {
                lblStatus.Text = $"Auto-pairing PDFs using {GetConfiguredThreadCount()} thread(s)...";
                PdfAutoPairResult result = await RunAutoPairAsync(operation);

                LoadData(entries.ToArray(), txtSearch.Text);
                Changed();

                if (result.NoPdfsFound)
                {
                    operation.Complete("No PDFs found.", Program.AppSettings.Data.PdfFolder);
                    lblStatus.Text = "No PDFs found in Pdf Folder.";
                    return;
                }

                string summary = $"Direct {result.DirectMatches}, smart {result.SmartMatches}, unmatched {result.Unmatched}";
                operation.Complete(summary, Program.AppSettings.Data.PdfFolder);
                lblStatus.Text = $"Auto-pair finished using {GetConfiguredThreadCount()} thread(s). Direct: {result.DirectMatches}, smart: {result.SmartMatches}, unmatched: {result.Unmatched}.";
            }
            catch (Exception ex)
            {
                operation.Fail(ex, "Failed");
                lblStatus.Text = ex.Message;
            }
        }

        private Task<PdfAutoPairResult> RunAutoPairAsync(StatusStripOperationHandle operation)
        {
            var progress = new Progress<PdfAutoPairProgress>(update =>
            {
                operation.Report(
                    update?.Summary,
                    update?.Details,
                    update?.Completed,
                    update?.Total,
                    update != null && update.IsIndeterminate);
            });

            return _pdfMatchingService.AutoPairAsync(entries, CreatePdfMatchingOptions(), progress);
        }

        private void exportPdfsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (entries.Count == 0)
                {
                    lblStatus.Text = "No records available for export.";
                    return;
                }

                using (var form = new ExportPdfsForm(GetDefaultPdfExportDirectory(), dataGridView1.SelectedRows.Count > 0, RunPdfExportAsync))
                {
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private async Task<ExportPdfsRunResult> RunPdfExportAsync(ExportPdfsRunOptions options, IProgress<ExportPdfsProgress> progress, CancellationToken cancellationToken)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var toExport = options.ExportSelectedOnly ? GetSelected() : entries.ToArray();
            if (toExport.Length == 0)
                throw new InvalidOperationException("No records selected for export.");

            lblStatus.Text = $"Exporting PDFs using {GetConfiguredThreadCount()} thread(s)...";
            ExportPdfsRunResult result = await _pdfExportService.RunExportAsync(
                toExport,
                options,
                _pdfMatchingService,
                CreatePdfMatchingOptions(),
                progress,
                cancellationToken);

            lblStatus.Text = result.Cancelled
                ? $"PDF export cancelled after {result.Completed}/{result.Total}."
                : $"Exported {result.Exported} PDF(s), skipped {result.Skipped}, DOI injected into {result.Injected}.";

            return result;
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (new SettingsForm().ShowDialog(this) == DialogResult.OK)
            {
                Program.AppSettings.LoadSettings();                
            }
        }

        private void btnDoi_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows)
                {
                    if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                    {
                        var entry = (BibtexEntry)drv.Row["Entry"];
                        if (entry != null)
                        {
                            SearchByDoi(entry);
                        }
                    }
                }   
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private void selectedDOIsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelected("doi");
        }

       

        private void selectedKeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelected("key");
        }

        private void selectedTitlesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelected("title");
        }

        private void CopySelected(string tagName)
        {
            var parts = new List<string>();

            foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows)
            {
                if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                {
                    var entry = (BibtexEntry)drv.Row["Entry"];
                    if (entry == null) continue;

                    // special case: "key" means BibTeX entry key
                    if (string.Equals(tagName, "key", StringComparison.OrdinalIgnoreCase))
                    {
                        var k = (entry.Key ?? string.Empty).Trim();
                        if (k.Length > 0)
                            parts.Add(k);

                        continue;
                    }

                    if (entry.Tags != null)
                    {
                        foreach (var tag in entry.Tags)
                        {
                            if (string.Equals(tag.Key, tagName, StringComparison.OrdinalIgnoreCase))
                            {
                                var value = (tag.Value ?? string.Empty).Trim();
                                if (value.Length > 0)
                                    parts.Add(value);
                                break; // one tag per name is enough
                            }
                        }
                    }
                }
            }

            Clipboard.SetText(string.Join(",", parts));
            lblStatus.Text = "Copied to clipboard";
        }

        private void btnAddorEditTag_Click(object sender, EventArgs e)
        {
            AddOrEditTagToSelected();
        }

        private void AddOrEditTagToSelected()
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return;

            // Tag key/value are taken from the form controls
            string key = (txtKey.Text ?? string.Empty).Trim();
            string value = (txtValue.Text ?? string.Empty).Trim();

            // --- Validate tag key ---
            if (string.IsNullOrWhiteSpace(key))
            {
                lblStatus.Text = "Key should not be empty!";
                return;
            }

            // If you treat "key" as a special virtual field (BibTeX entry key), disallow it as a tag key
            if (string.Equals(key, "key", StringComparison.OrdinalIgnoreCase))
            {
                lblStatus.Text = "\"key\" is reserved (BibTeX entry key). Choose another tag name.";
                return;
            }

            // Allowed: starts with letter; then letters/digits/_-:
            // (safe for BibTeX-like fields and CSV export)
            var tagKeyRegex = new Regex(@"^[A-Za-z][A-Za-z0-9_\-:]*$", RegexOptions.Compiled);
            if (!tagKeyRegex.IsMatch(key))
            {
                lblStatus.Text = "Invalid tag key. Use letters/digits and _ - : (must start with a letter).";
                return;
            }
            // ------------------------

            foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows)
            {
                if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                {
                    var entry = (BibtexEntry)drv.Row["Entry"];
                    if (entry != null)
                    {
                        var list = (entry.Tags ?? Array.Empty<BibtexTag>()).ToList();

                        // Add or edit
                        int idx = list.FindIndex(t => t != null &&
                                                     string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0)
                        {
                            list[idx].Value = value; // edit existing
                        }
                        else
                        {
                            list.Add(new BibtexTag { Key = key, Value = value }); // add new
                        }

                        entry.Tags = list.ToArray();
                    }
                }
            }

            lblStatus.Text = string.Empty;

            // reload dataset
            LoadData(visibleEntries.ToArray());
            Changed();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            try
            {
                UpdateWindowTitle();

                // pokud už něco máš načtené (např. z předchozí logiky), tak nepřepisuj
                if (entries != null && entries.Count > 0)
                    return;

                var s = Program.AppSettings.Data;

                if (!s.AllowBackup)
                    return;

                if (string.IsNullOrWhiteSpace(s.BackupFolder))
                {
                    lblStatus.Text = "Backup folder is not set.";
                    return;
                }

                // najdi poslední autosave
                string latestPath = BibtexAutosaveManager.GetLatestBackupPath(s.BackupFolder);

                if (string.IsNullOrWhiteSpace(latestPath) || !File.Exists(latestPath))
                {
                    lblStatus.Text = "No autosave found.";
                    return;
                }

                // načti obsah
                string content = File.ReadAllText(latestPath);
                if (string.IsNullOrWhiteSpace(content))
                {
                    lblStatus.Text = "Autosave is empty.";
                    return;
                }

                // PARSE bibtex -> entries
                // Uprav podle toho, jak se u tebe jmenuje importer/loader.
                // Cíl: vrátit BibtexEntry[] z obsahu souboru.
                var importer = new BibtexParser();
                var loadedEntries = importer.ParseFile(content);   // <- uprav název metody podle sebe

                if (loadedEntries == null || loadedEntries.Length == 0)
                {
                    lblStatus.Text = "Autosave contains no entries.";
                    return;
                }

                // nastav databázi
                entries = loadedEntries.ToList();
                visibleEntries = entries; // pokud máš filtrování, zatím to bereme jako "vše viditelné"

                // refresh UI
                LoadData(visibleEntries.ToArray());

                lblStatus.Text = "Loaded latest autosave backup.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to load autosave: " + ex.Message;
            }
        }

        //private void manualJCRDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //try
        //{
        //    OpenFileDialog dlg = new OpenFileDialog()
        //    {
        //        CheckFileExists = true,
        //        CheckPathExists = true,
        //        Filter = "CSV Files *.csv|*.csv"
        //    };
        //    if (dlg.ShowDialog(this) == DialogResult.OK)
        //    {
        //        string fileName = dlg.FileName;

        //        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        //        {
        //            BadDataFound = null // Ignorování špatných dat
        //        };

        //        using (var reader = new StreamReader(fileName))
        //        using (var csv = new CsvReader(reader, config))
        //        {                        
        //            // Načtení záznamů z CSV do seznamu
        //            var records = csv.GetRecords<JCRCsvDto>();

        //            // Zobrazení načtených záznamů
        //            foreach (var record in records)
        //            {
        //                try
        //                {
        //                    JournalReportsDto journalReportsDto = ConvertJCRCsvDtoToJournalReportsDto(record);
        //                    Program.JournalsDatabase.Data.JournalReports.Add(journalReportsDto);
        //                }
        //                catch (Exception)
        //                {

        //                }


        //            }
        //        }
        //    }
        //    ConsolidateDb();
        //    Program.JournalsDatabase.Save();
        //    lblStatus.Text = "CSV imported";
        //}
        //catch (Exception ex)
        //{
        //    lblStatus.Text = ex.Message;
        //}

        //}

        //private void ConsolidateDb()
        //{
        //    var journalReports = Program.JournalsDatabase.Data.JournalReports;
        //    foreach (var item1 in journalReports)
        //    {
        //        foreach (var item2 in journalReports)
        //        {
        //            if (item1 != item2)
        //            {
        //                if (item1.Journal.Name.ToLower() == item2.Journal.Name.ToLower())
        //                {
        //                    Merge(item1.Ranks.Jif.ToArray(), item2.Ranks.Jif.ToArray());
        //                }
        //            }
        //        }
        //    }
        //    Dictionary<string, JournalReportsDto> journals = new Dictionary<string, JournalReportsDto>();
        //    foreach (var item in journalReports)
        //    {
        //        if (journals.ContainsKey(item.Journal.Name) == false)
        //        {
        //            journals.Add(item.Journal.Name, item);
        //        }
        //    }
        //    Program.JournalsDatabase.Data.JournalReports = journals.Values.ToList();
        //}

        //private List<JifRankDetailDto> Merge(JifRankDetailDto[] entry1, JifRankDetailDto[] entry2)
        //{
        //    List<JifRankDetailDto> mergedTags = entry1.ToList();
        //    Dictionary<string, JifRankDetailDto> tagsDictionary = mergedTags.ToDictionary(tag => tag.Category);

        //    foreach (var tag in entry2)
        //    {
        //        if (!tagsDictionary.ContainsKey(tag.Category))
        //        {
        //            mergedTags.Add(tag);
        //        }
        //    }
        //    return mergedTags;
        //}

        //public static JournalReportsDto ConvertJCRCsvDtoToJournalReportsDto(JCRCsvDto csvDto)
        //{
        //    var journalReportsDto = new JournalReportsDto
        //    {
        //        Year = 2023, // Předpokládáme, že jde o rok 2023
        //        Suppressed = false, // Předpokládáme, že data nejsou potlačená
        //        Journal = new JournalDto
        //        {
        //            Name = csvDto.JournalName
        //        },
        //        Metrics = new MetricsDto
        //        {
        //            ImpactMetrics = new ImpactMetricsDto
        //            {
        //                TotalCites = int.TryParse(csvDto.TotalCitations, out var totalCites) ? totalCites : 0,
        //                Jif = csvDto.Jif2023,
        //                Jci = int.TryParse(csvDto.Jci2023, out var jci) ? jci : 0
        //            },
        //            SourceMetrics = new SourceMetricsDto
        //            {
        //                JifPercentile = int.TryParse(csvDto.JifPercentile, out var jifPercentile) ? jifPercentile : 0,
        //                CitableItems = new CitableItemsDto
        //                {
        //                    ArticlesPercentage = int.TryParse(csvDto.PercentCitableOA, out var percentCitableOA) ? percentCitableOA : 0
        //                }
        //            }
        //        },
        //        Ranks = new RanksDto
        //        {
        //                Jif = new List<JifRankDetailDto>
        //        {
        //            new JifRankDetailDto
        //            {
        //                Category = csvDto.Category,
        //                Edition = csvDto.Edition,
        //                Quartile = csvDto.JifQuartile,
        //                JifPercentile = (int)double.Parse( csvDto.JifPercentile, CultureInfo.InvariantCulture),

        //            }
        //        },
        //                Jci = new List<JciRankDetailDto>
        //        {
        //            new JciRankDetailDto
        //            {
        //                Category = csvDto.Category,
        //                Quartile = csvDto.JifQuartile
        //            }
        //        }
        //            },
        //            JournalData = new JournalDataDto(),
        //            SourceData = new SourceDataDto(),
        //            JournalProfile = new JournalProfileDto()
        //        };

        //    return journalReportsDto;
        //}




    }
}


