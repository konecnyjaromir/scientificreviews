using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.JCR.Dto;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private void createEntryKeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> keys = new List<string>();
            foreach (BibtexEntry entry in entries)
            {
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

        private async void exportDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportDatabaseAsync(entries.ToArray());
        }

        private async void exportVisibleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportDatabaseAsync(visibleEntries.ToArray());
        }

        private async Task ExportDatabaseAsync(BibtexEntry[] entriesToExport)
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
                        BibtexExporter exporter = new BibtexExporter();
                        string content = exporter.EntriesToString(entriesToExport);
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
                    BibtexExporter exporter = new BibtexExporter();
                    string[] content = exporter.GetDois(entries.ToArray());
                    File.WriteAllLines(fileName, content);
                }

                lblStatus.Text = "Export done.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private void removeDuplicitiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            entries = BibtexUtils.RemoveDuplicateEntriesByTag(entries, "title");
            LoadData(entries.ToArray());
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
            entries = BibtexUtils.RemoveDuplicateEntriesByTag(entries, "doi");
            LoadData(entries.ToArray());
            Changed();
        }

        private void updatePageTagFormatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BibtexUtils.UpdatePages(entries);
            Changed();
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

            LoadData(entries.ToArray());
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

        private void columnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditColumnsForm frm = new EditColumnsForm();
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
    }
}
