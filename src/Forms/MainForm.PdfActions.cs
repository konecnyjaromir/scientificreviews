using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.Logs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        public void SearchEntryTitleOnGoogle(BibtexEntry entry)
        {
            if (entry == null || entry.Tags == null)
                return;

            BibtexTag titleTag = entry.Tags
                .FirstOrDefault(t => string.Equals(t.Key, "title", StringComparison.OrdinalIgnoreCase));

            if (titleTag == null || string.IsNullOrWhiteSpace(titleTag.Value))
                return;

            OpenUrl(BuildGoogleSearchUrl(titleTag.Value));
        }

        public void SearchByDoi(BibtexEntry entry)
        {
            if (entry == null || entry.Tags == null)
                return;

            BibtexTag doiTag = entry.Tags
                .FirstOrDefault(t => string.Equals(t.Key, "doi", StringComparison.OrdinalIgnoreCase));

            if (doiTag == null || string.IsNullOrWhiteSpace(doiTag.Value))
            {
                lblStatus.Text = "Record does not contain DOI. Opened in Google search.";
                SearchEntryTitleOnGoogle(entry);
                return;
            }

            string doiValue = doiTag.Value.Trim();
            string normalizedDoi = DoiNormalizationHelper.NormalizeDoiValue(doiValue);
            DoiValueKind doiKind = DoiNormalizationHelper.GetDoiValueKind(normalizedDoi);

            if (doiKind == DoiValueKind.Classic || doiKind == DoiValueKind.Arxiv)
            {
                OpenUrl($"https://doi.org/{Uri.EscapeDataString(normalizedDoi)}");
                return;
            }

            MessageBox.Show(
                "Unsupported DOI format. The record will be opened using Google search.",
                Program.APP_NAME,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            lblStatus.Text = "Unsupported DOI format. Opened in Google search.";
            OpenUrl(BuildGoogleSearchUrl(doiValue));
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
                throw new Exception("File not exists for entry: " + (entry?.Key ?? "<null>"));

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
                        BibtexEntry entry = (BibtexEntry)drv.Row["Entry"];
                        if (entry != null)
                            SearchEntryTitleOnGoogle(entry);
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
                        BibtexEntry entry = (BibtexEntry)drv.Row["Entry"];
                        if (entry != null)
                            OpenPdfOrPromptManualPair(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private BibtexEntry[] GetSelected()
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return new BibtexEntry[0];

            List<BibtexEntry> selected = new List<BibtexEntry>();
            foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows)
            {
                if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                {
                    BibtexEntry entry = (BibtexEntry)drv.Row["Entry"];
                    selected.Add(entry);
                }
            }

            return selected.ToArray();
        }

        private BibtexEntry[] GetSelectedOrdered()
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return new BibtexEntry[0];

            List<BibtexEntry> selected = new List<BibtexEntry>();
            foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
            {
                if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                {
                    BibtexEntry entry = (BibtexEntry)drv.Row["Entry"];
                    selected.Add(entry);
                }
            }

            return selected.ToArray();
        }

        private async void exportSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ShowExportDialogAsync(DatabaseExportScope.Selected, DatabaseExportFormat.Bib);
        }

        private async void autoPairWithPdfsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await StartAutoPairOperationAsync(false);
        }

        private async Task StartAutoPairOperationAsync(bool startedAutomatically, CancellationToken externalCancellationToken = default(CancellationToken))
        {
            StatusStripOperationHandle operation = StartTrackedOperation(
                "auto-pair-pdfs",
                "Auto-pair PDFs",
                Program.AppSettings.Data.PdfFolder,
                startedAutomatically);
            if (operation == null)
                return;

            ProcessLogScope log = BeginProcessLog("Auto-pair PDFs", Program.AppSettings.Data.PdfFolder);
            using (CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken))
            {
                operation.RegisterCancellation(cancellation.Cancel);

                try
                {
                    lblStatus.Text = $"Auto-pairing PDFs using {GetConfiguredThreadCount()} thread(s)...";
                    PdfAutoPairResult result = await RunAutoPairAsync(operation, cancellation.Token);

                    RefreshGrid();
                    Changed();

                    if (result.NoPdfsFound)
                    {
                        operation.Complete("No PDFs found.", Program.AppSettings.Data.PdfFolder);
                        lblStatus.Text = "No PDFs found in Pdf Folder.";
                        log.Complete("No PDFs found.");
                        return;
                    }

                    string summary = $"Direct {result.DirectMatches}, smart {result.SmartMatches}, unmatched {result.Unmatched}";
                    operation.Complete(summary, Program.AppSettings.Data.PdfFolder);
                    lblStatus.Text = $"Auto-pair finished using {GetConfiguredThreadCount()} thread(s). Direct: {result.DirectMatches}, smart: {result.SmartMatches}, unmatched: {result.Unmatched}.";
                    log.Complete(summary);
                }
                catch (OperationCanceledException)
                {
                    operation.Cancel("Cancelled", "Auto-pair was stopped by user.");
                    lblStatus.Text = "Auto-pair cancelled.";
                    log.Complete("Auto-pair cancelled.");
                }
                catch (Exception ex)
                {
                    operation.Fail(ex, "Failed");
                    lblStatus.Text = ex.Message;
                    log.Fail(ex, "Auto-pair failed.");
                }
                finally
                {
                    log.Dispose();
                }
            }
        }

        private async Task<PdfAutoPairResult> RunAutoPairAsync(StatusStripOperationHandle operation, CancellationToken cancellationToken)
        {
            ProcessLogScope log = BeginProcessLog("Auto-pair PDFs inner", Program.AppSettings.Data.PdfFolder);
            Progress<PdfAutoPairProgress> progress = new Progress<PdfAutoPairProgress>(update =>
            {
                operation.Report(
                    update?.Summary,
                    update?.Details,
                    update?.Completed,
                    update?.Total,
                    update != null && update.IsIndeterminate);
                LogProcessProgress(log, update?.Summary, update?.Details, update?.Completed, update?.Total);
            });

            try
            {
                PdfAutoPairResult result = await _pdfMatchingService.AutoPairAsync(entries, CreatePdfMatchingOptions(), progress, cancellationToken);
                log.Complete("Auto-pair inner process completed.");
                return result;
            }
            catch (Exception ex)
            {
                log.Fail(ex, "Auto-pair inner process failed.");
                throw;
            }
            finally
            {
                log.Dispose();
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

                using (ExportPdfsForm form = new ExportPdfsForm(GetDefaultPdfExportDirectory(), dataGridView1.SelectedRows.Count > 0, RunPdfExportAsync))
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

            BibtexEntry[] toExport = options.ExportSelectedOnly ? GetSelected() : entries.ToArray();
            if (toExport.Length == 0)
                throw new InvalidOperationException("No records selected for export.");

            ProcessLogScope log = BeginProcessLog("Export PDFs", $"{toExport.Length} record(s) -> {options.OutputDirectory}");
            IProgress<ExportPdfsProgress> compositeProgress = new Progress<ExportPdfsProgress>(update =>
            {
                progress?.Report(update);
                LogProcessProgress(log, update?.StatusText, null, update?.Completed, update?.Total);
            });

            lblStatus.Text = $"Exporting PDFs using {GetConfiguredThreadCount()} thread(s)...";
            try
            {
                ExportPdfsRunResult result = await _pdfExportService.RunExportAsync(
                    toExport,
                    options,
                    _pdfMatchingService,
                    CreatePdfMatchingOptions(),
                    compositeProgress,
                    cancellationToken);

                lblStatus.Text = result.Cancelled
                    ? $"PDF export cancelled after {result.Completed}/{result.Total}."
                    : $"Exported {result.Exported} PDF(s), skipped {result.Skipped}, DOI injected into {result.Injected}.";

                if (result.Cancelled)
                    log.Fail($"PDF export cancelled after {result.Completed}/{result.Total}.");
                else
                    log.Complete($"Exported {result.Exported}, skipped {result.Skipped}, DOI injected into {result.Injected}.");

                return result;
            }
            catch (Exception ex)
            {
                log.Fail(ex, "PDF export failed.");
                throw;
            }
            finally
            {
                log.Dispose();
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
                        BibtexEntry entry = (BibtexEntry)drv.Row["Entry"];
                        if (entry != null)
                            SearchByDoi(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }
    }
}
