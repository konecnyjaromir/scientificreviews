using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private void InitializeRecordContextMenu()
        {
            _contextEditMenuItem = new ToolStripMenuItem("Edit");
            _contextEditMenuItem.Click += (sender, e) => allowEditToolStripMenuItem_Click(sender, e);

            _contextCopyMenuItem = new ToolStripMenuItem("Copy");
            _contextCopyMenuItem.ShortcutKeyDisplayString = "Ctrl+C";
            _contextCopyMenuItem.Click += (sender, e) => CopySelectedRecordsToClipboard();

            _contextCutMenuItem = new ToolStripMenuItem("Cut");
            _contextCutMenuItem.ShortcutKeyDisplayString = "Ctrl+X";
            _contextCutMenuItem.Click += (sender, e) => CutSelectedRecordsToClipboard();

            _contextPasteMenuItem = new ToolStripMenuItem("Paste");
            _contextPasteMenuItem.ShortcutKeyDisplayString = "Ctrl+V";
            _contextPasteMenuItem.Click += (sender, e) => PasteRecordsFromClipboard();

            _contextDuplicateMenuItem = new ToolStripMenuItem("Duplicate");
            _contextDuplicateMenuItem.ShortcutKeyDisplayString = "Ctrl+D";
            _contextDuplicateMenuItem.Click += (sender, e) => DuplicateSelectedRecords();

            _recordContextMenu = new ContextMenuStrip();
            _recordContextMenu.Items.AddRange(new ToolStripItem[]
            {
                _contextEditMenuItem,
                new ToolStripSeparator(),
                _contextCopyMenuItem,
                _contextCutMenuItem,
                _contextPasteMenuItem,
                _contextDuplicateMenuItem
            });
            _recordContextMenu.Opening += recordContextMenu_Opening;

            _contextRefreshMenuItem = new ToolStripMenuItem("Refresh");
            _contextRefreshMenuItem.Click += (sender, e) => RefreshGrid();

            _gridBackgroundContextMenu = new ContextMenuStrip();
            _gridBackgroundContextMenu.Items.Add(_contextRefreshMenuItem);
        }

        private void recordContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool hasSelection = GetSelectedOrdered().Length > 0;

            _contextEditMenuItem.Checked = allowEditToolStripMenuItem.Checked;
            _contextCopyMenuItem.Enabled = hasSelection;
            _contextCutMenuItem.Enabled = hasSelection;
            _contextPasteMenuItem.Enabled = Clipboard.ContainsText() && string.IsNullOrWhiteSpace(Clipboard.GetText()) == false;
            _contextDuplicateMenuItem.Enabled = hasSelection;
        }

        private void dataGridView1_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0)
                return;

            if (e.RowIndex >= dataGridView1.Rows.Count)
                return;

            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
            if (row.Selected == false)
            {
                dataGridView1.ClearSelection();
                row.Selected = true;
            }

            if (e.ColumnIndex >= 0)
                dataGridView1.CurrentCell = row.Cells[e.ColumnIndex];

            Rectangle cellBounds = dataGridView1.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            Point menuLocation = new Point(cellBounds.Left + e.X, cellBounds.Top + e.Y);
            _recordContextMenu?.Show(dataGridView1, menuLocation);
        }

        private void dataGridView1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            DataGridView.HitTestInfo hitTest = dataGridView1.HitTest(e.X, e.Y);
            if (hitTest.RowIndex >= 0)
                return;

            _gridBackgroundContextMenu?.Show(dataGridView1, new Point(e.X, e.Y));
        }

        private void dataGridView1_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.RowIndex < 0)
                return;

            BibtexEntry entry = GetEntryFromRowIndex(e.RowIndex);
            if (entry == null)
                return;

            try
            {
                OpenPdfOrPromptManualPair(entry);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private BibtexEntry GetEntryFromRowIndex(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dataGridView1.Rows.Count)
                return null;

            DataGridViewRow row = dataGridView1.Rows[rowIndex];
            if (row?.DataBoundItem is DataRowView drv && drv.Row != null)
                return drv.Row["Entry"] as BibtexEntry;

            return null;
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

            if (e.Control && e.KeyCode == Keys.D)
            {
                DuplicateSelectedRecords();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                RemoveSelectedRecords();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void RemoveSelectedRecords()
        {
            if (dataGridView1.SelectedRows.Count <= 0)
                return;

            int currentIndex = bindingSource1.Position;

            foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(r => r.Index))
            {
                if (dgvr.DataBoundItem is DataRowView drv)
                {
                    DataRow row = drv.Row;
                    BibtexEntry entry = row["Entry"] as BibtexEntry;
                    if (entry != null)
                    {
                        entries.Remove(entry);
                        visibleEntries.Remove(entry);
                    }
                }
            }

            LoadData(visibleEntries.ToArray());
            Changed();

            if (currentIndex >= bindingSource1.Count)
                currentIndex = bindingSource1.Count - 1;

            if (currentIndex >= 0)
                bindingSource1.Position = currentIndex;
        }

        private bool CopySelectedRecordsToClipboard()
        {
            try
            {
                BibtexEntry[] selectedEntries = GetSelectedOrdered();
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
            int selectedCount = GetSelectedOrdered().Length;
            if (selectedCount == 0)
            {
                lblStatus.Text = "No records selected.";
                return;
            }

            try
            {
                if (CopySelectedRecordsToClipboard() == false)
                    return;

                RemoveSelectedRecords();
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
                if (Clipboard.ContainsText() == false)
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
                BibtexEntry[] pastedEntries = parser.ParseFile(clipboardText);
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

        private void duplicateRecordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DuplicateSelectedRecords();
        }

        private void DuplicateSelectedRecords()
        {
            try
            {
                BibtexEntry[] selectedEntries = GetSelectedOrdered();
                if (selectedEntries.Length == 0)
                {
                    lblStatus.Text = "No records selected.";
                    return;
                }

                BibtexEntry[] duplicates = selectedEntries
                    .Select(entry => entry?.DeepClone())
                    .Where(entry => entry != null)
                    .ToArray();

                if (duplicates.Length == 0)
                {
                    lblStatus.Text = "No records duplicated.";
                    return;
                }

                entries.AddRange(duplicates);
                LoadData(entries.ToArray(), txtSearch.Text);
                SelectEntriesInGrid(duplicates);
                Changed();
                lblStatus.Text = $"Duplicated {duplicates.Length} record(s).";
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
                    BibtexEntry entry = drv.Row["Entry"] as BibtexEntry;
                    if (entry != null && selectedSet.Contains(entry))
                        matchingRows.Add(row);
                }
            }

            if (matchingRows.Count == 0)
                return;

            dataGridView1.ClearSelection();
            foreach (DataGridViewRow row in matchingRows)
            {
                row.Selected = true;
            }

            if (matchingRows[0].Cells.Count > 0)
                dataGridView1.CurrentCell = matchingRows[0].Cells[0];
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            SelectEntry();
        }

        private void SelectEntry()
        {
            if (bindingSource1.Current is DataRowView == false)
                return;

            bool readOnly = allowEditToolStripMenuItem.Checked == false;
            DataRowView drv = (DataRowView)bindingSource1.Current;
            if (drv.Row != null)
            {
                BibtexEntry entry = (BibtexEntry)drv.Row["Entry"];
                ShowEntry(entry, txtSearch.Text);

                CustomClass customClass = new CustomClass();
                customClass.Add(new CustomProperty("entryKey", "Key", entry.Key, "Bibitem", readOnly, true));
                customClass.Add(new CustomProperty("entryType", "Type", entry.Type, "Bibitem", readOnly, true));
                foreach (BibtexTag tag in entry.Tags)
                {
                    CustomProperty item = new CustomProperty(tag.Key, tag.Key, tag.Value, "Parameters", readOnly, true);
                    customClass.Add(item);
                }

                propertyGrid1.Tag = entry;
                propertyGrid1.SelectedObject = customClass;
            }

            lblSelected.Text = $"({dataGridView1.SelectedRows.Count})";
        }

        private void ShowEntry(BibtexEntry entry, string search = "")
        {
            string text = bibtexExporter.EntryToString(entry);
            text = text.ToLower();
            search = search.ToLower();
            richTextBox1.Text = text;
            if (string.IsNullOrWhiteSpace(search))
                return;

            int selStart = richTextBox1.SelectionStart;
            int selLength = richTextBox1.SelectionLength;

            richTextBox1.SelectAll();
            richTextBox1.SelectionBackColor = richTextBox1.BackColor;

            int startIndex = 0;
            while (startIndex < richTextBox1.TextLength)
            {
                int idx = richTextBox1.Find(search, startIndex, RichTextBoxFinds.None);
                if (idx < 0)
                    break;

                richTextBox1.Select(idx, search.Length);
                richTextBox1.SelectionBackColor = Color.Yellow;
                startIndex = idx + search.Length;
            }

            richTextBox1.Select(selStart, selLength);
            richTextBox1.SelectionBackColor = richTextBox1.BackColor;
        }

        private void OpenPdf(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath))
                return;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = pdfPath,
                UseShellExecute = true
            });
        }

        private void OpenPdf(BibtexEntry entry)
        {
            if (entry == null || entry.Tags == null)
                return;

            OpenPdf(GetPdfFileName(entry));
        }

        private string GetManualPdfPairInitialDirectory(BibtexEntry entry)
        {
            string existingPdf = FindStoredPdfFile(entry);
            if (string.IsNullOrWhiteSpace(existingPdf) == false)
            {
                string existingDirectory = Path.GetDirectoryName(existingPdf);
                if (string.IsNullOrWhiteSpace(existingDirectory) == false && Directory.Exists(existingDirectory))
                    return existingDirectory;
            }

            if (string.IsNullOrWhiteSpace(Program.AppSettings.Data.PdfFolder) == false && Directory.Exists(Program.AppSettings.Data.PdfFolder))
                return Program.AppSettings.Data.PdfFolder;

            string defaultExportDirectory = GetDefaultPdfExportDirectory();
            if (string.IsNullOrWhiteSpace(defaultExportDirectory) == false && Directory.Exists(defaultExportDirectory))
                return defaultExportDirectory;

            return Application.StartupPath;
        }

        private bool PromptManualPdfPair(BibtexEntry entry, bool openAfterPair)
        {
            if (entry == null)
                return false;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.CheckFileExists = true;
                openFileDialog.CheckPathExists = true;
                openFileDialog.Filter = "PDF files (*.pdf)|*.pdf";
                openFileDialog.Title = "Select PDF for record";
                openFileDialog.InitialDirectory = GetManualPdfPairInitialDirectory(entry);

                if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                    return false;

                AssignPdfToEntry(entry, openFileDialog.FileName);
                RefreshGrid(new[] { entry });
                Changed();
                lblStatus.Text = "PDF paired manually.";

                if (openAfterPair)
                    OpenPdf(openFileDialog.FileName);

                return true;
            }
        }

        private void OpenPdfOrPromptManualPair(BibtexEntry entry)
        {
            if (entry == null)
                return;

            string pdfPath = FindPdfFile(entry);
            if (string.IsNullOrWhiteSpace(pdfPath) == false)
            {
                OpenPdf(pdfPath);
                return;
            }

            PromptManualPdfPair(entry, true);
        }
    }
}
