
using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.JCR;
using ScientificReviews.JCR.Dto;
using System;
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

            pdfFiles = pdfFiles ?? GetPdfFiles();
            if (pdfFiles.Length == 0)
                return null;

            string key = (entry.Key ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                var keyMatch = pdfFiles.FirstOrDefault(file =>
                    Path.GetFileNameWithoutExtension(file)
                        .IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);

                if (!string.IsNullOrWhiteSpace(keyMatch))
                    return keyMatch;
            }

            string titleLower = ((entry.GetTagValue("title") ?? string.Empty).Trim()).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(titleLower))
            {
                var titleMatch = pdfFiles.FirstOrDefault(file =>
                    string.Equals(Path.GetFileNameWithoutExtension(file), titleLower, StringComparison.Ordinal));

                if (!string.IsNullOrWhiteSpace(titleMatch))
                    return titleMatch;
            }

            string entryDoi = NormalizeDoi(entry.GetTagValue("doi"));
            if (!string.IsNullOrWhiteSpace(entryDoi))
            {
                foreach (var file in pdfFiles)
                {
                    if (PdfContainsMatchingDoiMetadata(file, entryDoi))
                        return file;
                }
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
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private bool PdfContainsMatchingDoiMetadata(string fileName, string entryDoi)
        {
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(entryDoi))
                return false;

            try
            {
                foreach (var doi in ExtractPdfMetadataDois(fileName))
                {
                    if (string.Equals(NormalizeDoi(doi), entryDoi, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private IEnumerable<string> ExtractPdfMetadataDois(string fileName)
        {
            byte[] data = File.ReadAllBytes(fileName);
            string pdfText = Encoding.GetEncoding("ISO-8859-1").GetString(data);

            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddRegexMatches(results, pdfText, @"(?is)<prism:doi>\s*(?<doi>.*?)\s*</prism:doi>");
            AddRegexMatches(results, pdfText, @"(?is)<pdfx:doi>\s*(?<doi>.*?)\s*</pdfx:doi>");
            AddRegexMatches(results, pdfText, @"(?is)<dc:identifier>\s*(?:doi:\s*)?(?<doi>10\.\d{4,9}/[^<\s]+)\s*</dc:identifier>");
            AddRegexMatches(results, pdfText, @"(?is)/DOI\s*\((?<doi>10\.\d{4,9}/[^)]*)\)");

            foreach (Match match in Regex.Matches(pdfText, @"(?is)/DOI\s*<(?<hex>[0-9A-F]+)>"))
            {
                string decoded = DecodePdfHexString(match.Groups["hex"].Value);
                string normalized = NormalizeDoi(decoded);
                if (!string.IsNullOrWhiteSpace(normalized))
                    results.Add(normalized);
            }

            return results;
        }

        private void AddRegexMatches(HashSet<string> results, string pdfText, string pattern)
        {
            foreach (Match match in Regex.Matches(pdfText, pattern))
            {
                string normalized = NormalizeDoi(match.Groups["doi"].Value);
                if (!string.IsNullOrWhiteSpace(normalized))
                    results.Add(normalized);
            }
        }

        private string DecodePdfHexString(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return null;

            hex = Regex.Replace(hex, @"\s+", string.Empty);
            if (hex.Length % 2 == 1)
                hex += "0";

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
        }

        private string NormalizeDoi(string doi)
        {
            if (string.IsNullOrWhiteSpace(doi))
                return null;

            string normalized = doi.Trim();
            normalized = Regex.Replace(normalized, @"^https?://(dx\.)?doi\.org/", string.Empty, RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"^doi:\s*", string.Empty, RegexOptions.IgnoreCase);
            normalized = normalized.Trim().TrimEnd('/', '.', ',', ';');

            return normalized.ToLowerInvariant();
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

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var entry in entries)
            {
                if (entry == null || entry.Tags == null || string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                string filename = FindPdfFile(entry);
                if (string.IsNullOrWhiteSpace(filename))
                    continue;

                string filename2 = Path.Combine(Program.AppSettings.Data.PdfFolder, entry.Key + ".pdf");
                if (string.Equals(filename, filename2, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (File.Exists(filename2))
                    continue;

                File.Move(filename, filename2);

            }
        }

        private void checkPdfToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var pdfFiles = GetPdfFiles();
            foreach (var entry in entries)
            {

                if (entry == null || entry.Tags == null)
                    continue;

                var haspdfTag = entry.Tags
               .FirstOrDefault(t => string.Equals(t.Key, "has_pdf", StringComparison.OrdinalIgnoreCase));

                if (haspdfTag == null)
                {
                    haspdfTag = new BibtexTag()
                    {
                        Key = "has_pdf"
                    };
                    var list = entry.Tags.ToList();
                    list.Add(haspdfTag);
                    entry.Tags = list.ToArray();
                }


                if (string.IsNullOrWhiteSpace(FindPdfFile(entry, pdfFiles)))
                {
                    haspdfTag.Value = "no";
                }
                else
                {
                    haspdfTag.Value = "yes";
                }

            }
            LoadData(entries.ToArray());
        }

        private void exportSelectedPDFToolStripMenuItem_Click(object sender, EventArgs e)
        {      
            try
            {
                var toExport = GetSelected();
                if (toExport.Length == 0)
                    return;
                lblStatus.Text = "Exporting...";
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog()
                {
                    SelectedPath = Program.AppSettings.Data.LastDirectory
                })
                {
                    DialogResult result = folderDialog.ShowDialog(this);
                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                    {
                        foreach (var entry in toExport)
                        {
                            try
                            {
                                string fileName = GetPdfFileName(entry);
                                string dest = Path.Combine(folderDialog.SelectedPath, entry.Key + ".pdf");
                                File.Copy(fileName, dest);
                            }
                            catch { continue; }
                        }
                    }
                }
                lblStatus.Text = "Export PDF done.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
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


