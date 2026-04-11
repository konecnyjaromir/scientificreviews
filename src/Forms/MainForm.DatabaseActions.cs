using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.JCR.Dto;
using ScientificReviews.Logs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            public int InvalidEntries { get; set; }
            public List<string> InvalidRecordKeys { get; } = new List<string>();
        }

        private enum DoiValueKind
        {
            Empty,
            Classic,
            Arxiv,
            Invalid
        }

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
            ProcessLogScope log = BeginProcessLog("Export BibTeX", $"Records: {entriesToExport?.Length ?? 0}");
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
                    log.Complete($"Exported {entriesToExport.Length} record(s) to {saveFileDialog.FileName}.");
                }
                else
                {
                    log.Step("Cancelled by user.");
                    log.Complete("No file selected.");
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
                log.Fail(ex, "BibTeX export failed.");
            }
            finally
            {
                log.Dispose();
            }
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

        private async void fetchMissingMetadataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await StartFetchMissingMetadataOperationAsync();
        }

        private void normalizeDoiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunNormalizeDoiOperation(entries.ToArray());
        }

        private async Task StartFetchMissingMetadataOperationAsync()
        {
            BibtexEntry[] targetEntries = entries.ToArray();

            if (targetEntries.Length == 0)
            {
                lblStatus.Text = "No records available for metadata fetch.";
                return;
            }

            if (TryPrepareDoisForMetadataFetch(targetEntries) == false)
                return;

            string details = $"Fetching metadata for records: {targetEntries.Length}";

            StatusStripOperationHandle operation = StartTrackedOperation(
                "fetch-metadata",
                "Fetch metadata",
                details);
            if (operation == null)
                return;

            ProcessLogScope log = BeginProcessLog("Fetch metadata", details);

            try
            {
                lblStatus.Text = $"Fetching metadata using {GetConfiguredThreadCount()} thread(s)...";
                MetadataUpdateResult result = await RunFetchMissingMetadataAsync(targetEntries, operation);

                LoadData(entries.ToArray(), txtSearch.Text);

                if (result.UpdatedEntries > 0)
                    Changed();

                string summary = $"Updated {result.UpdatedEntries}, unresolved {result.UnresolvedEntries}, failed {result.FailedEntries}";
                string detailMessage = $"Already complete: {result.AlreadyCompleteEntries}.";
                operation.Complete(summary, detailMessage);
                log.Complete($"{summary} {detailMessage}");

                lblStatus.Text = result.UpdatedEntries > 0
                    ? $"Metadata fetch finished. Updated {result.UpdatedEntries} record(s)."
                    : "Metadata fetch finished. No missing metadata could be resolved.";
            }
            catch (Exception ex)
            {
                operation.Fail(ex, "Failed");
                lblStatus.Text = ex.Message;
                log.Fail(ex, "Metadata fetch failed.");
            }
            finally
            {
                log.Dispose();
            }
        }

        private bool TryPrepareDoisForMetadataFetch(BibtexEntry[] targetEntries)
        {
            List<string> invalidRecordKeys = GetInvalidDoiRecordKeys(targetEntries);
            if (invalidRecordKeys.Count == 0)
                return true;

            DialogResult response = MessageBox.Show(
                this,
                $"{invalidRecordKeys.Count} record(s) contain DOI values that are neither classic DOI nor arXiv DOI/identifier.\r\n\r\nDo you want to normalize DOI values now, copy arXiv eprint to doi where doi is missing, and then run metadata fetch?",
                "Normalize DOI",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (response == DialogResult.Cancel)
                return false;

            if (response != DialogResult.Yes)
                return true;

            RunNormalizeDoiOperation(targetEntries);
            return true;
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
                    LoadData(entries.ToArray(), txtSearch.Text);
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
                string normalizedDoi = NormalizeDoiValue(currentDoi);

                if (string.IsNullOrWhiteSpace(normalizedDoi))
                {
                    string eprint = BibtexTagService.GetTagValueIgnoreCase(entry, "eprint");
                    string normalizedFromEprint = TryExtractArxivIdentifier(eprint);
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
                if (GetDoiValueKind(finalDoi) == DoiValueKind.Invalid)
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
                if (GetDoiValueKind(doi) == DoiValueKind.Invalid)
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
            if (normalization.InvalidEntries > 0)
                summary += $" {normalization.InvalidEntries} record(s) still have invalid DOI.";

            return summary;
        }

        private static string NormalizeDoiValue(string doi)
        {
            string prepared = PrepareDoiValue(doi);
            if (string.IsNullOrWhiteSpace(prepared))
                return null;

            string arxivIdentifier = TryExtractArxivIdentifier(prepared);
            if (string.IsNullOrWhiteSpace(arxivIdentifier) == false)
                return arxivIdentifier;

            if (IsClassicDoiValue(prepared))
                return prepared.ToLowerInvariant();

            return prepared;
        }

        private static DoiValueKind GetDoiValueKind(string doi)
        {
            string prepared = PrepareDoiValue(doi);
            if (string.IsNullOrWhiteSpace(prepared))
                return DoiValueKind.Empty;

            if (string.IsNullOrWhiteSpace(TryExtractArxivIdentifier(prepared)) == false)
                return DoiValueKind.Arxiv;

            return IsClassicDoiValue(prepared) ? DoiValueKind.Classic : DoiValueKind.Invalid;
        }

        private static string PrepareDoiValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string prepared = value.Trim();
            prepared = Regex.Replace(prepared, @"^https?://(dx\.)?doi\.org/", string.Empty, RegexOptions.IgnoreCase);
            prepared = Regex.Replace(prepared, @"^doi:\s*", string.Empty, RegexOptions.IgnoreCase);
            return prepared.Trim().TrimEnd('/', '.', ',', ';');
        }

        private static string TryExtractArxivIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string normalized = NormalizeArxivIdentifierValue(value);
            if (string.IsNullOrWhiteSpace(normalized) == false)
                return normalized;

            string prepared = PrepareDoiValue(value);
            if (string.IsNullOrWhiteSpace(prepared))
                return null;

            Match doiMatch = Regex.Match(prepared, @"^10\.48550/arxiv[\.:](.+)$", RegexOptions.IgnoreCase);
            if (doiMatch.Success == false)
                return null;

            return NormalizeArxivIdentifierValue(doiMatch.Groups[1].Value);
        }

        private static string NormalizeArxivIdentifierValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string normalized = value.Trim();
            normalized = Regex.Replace(normalized, @"^arxiv:\s*", string.Empty, RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"^https?://arxiv\.org/(abs|pdf)/", string.Empty, RegexOptions.IgnoreCase);
            normalized = normalized.Trim().TrimEnd('/', '.', ',', ';');
            return IsArxivIdentifierValue(normalized) ? normalized.ToLowerInvariant() : null;
        }

        private static bool IsArxivIdentifierValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return Regex.IsMatch(
                value.Trim(),
                @"^(?:\d{4}\.\d{4,5}|[a-z\-]+(?:\.[a-z\-]+)?/\d{7})(v\d+)?$",
                RegexOptions.IgnoreCase);
        }

        private static bool IsClassicDoiValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return Regex.IsMatch(value.Trim(), @"^10\.\d{4,9}/\S+$", RegexOptions.IgnoreCase);
        }

        private async Task<MetadataUpdateResult> RunFetchMissingMetadataAsync(IEnumerable<BibtexEntry> targetEntries, StatusStripOperationHandle operation)
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
                MetadataUpdateResult result = await _metadataFetchService.PopulateMissingMetadataAsync(
                    targetArray,
                    new MetadataUpdateOptions
                    {
                        ContactEmail = Program.AppSettings.Data.MetadataContactEmail,
                        ThreadCount = GetConfiguredThreadCount()
                    },
                    progress);
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
            ProcessLogScope log = BeginProcessLog("Export CSV table", $"Records: {entries.Count}");
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
                    log.Complete($"Exported table with {table.Rows.Count} row(s) to {fileName}.");
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
                log.Fail(ex, "CSV export failed.");
            }
            finally
            {
                log.Dispose();
            }
        }
    }
}
