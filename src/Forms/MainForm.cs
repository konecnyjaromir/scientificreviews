
using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.JCR;
using ScientificReviews.JCR.Dto;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ScientificReviews.Forms
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        List<BibtexEntry> entries = new List<BibtexEntry>();
        List<BibtexEntry> visibleEntries = new List<BibtexEntry>();
        BibtexExporter bibtexExporter = new BibtexExporter();

        private CancellationTokenSource _changedCts;
        private readonly SemaphoreSlim _autosaveLock = new SemaphoreSlim(1, 1);

        private sealed class PdfArchiveItem
        {
            public string FilePath { get; set; }
            public string FileNameWithoutExtension { get; set; }
            public string StandardizedName { get; set; }
            public Dictionary<string, int> Tokens { get; set; }
            public HashSet<string> Keywords { get; set; }
        }

        private sealed class PdfSimilarityCandidate
        {
            public BibtexEntry Entry { get; set; }
            public PdfArchiveItem Pdf { get; set; }
            public double Score { get; set; }
        }

        private int GetConfiguredThreadCount()
        {
            return Math.Max(1, Program.AppSettings.Data.Threads);
        }

        private ParallelOptions CreateParallelOptions()
        {
            return new ParallelOptions
            {
                MaxDegreeOfParallelism = GetConfiguredThreadCount()
            };
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

        private async void loadBibTexFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog()
                {
                    SelectedPath = Program.AppSettings.Data.LastDirectory
                })
                {
                    DialogResult result = folderDialog.ShowDialog(this);
                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                    {
                        Program.AppSettings.Data.LastDirectory = folderDialog.SelectedPath; 
                        await Task.Run(() =>
                        {                            
                            string[] files = Directory.GetFiles(folderDialog.SelectedPath, "*.bib", SearchOption.AllDirectories);
                            BibtexParser parser = new BibtexParser();
                            foreach (string file in files)
                            {
                                entries.AddRange(parser.ParseFile(File.ReadAllText(file)));
                            }                            
                        });
                        LoadData(entries.ToArray());
                        Changed();
                    }
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }


        }

        private void createEntryKeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> keys = new List<string>();
            foreach (BibtexEntry entry in entries)
            {
                //string key = entry.GetTagValue("author").Split(',')[0].Trim().Replace(" ", "") + entry.GetTagValue("year");

                string authors = entry.GetTagValue("author");
                string year = entry.GetTagValue("year");
                if (authors == null || year == null)
                    continue;

                string key = BibtexUtils.GetFirstAuthorLastName(authors).Replace(" ", "") + year;
                key = key.ToLower();
                string myKey = key;
                int i = 1;
                while (keys.Contains(myKey))
                {
                    myKey = key + "_" + i.ToString();
                    i++;
                }
                entry.Key = myKey;
                keys.Add(myKey);                
            }
            LoadData(entries.ToArray());
          

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

        private void removeTagsToolStripMenuItem_Click(object sender, EventArgs e)
        {           
            List<string> tags = new List<string>();
            foreach (var entry in entries)
            {
                foreach (var tag in entry.Tags)
                {
                    if (tags.Contains(tag.Key) == false)
                    {
                        tags.Add(tag.Key);
                    }
                }
            }
            SelectForm frm = new SelectForm();
            frm.SetData(tags.ToArray());
            frm.SetSelection(Program.AppSettings.Data.SelectedTags);
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                var tagsToLeave = frm.GetSelected().ToList();
                Program.AppSettings.Data.SelectedTags = tagsToLeave.ToArray();
                foreach (var entry in entries)
                {
                    List<BibtexTag> list = new List<BibtexTag>();
                    foreach (var tag in entry.Tags)
                    {
                        if (tagsToLeave.Contains(tag.Key))
                            list.Add(tag);
                    }
                    entry.Tags = list.ToArray();
                }
                Changed();
            }
        }

        private void removeTypesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> types = new List<string>();
            foreach (var entry in entries)
            {
                if (types.Contains(entry.Type) == false)
                    types.Add(entry.Type);
            }
            SelectForm frm = new SelectForm();
            frm.SetData(types.ToArray());
            frm.SetSelection(Program.AppSettings.Data.SelectedTypes);
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                var typesToLeave = frm.GetSelected().ToList();
                Program.AppSettings.Data.SelectedTypes = typesToLeave.ToArray();
                List<BibtexEntry> list = new List<BibtexEntry>();
                foreach (var entry in entries)
                {
                    if (typesToLeave.Contains(entry.Type))
                        list.Add(entry);
                }
                entries = list;
                LoadData(entries.ToArray());
                Changed();
            }
            

        }

        private async void exportDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportDatabaseAsync(entries.ToArray());
        }

        private async Task ExportDatabaseAsync(BibtexEntry[] entries)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog()
                {
                    CheckPathExists = true,
                    Filter = "Bibtex database *.bib|*.bib"
                };
                if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    lblStatus.Text = "Exporting...";
                    await Task.Run(() =>
                    {
                        string fileName = saveFileDialog.FileName;
                        BibtexExporter bibtexExporter = new BibtexExporter();
                        string content = bibtexExporter.EntriesToString(entries);
                        File.WriteAllText(fileName, content);
                    });
                    lblStatus.Text = "Export done.";
                }
                
            }
            catch (Exception ex)
            {

                lblStatus.Text = ex.Message;
            }
        }

        private void dataGridView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectedRecordsToClipboard();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.X)
            {
                CutSelectedRecordsToClipboard();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.V)
            {
                PasteRecordsFromClipboard();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                RemoveSelecedRecords();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void RemoveSelecedRecords()
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {                
                int currentIndex = bindingSource1.Position;
                
                foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(r => r.Index))
                {
                    if (dgvr.DataBoundItem is DataRowView drv)
                    {
                        var row = drv.Row;
                        var entry = row["Entry"] as BibtexEntry;
                        if (entry != null)
                        {
                            entries.Remove(entry);
                            visibleEntries.Remove(entry);
                        }

                    }
                }

                
                LoadData(visibleEntries.ToArray());
                Changed();
                // Korekce indexu po mazání
                if (currentIndex >= bindingSource1.Count)
                    currentIndex = bindingSource1.Count - 1;

                if (currentIndex >= 0)
                    bindingSource1.Position = currentIndex;
            }
        }

        private bool CopySelectedRecordsToClipboard()
        {
            try
            {
                var selectedEntries = GetSelectedOrdered();
                if (selectedEntries.Length == 0)
                {
                    lblStatus.Text = "No records selected.";
                    return false;
                }

                string content = bibtexExporter.EntriesToString(selectedEntries);
                Clipboard.SetText(content);
                lblStatus.Text = $"Copied {selectedEntries.Length} record(s) to clipboard.";
                return true;
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                return false;
            }
        }

        private void btnCopyRecord_Click(object sender, EventArgs e)
        {
            CopySelectedRecordsToClipboard();
        }

        private void CutSelectedRecordsToClipboard()
        {
            var selectedCount = GetSelectedOrdered().Length;
            if (selectedCount == 0)
            {
                lblStatus.Text = "No records selected.";
                return;
            }

            try
            {
                if (!CopySelectedRecordsToClipboard())
                    return;

                RemoveSelecedRecords();
                lblStatus.Text = $"Cut {selectedCount} record(s) to clipboard.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private void btnCutRecord_Click(object sender, EventArgs e)
        {
            CutSelectedRecordsToClipboard();
        }

        private void PasteRecordsFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    lblStatus.Text = "Clipboard does not contain BibTeX records.";
                    return;
                }

                string clipboardText = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(clipboardText))
                {
                    lblStatus.Text = "Clipboard does not contain BibTeX records.";
                    return;
                }

                BibtexParser parser = new BibtexParser();
                var pastedEntries = parser.ParseFile(clipboardText);
                if (pastedEntries == null || pastedEntries.Length == 0)
                {
                    lblStatus.Text = "Clipboard does not contain valid BibTeX records.";
                    return;
                }

                entries.AddRange(pastedEntries);
                LoadData(entries.ToArray(), txtSearch.Text);
                SelectEntriesInGrid(pastedEntries);
                Changed();
                lblStatus.Text = $"Pasted {pastedEntries.Length} record(s).";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private void btnPasteRecord_Click(object sender, EventArgs e)
        {
            PasteRecordsFromClipboard();
        }

        private void SelectEntriesInGrid(IEnumerable<BibtexEntry> selectedEntries)
        {
            var selectedSet = new HashSet<BibtexEntry>(selectedEntries ?? Enumerable.Empty<BibtexEntry>());
            if (selectedSet.Count == 0)
                return;

            var matchingRows = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.DataBoundItem is DataRowView drv && drv.Row != null)
                {
                    var entry = drv.Row["Entry"] as BibtexEntry;
                    if (entry != null && selectedSet.Contains(entry))
                    {
                        matchingRows.Add(row);
                    }
                }
            }

            if (matchingRows.Count == 0)
                return;

            dataGridView1.ClearSelection();
            foreach (var row in matchingRows)
            {
                row.Selected = true;
            }

            if (matchingRows[0].Cells.Count > 0)
            {
                dataGridView1.CurrentCell = matchingRows[0].Cells[0];
            }
        }


        private void splitter1_SplitterMoved(object sender, SplitterEventArgs e)
        {

        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            SelectEntry();
        }

        private void SelectEntry()
        {
            if (bindingSource1.Current is DataRowView == false)
                return;
            bool readOnly = !allowEditToolStripMenuItem.Checked;

            DataRowView drv = (DataRowView)bindingSource1.Current;
            if (drv.Row != null)
            {
                var entry = (BibtexEntry)(drv.Row["Entry"]);
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
        }

        private void ShowEntry(BibtexEntry entry, string search = "")
        {
            // 1) Vypiš text
            string text = bibtexExporter.EntryToString(entry);
            text = text.ToLower();
            search = search.ToLower();
            richTextBox1.Text = text;
            // 2) Nic nehledáme -> hotovo
            if (string.IsNullOrWhiteSpace(search))
                return;

            // 3) Ulož selection, ať uživateli nic "neskáče"
            int selStart = richTextBox1.SelectionStart;
            int selLength = richTextBox1.SelectionLength;

            // 4) Odstraň případné staré zvýraznění (volitelné, ale obvykle chceš)
            richTextBox1.SelectAll();
            richTextBox1.SelectionBackColor = richTextBox1.BackColor;

            // 5) Hledej a zvýrazňuj všechny výskyty (case-insensitive)
            int startIndex = 0;
            while (startIndex < richTextBox1.TextLength)
            {
                int idx = text.IndexOf(
                    search,
                    startIndex
                );

                // Správně: nepoužívat MatchCase => case-insensitive
                idx = richTextBox1.Find(search, startIndex, RichTextBoxFinds.None);

                if (idx < 0) break;

                richTextBox1.Select(idx, search.Length);
                richTextBox1.SelectionBackColor = Color.Yellow;

                startIndex = idx + search.Length;
            }

            // 6) Vrať selection
            richTextBox1.Select(selStart, selLength);
            richTextBox1.SelectionBackColor = richTextBox1.BackColor; // aby se výběr netvářil žlutě
        }

        private void exportDOIsToolStripMenuItem_Click(object sender, EventArgs e)
        {
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
                    BibtexExporter bibtexExporter = new BibtexExporter();
                    string[] content = bibtexExporter.GetDois(entries.ToArray());
                    File.WriteAllLines(fileName, content);
                }
                lblStatus.Text = "Export done.";
            }
            catch (Exception ex)
            {

                lblStatus.Text = ex.Message;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Program.AppSettings.SaveSettings();            
        }

        private void removeDuplicitiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            entries = BibtexUtils.RemoveDuplicateEntriesByTag(entries, "title");
            LoadData(entries.ToArray());
        }

        private void removeWithoutDOIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<BibtexEntry> list = new List<BibtexEntry>();
            foreach (var entry in entries)
            {
                if (entry.GetTagValue("doi") != null)
                {
                    list.Add(entry);
                }
            }
            entries = list;
            LoadData(entries.ToArray());
            Changed();
        }

        private void removeDuplicitiesByDOIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            entries = BibtexUtils.RemoveDuplicateEntriesByTag(entries, "doi");
            LoadData(entries.ToArray());
            Changed();
        }
       

        private void updatePageTagFormatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BibtexUtils.UpdatePages(entries);
            Changed();
        }

        private async void updateJournalsDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                //Get actual year - 1
                int year = DateTime.Now.Year - 1;

                lblStatus.Text = "Updating database...";
                // Find journals, that missing information about quartiles
                JcrApiClient jcrApiClient = new JcrApiClient(Program.AppSettings.Data.JcrApiKey);
                List<string> missingJournals = new List<string>();
                var journalReports = Program.JournalsDatabase.Data.JournalReports;
                Dictionary<string, JournalReportsDto> dic = journalReports.ToDictionary(rep => rep.Journal.Name.ToLower());
                foreach (var entry in entries)
                {
                    if (entry.Type == "article")
                    {
                        string journalName = BibtexUtils.RemoveLatex(entry.GetTagValue("journal")).ToLower();

                        if (dic.Keys.Contains(journalName) == false)
                        {
                            if (missingJournals.Contains(journalName) == false)
                                missingJournals.Add(journalName);
                        }
                    }
                }
                missingJournals.Sort();

                var notFoundJournals = new List<string>();

                foreach (string missingJournal in missingJournals)
                {
                    lblStatus.Text = $"Updating database... ({missingJournal})";

                    // Get journals by name
                    try
                    {
                        var resp = await jcrApiClient.GetJournalsAsync(missingJournal.Replace("&", "").Replace("-", " "));
                        foreach (var hit in resp.Hits)
                        {
                            JournalReportsDto report = null;
                            //Try fetch journal report for actual year, if not exists, try to fetch for previous year, and so on, until 2020. I want to do this, because some journals have not yet published report for actual year, but they have published for previous years, and I want to have at least some data about quartiles, even if it is not for actual year.
                            for (int i = year; i > 2020; i--)
                            {
                                try
                                {
                                    // Get journal reports for particular journal and year
                                    report = await jcrApiClient.GetJournalReportsAsync(hit.Id, i);
                                    break;
                                }
                                catch (Exception)
                                {
                                    continue;
                                }

                            }

                            if (report == null) {                                     
                                notFoundJournals.Add(missingJournal);
                                lblStatus.Text = $"Journal {missingJournal} was not found for any year, skipping...";
                                continue;
                            }

                            var dicTmp = journalReports.ToDictionary(rep => rep.Journal.Name.ToLower());
                            if (dicTmp.ContainsKey(report.Journal.Name.ToLower()) == false)
                            {
                                journalReports.Add(report);
                            }
                            else
                            {
                                //do nothing, because we do not want to overwrite existing data in database. We want only to add missing journals, but not update existing ones.
                            }

                            Program.JournalsDatabase.Save();

                        }
                    }
                    catch (Exception ex)
                    {
                        lblStatus.Text = $"Journal {missingJournal} failed to fetch, reason: {ex.Message}";
                        notFoundJournals.Add(missingJournal);
                        continue;
                    }
                }
                if (notFoundJournals.Count > 0)
                {
                    lblStatus.Text = $"Some journals wasnt found, I found:{missingJournals.Count - notFoundJournals.Count}/{missingJournals.Count}... More informations in logs.";
                }
                else lblStatus.Text = "Journal database updated.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }


        private void createExtraJCRTagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var journalReports = Program.JournalsDatabase.Data.JournalReports;
            Dictionary<string, JournalReportsDto> dic = journalReports.ToDictionary(rep => rep.Journal.Name.ToLower());
            foreach (var entry in entries)
            {
                if (entry.Type == "article")
                {
                    string journalValue = entry.GetTagValue("journal");
                    if (string.IsNullOrWhiteSpace(journalValue))
                        continue;

                    string journalName = BibtexUtils.RemoveLatex(journalValue).ToLower();

                    if (dic.ContainsKey(journalName))
                    {
                        var report = dic[journalName];

                        // Calc average percentile JIF
                        double percentile = 0;
                        foreach (var jif in report.Ranks.Jif)
                        {
                            percentile += jif.JifPercentile;
                        }
                        percentile = percentile / report.Ranks.Jif.Count;
                        // Set quartile from percentile
                        string quartile = percentile >= 75 ? "Q1" : percentile >= 50 ? "Q2" : percentile >= 25 ? "Q3" : "Q4";

                        string percentileText = Math.Round(percentile, 1).ToString(CultureInfo.InvariantCulture);
                        SetSingleTagValue(entry, "jif", percentileText);
                        SetSingleTagValue(entry, "jif_" + report.Year.ToString(), percentileText);
                        SetSingleTagValue(entry, "jif_Q", quartile);
                    }
                }
            }
            // Refresh table 
            LoadData(entries.ToArray());
            Changed();
        }

        private void removeDuplicateTagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int changedEntries = 0;
            int removedTags = 0;

            foreach (var entry in entries)
            {
                int removedForEntry = RemoveDuplicateTags(entry);
                if (removedForEntry > 0)
                {
                    changedEntries++;
                    removedTags += removedForEntry;
                }
            }

            LoadData(entries.ToArray(), txtSearch.Text);

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

        private void SetSingleTagValue(BibtexEntry entry, string key, string value)
        {
            if (entry == null || string.IsNullOrWhiteSpace(key))
                return;

            var tags = (entry.Tags ?? Array.Empty<BibtexTag>()).ToList();
            var updatedTags = new List<BibtexTag>();
            bool updated = false;

            foreach (var tag in tags)
            {
                if (tag == null)
                    continue;

                if (string.Equals(tag.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    if (updated)
                        continue;

                    tag.Value = value;
                    updatedTags.Add(tag);
                    updated = true;
                    continue;
                }

                updatedTags.Add(tag);
            }

            if (!updated)
            {
                updatedTags.Add(new BibtexTag(key, value));
            }

            entry.Tags = updatedTags.ToArray();
        }

        private string GetTagValueIgnoreCase(BibtexEntry entry, string key)
        {
            if (entry?.Tags == null || string.IsNullOrWhiteSpace(key))
                return null;

            foreach (var tag in entry.Tags)
            {
                if (tag != null && string.Equals(tag.Key, key, StringComparison.OrdinalIgnoreCase))
                    return tag.Value;
            }

            return null;
        }

        private void RemoveAllTagsByKey(BibtexEntry entry, string key)
        {
            if (entry?.Tags == null || string.IsNullOrWhiteSpace(key))
                return;

            entry.Tags = entry.Tags
                .Where(tag => tag != null && string.Equals(tag.Key, key, StringComparison.OrdinalIgnoreCase) == false)
                .ToArray();
        }

        private int RemoveDuplicateTags(BibtexEntry entry)
        {
            if (entry?.Tags == null || entry.Tags.Length <= 1)
                return 0;

            var uniqueTags = new List<BibtexTag>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = entry.Tags.Length - 1; i >= 0; i--)
            {
                var tag = entry.Tags[i];
                if (tag == null)
                    continue;

                string tagKey = tag.Key ?? string.Empty;
                if (seenKeys.Add(tagKey))
                {
                    uniqueTags.Insert(0, tag);
                }
            }

            int removed = entry.Tags.Length - uniqueTags.Count;
            if (removed > 0)
            {
                entry.Tags = uniqueTags.ToArray();
            }

            return removed;
        }

        private async void loadBibTexFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog()
                {
                    CheckPathExists = true,
                    CheckFileExists = true,
                    Filter = "Bibtex database *.bib|*.bib"
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    string fileName = ofd.FileName;
                    await Task.Run(() =>
                    {                        
                        BibtexParser parser = new BibtexParser();
                        entries.AddRange(parser.ParseFile(File.ReadAllText(fileName)));
                    });
                    LoadData(entries.ToArray());
                    Changed();
                }
                lblStatus.Text = "Loaded.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private void removeQ3Q4ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<BibtexEntry> list = new List<BibtexEntry>();
            foreach (var entry in entries)
            {
                string jif = entry.GetTagValue("jif");
                if (jif == null) continue;
                double jifVal = double.Parse(jif, CultureInfo.InvariantCulture);
                if (jifVal >= 50)
                {
                    list.Add(entry);
                }

            }
            entries = list;
            LoadData(entries.ToArray());
            Changed();
        }

        private void databaseToolStripMenuItem_Click(object sender, EventArgs e)
        {

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
                var toExlcude = new List<BibtexEntry>();
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    string fileName = ofd.FileName;
                    await Task.Run(() =>
                    {
                        
                        BibtexParser parser = new BibtexParser();
                        toExlcude.AddRange(parser.ParseFile(File.ReadAllText(fileName)));
                    });
                   
                }
                entries = BibtexUtils.ExcludeEntries(entries, toExlcude);
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
            var frm = InputBoxForm.Show("Enter a pattern to filter items:", this);
            if (frm.DialogResult == DialogResult.OK) {
                string[] patterns = frm.GetText().Split(',');
                foreach (string item in patterns)
                {
                    string pattern = item.Trim();
                    pattern = pattern.ToLower();
                    List<BibtexEntry> filtered = new List<BibtexEntry>();
                    foreach (BibtexEntry entry in entries)
                    {
                        if (entry.GetTagValue("title").ToLower().Contains(pattern) == false)
                        {
                            filtered.Add(entry);
                        }
                    }
                    entries = filtered;
                }                
                LoadData(entries.ToArray());
                Changed();
            }
        }

        private void columnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new EditColumnsForm();
            frm.SetColumns(Program.AppSettings.Data.Columns);

            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                Program.AppSettings.Data.Columns = frm.GetColumns();
                LoadData(entries.ToArray());
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            LoadData(entries.ToArray(), txtSearch.Text);
        }

        private void txtSearch_Click(object sender, EventArgs e)
        {
            
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

        private void recordToolStripMenuItem_Click(object sender, EventArgs e)
        {

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
            RemoveSelecedRecords();
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
            if (entry == null)
                return null;

            string storedPdf = FindStoredPdfFile(entry);
            if (string.IsNullOrWhiteSpace(storedPdf) == false)
                return storedPdf;

            pdfFiles = pdfFiles ?? GetPdfFiles();
            if (pdfFiles.Length == 0)
                return null;

            return FindDirectPdfMatch(entry, pdfFiles);
        }

        private string FindStoredPdfFile(BibtexEntry entry)
        {
            string storedValue = GetTagValueIgnoreCase(entry, "pdf_file");
            if (string.IsNullOrWhiteSpace(storedValue))
                return null;

            string candidatePath = storedValue;
            if (!Path.IsPathRooted(candidatePath))
            {
                string pdfFolder = Program.AppSettings.Data.PdfFolder;
                if (string.IsNullOrWhiteSpace(pdfFolder))
                    return null;

                candidatePath = Path.Combine(pdfFolder, storedValue);
            }

            candidatePath = Path.GetFullPath(candidatePath);
            return File.Exists(candidatePath) ? candidatePath : null;
        }

        private string FindDirectPdfMatch(BibtexEntry entry, IEnumerable<string> pdfFiles)
        {
            var files = (pdfFiles ?? Enumerable.Empty<string>()).ToArray();
            if (files.Length == 0)
                return null;

            string key = (entry.Key ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                var keyMatch = files.FirstOrDefault(file =>
                    Path.GetFileNameWithoutExtension(file)
                        .IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);

                if (!string.IsNullOrWhiteSpace(keyMatch))
                    return keyMatch;
            }

            string titleLower = GetNormalizedTitleForExactMatch(entry);
            if (!string.IsNullOrWhiteSpace(titleLower))
            {
                var titleMatch = files.FirstOrDefault(file =>
                    string.Equals(Path.GetFileNameWithoutExtension(file).Trim().ToLowerInvariant(), titleLower, StringComparison.Ordinal));

                if (!string.IsNullOrWhiteSpace(titleMatch))
                    return titleMatch;
            }

            return null;
        }

        private string[] GetPdfFiles()
        {
            string pdfFolder = Program.AppSettings.Data.PdfFolder;
            if (string.IsNullOrWhiteSpace(pdfFolder) || Directory.Exists(pdfFolder) == false)
                return new string[0];

            var searchOption = Program.AppSettings.Data.RecursivePdfSearch
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            return Directory
                .GetFiles(pdfFolder, "*.pdf", searchOption)
                .Where(file => IsPdfFileAllowed(file, pdfFolder))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private bool IsPdfFileAllowed(string filePath, string rootPdfFolder)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string rootFullPath = Path.GetFullPath(rootPdfFolder);
            var directory = new DirectoryInfo(Path.GetDirectoryName(filePath) ?? rootFullPath);

            while (directory != null && string.Equals(directory.FullName, rootFullPath, StringComparison.OrdinalIgnoreCase) == false)
            {
                if (IsIgnoredPdfFolderName(directory.Name))
                    return false;

                directory = directory.Parent;
            }

            return true;
        }

        private bool IsIgnoredPdfFolderName(string folderName)
        {
            return string.IsNullOrWhiteSpace(folderName) == false &&
                folderName.StartsWith("__", StringComparison.Ordinal) &&
                folderName.EndsWith("__", StringComparison.Ordinal);
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

        private string GetNormalizedTitleForExactMatch(BibtexEntry entry)
        {
            string title = GetTagValueIgnoreCase(entry, "title");
            if (string.IsNullOrWhiteSpace(title))
                return null;

            return BibtexUtils.RemoveLatex(title).Trim().ToLowerInvariant();
        }

        private string GetStandardizedTitle(BibtexEntry entry)
        {
            string title = GetTagValueIgnoreCase(entry, "title");
            return StandardizeText(BibtexUtils.RemoveLatex(title ?? string.Empty));
        }

        private PdfArchiveItem BuildPdfArchiveItem(string filePath)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            string standardized = StandardizeText(fileNameWithoutExtension);

            return new PdfArchiveItem
            {
                FilePath = filePath,
                FileNameWithoutExtension = fileNameWithoutExtension,
                StandardizedName = standardized,
                Tokens = Tokenize(standardized),
                Keywords = ExtractKeywords(standardized)
            };
        }

        private string StandardizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = RemoveDiacritics(value).ToLowerInvariant();
            normalized = normalized.Replace('_', ' ').Replace('-', ' ');
            normalized = Regex.Replace(normalized, @"[^a-z0-9\s]", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }

        private string RemoveDiacritics(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    builder.Append(c);
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private Dictionary<string, int> Tokenize(string value)
        {
            var tokens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in (value ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (tokens.ContainsKey(token))
                    tokens[token]++;
                else
                    tokens[token] = 1;
            }

            return tokens;
        }

        private HashSet<string> ExtractKeywords(string standardizedText)
        {
            return new HashSet<string>(
                (standardizedText ?? string.Empty)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(token => token.Length >= 4),
                StringComparer.OrdinalIgnoreCase);
        }

        private double ComputeCosineSimilarity(Dictionary<string, int> left, Dictionary<string, int> right)
        {
            if (left == null || right == null || left.Count == 0 || right.Count == 0)
                return 0d;

            double dot = 0d;
            foreach (var pair in left)
            {
                if (right.TryGetValue(pair.Key, out int rightValue))
                {
                    dot += pair.Value * rightValue;
                }
            }

            double leftNorm = Math.Sqrt(left.Values.Sum(v => v * v));
            double rightNorm = Math.Sqrt(right.Values.Sum(v => v * v));
            if (leftNorm == 0d || rightNorm == 0d)
                return 0d;

            return dot / (leftNorm * rightNorm);
        }

        private double ComputeKeywordScore(HashSet<string> left, HashSet<string> right)
        {
            if (left == null || right == null || left.Count == 0 || right.Count == 0)
                return 0d;

            int intersection = left.Count(token => right.Contains(token));
            int union = left.Count + right.Count - intersection;
            if (union == 0)
                return 0d;

            return (double)intersection / union;
        }

        private double ComputeSimilarityScore(BibtexEntry entry, PdfArchiveItem pdf)
        {
            string standardizedTitle = GetStandardizedTitle(entry);
            if (string.IsNullOrWhiteSpace(standardizedTitle) || pdf == null || string.IsNullOrWhiteSpace(pdf.StandardizedName))
                return 0d;

            if (string.Equals(standardizedTitle, pdf.StandardizedName, StringComparison.Ordinal))
                return 1d;

            var titleTokens = Tokenize(standardizedTitle);
            var titleKeywords = ExtractKeywords(standardizedTitle);
            double cosine = ComputeCosineSimilarity(titleTokens, pdf.Tokens);
            double keywordScore = ComputeKeywordScore(titleKeywords, pdf.Keywords);

            return Math.Min(1d, (cosine * 0.9d) + (keywordScore * 0.1d));
        }

        private void AssignPdfToEntry(BibtexEntry entry, string pdfFilePath)
        {
            if (entry == null || string.IsNullOrWhiteSpace(pdfFilePath))
                return;

            SetSingleTagValue(entry, "pdf_file", GetPdfStorageValue(pdfFilePath));
            SetSingleTagValue(entry, "has_pdf", "yes");
        }

        private string GetPdfStorageValue(string pdfFilePath)
        {
            string pdfFolder = Program.AppSettings.Data.PdfFolder;
            if (string.IsNullOrWhiteSpace(pdfFolder))
                return pdfFilePath;

            try
            {
                string folderFullPath = EnsureTrailingSeparator(Path.GetFullPath(pdfFolder));
                string fileFullPath = Path.GetFullPath(pdfFilePath);

                if (fileFullPath.StartsWith(folderFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    Uri folderUri = new Uri(folderFullPath);
                    Uri fileUri = new Uri(fileFullPath);
                    return Uri.UnescapeDataString(folderUri.MakeRelativeUri(fileUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
                }
            }
            catch
            {
            }

            return pdfFilePath;
        }

        private string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()) && !path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                return path + Path.DirectorySeparatorChar;

            return path;
        }

        private void UpdateHasPdfTag(BibtexEntry entry, bool hasPdf)
        {
            SetSingleTagValue(entry, "has_pdf", hasPdf ? "yes" : "no");
        }

        private void OpenPdf(BibtexEntry entry)
        {
            if (entry == null || entry.Tags == null)
                return;
                       
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = GetPdfFileName(entry),
                UseShellExecute = true
            });
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
                            OpenPdf(entry);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            entries = new List<BibtexEntry>();
            LoadData(entries.ToArray());
            Changed();
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
            try
            {
                lblStatus.Text = $"Auto-pairing PDFs using {GetConfiguredThreadCount()} thread(s)...";

                int directMatches = 0;
                int smartMatches = 0;
                int unmatched = 0;
                bool noPdfsFound = false;

                await Task.Run(() =>
                {
                    string[] pdfFiles = GetPdfFiles();
                    if (pdfFiles.Length == 0)
                    {
                        foreach (var entry in entries)
                        {
                            RemoveAllTagsByKey(entry, "pdf_file");
                            UpdateHasPdfTag(entry, false);
                        }

                        noPdfsFound = true;
                        return;
                    }

                    double threshold = Math.Max(0d, Math.Min(100d, Program.AppSettings.Data.PdfAutoPairThresholdPercent)) / 100d;
                    var assignedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var pairedEntries = new HashSet<BibtexEntry>();

                    foreach (var entry in entries)
                    {
                        string storedPdf = FindStoredPdfFile(entry);
                        if (string.IsNullOrWhiteSpace(storedPdf))
                        {
                            RemoveAllTagsByKey(entry, "pdf_file");
                            continue;
                        }

                        if (assignedFiles.Add(storedPdf))
                        {
                            AssignPdfToEntry(entry, storedPdf);
                            pairedEntries.Add(entry);
                            directMatches++;
                        }
                        else
                        {
                            RemoveAllTagsByKey(entry, "pdf_file");
                        }
                    }

                    foreach (var entry in entries.Where(item => pairedEntries.Contains(item) == false))
                    {
                        string directMatch = FindDirectPdfMatch(entry, pdfFiles.Where(file => assignedFiles.Contains(file) == false));
                        if (string.IsNullOrWhiteSpace(directMatch))
                            continue;

                        AssignPdfToEntry(entry, directMatch);
                        assignedFiles.Add(directMatch);
                        pairedEntries.Add(entry);
                        directMatches++;
                    }

                    var remainingEntries = entries.Where(item => pairedEntries.Contains(item) == false).ToList();
                    var remainingPdfPaths = pdfFiles
                        .Where(file => assignedFiles.Contains(file) == false)
                        .ToArray();

                    var remainingPdfsBag = new ConcurrentBag<PdfArchiveItem>();
                    Parallel.ForEach(remainingPdfPaths, CreateParallelOptions(), file =>
                    {
                        remainingPdfsBag.Add(BuildPdfArchiveItem(file));
                    });

                    var remainingPdfs = remainingPdfsBag
                        .OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var candidates = new ConcurrentBag<PdfSimilarityCandidate>();
                    Parallel.ForEach(remainingEntries, CreateParallelOptions(), entry =>
                    {
                        foreach (var pdf in remainingPdfs)
                        {
                            double score = ComputeSimilarityScore(entry, pdf);
                            if (score >= threshold)
                            {
                                candidates.Add(new PdfSimilarityCandidate
                                {
                                    Entry = entry,
                                    Pdf = pdf,
                                    Score = score
                                });
                            }
                        }
                    });

                    foreach (var candidate in candidates.OrderByDescending(item => item.Score).ThenBy(item => item.Pdf.FilePath, StringComparer.OrdinalIgnoreCase))
                    {
                        if (pairedEntries.Contains(candidate.Entry) || assignedFiles.Contains(candidate.Pdf.FilePath))
                            continue;

                        AssignPdfToEntry(candidate.Entry, candidate.Pdf.FilePath);
                        assignedFiles.Add(candidate.Pdf.FilePath);
                        pairedEntries.Add(candidate.Entry);
                        smartMatches++;
                    }

                    foreach (var entry in entries)
                    {
                        bool hasPdf = pairedEntries.Contains(entry);
                        if (hasPdf == false)
                        {
                            RemoveAllTagsByKey(entry, "pdf_file");
                        }

                        UpdateHasPdfTag(entry, hasPdf);
                    }

                    unmatched = entries.Count - pairedEntries.Count;
                });

                LoadData(entries.ToArray(), txtSearch.Text);
                Changed();

                if (noPdfsFound)
                {
                    lblStatus.Text = "No PDFs found in Pdf Folder.";
                    return;
                }

                lblStatus.Text = $"Auto-pair finished using {GetConfiguredThreadCount()} thread(s). Direct: {directMatches}, smart: {smartMatches}, unmatched: {unmatched}.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
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

                using (var form = new ExportPdfsForm(Program.AppSettings.Data.PdfFolder, dataGridView1.SelectedRows.Count > 0))
                {
                    if (form.ShowDialog(this) != DialogResult.OK)
                        return;

                    var toExport = form.ExportSelectedOnly ? GetSelected() : entries.ToArray();
                    if (toExport.Length == 0)
                    {
                        lblStatus.Text = "No records selected for export.";
                        return;
                    }

                    Directory.CreateDirectory(form.OutputDirectory);

                    int exported = 0;
                    int skipped = 0;
                    int injected = 0;

                    foreach (var entry in toExport)
                    {
                        string sourcePdf;
                        try
                        {
                            sourcePdf = GetPdfFileName(entry);
                        }
                        catch
                        {
                            skipped++;
                            continue;
                        }

                        string baseFileName = BuildExportPdfBaseName(entry, form.FileNameMode, form.CustomPattern);
                        string destination = BuildExportDestinationPath(form.OutputDirectory, baseFileName, sourcePdf);
                        bool isSameFile = string.Equals(
                            Path.GetFullPath(sourcePdf),
                            Path.GetFullPath(destination),
                            StringComparison.OrdinalIgnoreCase);

                        if (!isSameFile)
                        {
                            File.Copy(sourcePdf, destination, false);
                        }

                        string doi = GetTagValueIgnoreCase(entry, "doi");
                        if (form.InjectDoiMetadata && string.IsNullOrWhiteSpace(doi) == false)
                        {
                            InjectDoiIntoPdfMetadata(destination, doi);
                            injected++;
                        }

                        exported++;
                    }

                    lblStatus.Text = $"Exported {exported} PDF(s), skipped {skipped}, DOI injected into {injected}.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private string BuildExportPdfBaseName(BibtexEntry entry, PdfExportFileNameMode mode, string customPattern)
        {
            string pattern;
            switch (mode)
            {
                case PdfExportFileNameMode.Key:
                    pattern = "<key>";
                    break;
                case PdfExportFileNameMode.Custom:
                    pattern = string.IsNullOrWhiteSpace(customPattern) ? "<key>" : customPattern;
                    break;
                default:
                    pattern = "<key>_<title>";
                    break;
            }

            string rendered = Regex.Replace(pattern, @"<(?<tag>[^>]+)>", match =>
            {
                string tagName = match.Groups["tag"].Value.Trim();
                string value;
                if (string.Equals(tagName, "key", StringComparison.OrdinalIgnoreCase))
                    value = entry.Key;
                else if (string.Equals(tagName, "type", StringComparison.OrdinalIgnoreCase))
                    value = entry.Type;
                else
                    value = GetTagValueIgnoreCase(entry, tagName);

                return SanitizeFileNamePart(BibtexUtils.RemoveLatex(value ?? string.Empty));
            });

            rendered = Regex.Replace(rendered, @"\s+", " ").Trim();
            rendered = rendered.Trim(' ', '.', '_', '-');

            if (string.IsNullOrWhiteSpace(rendered))
                rendered = SanitizeFileNamePart(entry.Key);

            if (string.IsNullOrWhiteSpace(rendered))
                rendered = "record";

            return rendered;
        }

        private string SanitizeFileNamePart(string value)
        {
            string sanitized = value ?? string.Empty;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();
            return sanitized;
        }

        private string BuildExportDestinationPath(string outputDirectory, string baseFileName, string sourcePdfPath)
        {
            string destination = Path.Combine(outputDirectory, baseFileName + ".pdf");
            string sourceFullPath = Path.GetFullPath(sourcePdfPath);

            if (string.Equals(Path.GetFullPath(destination), sourceFullPath, StringComparison.OrdinalIgnoreCase))
                return destination;

            if (File.Exists(destination) == false)
                return destination;

            for (int index = 2; ; index++)
            {
                string candidate = Path.Combine(outputDirectory, $"{baseFileName}_{index}.pdf");
                if (File.Exists(candidate) == false)
                    return candidate;
            }
        }

        private void InjectDoiIntoPdfMetadata(string fileName, string doi)
        {
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(doi))
                return;

            byte[] originalBytes = File.ReadAllBytes(fileName);
            Encoding encoding = Encoding.GetEncoding("ISO-8859-1");
            string pdfText = encoding.GetString(originalBytes);

            Match trailerMatch = Regex.Matches(pdfText, @"trailer\s*<<(?<dict>.*?)>>\s*startxref\s*(?<xref>\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                .Cast<Match>()
                .LastOrDefault();
            if (trailerMatch == null)
                return;

            string trailerDict = trailerMatch.Groups["dict"].Value;
            int prevXref = int.Parse(trailerMatch.Groups["xref"].Value, CultureInfo.InvariantCulture);

            Match rootMatch = Regex.Match(trailerDict, @"/Root\s+(?<root>\d+\s+\d+\s+R)", RegexOptions.IgnoreCase);
            Match sizeMatch = Regex.Match(trailerDict, @"/Size\s+(?<size>\d+)", RegexOptions.IgnoreCase);
            Match infoMatch = Regex.Match(trailerDict, @"/Info\s+(?<info>\d+)\s+\d+\s+R", RegexOptions.IgnoreCase);
            Match idMatch = Regex.Match(trailerDict, @"/ID\s*\[[^\]]+\]", RegexOptions.IgnoreCase);

            if (!rootMatch.Success || !sizeMatch.Success)
                return;

            int newObjectNumber = int.Parse(sizeMatch.Groups["size"].Value, CultureInfo.InvariantCulture);
            string existingInfoBody = string.Empty;
            if (infoMatch.Success)
            {
                int infoObjectNumber = int.Parse(infoMatch.Groups["info"].Value, CultureInfo.InvariantCulture);
                Match infoObjectMatch = Regex.Match(pdfText, $@"(?s)\b{infoObjectNumber}\s+0\s+obj\s*<<(.*?)>>\s*endobj");
                if (infoObjectMatch.Success)
                {
                    existingInfoBody = infoObjectMatch.Groups[1].Value;
                }
            }

            existingInfoBody = Regex.Replace(existingInfoBody, @"(?is)/DOI\s*(\((?:\\.|[^\\)])*\)|<[^>]*>)", string.Empty).Trim();

            string escapedDoi = EscapePdfLiteralString(doi.Trim());
            string infoObject = $"{newObjectNumber} 0 obj\n<<\n{existingInfoBody}\n/DOI ({escapedDoi})\n>>\nendobj\n";
            int objectOffset = originalBytes.Length;
            string xref = $"xref\n{newObjectNumber} 1\n{objectOffset:D10} 00000 n \n";
            int xrefOffset = objectOffset + encoding.GetByteCount(infoObject);

            string trailer = $"trailer\n<< /Size {newObjectNumber + 1} /Root {rootMatch.Groups["root"].Value} /Info {newObjectNumber} 0 R";
            if (idMatch.Success)
            {
                trailer += " " + idMatch.Value;
            }

            trailer += $" /Prev {prevXref} >>\nstartxref\n{xrefOffset}\n%%EOF";

            byte[] appendedBytes = encoding.GetBytes(infoObject + xref + trailer);
            File.WriteAllBytes(fileName, originalBytes.Concat(appendedBytes).ToArray());
        }

        private string EscapePdfLiteralString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
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


