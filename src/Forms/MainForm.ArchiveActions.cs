using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.Logs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private void UpdateWindowTitle()
        {
            string title = Program.APP_NAME;
            if (string.IsNullOrWhiteSpace(_currentBibTexPath) == false)
            {
                string bibFileName = Path.GetFileName(_currentBibTexPath);
                if (string.IsNullOrWhiteSpace(bibFileName) == false)
                    title += " - " + bibFileName;
            }

            Text = title;
        }

        private void SetCurrentBibTex(string filePath)
        {
            _currentBibTexPath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
            if (string.IsNullOrWhiteSpace(filePath) == false)
                Program.AppSettings.Data.LastBibTex = filePath;

            UpdateWindowTitle();
        }

        private string GetDefaultPdfExportDirectory()
        {
            if (string.IsNullOrWhiteSpace(_currentBibTexPath) == false)
            {
                string bibDirectory = Path.GetDirectoryName(_currentBibTexPath);
                if (string.IsNullOrWhiteSpace(bibDirectory) == false)
                    return bibDirectory;
            }

            if (string.IsNullOrWhiteSpace(Program.AppSettings.Data.PdfFolder) == false)
                return Program.AppSettings.Data.PdfFolder;

            if (string.IsNullOrWhiteSpace(Program.AppSettings.Data.LastDirectory) == false)
                return Program.AppSettings.Data.LastDirectory;

            return Application.StartupPath;
        }

        private void StartAutomaticBackgroundOperationsAfterLoad()
        {
            if (entries.Count == 0)
                return;

            if (Program.AppSettings.Data.AutoPreprocessingMode == AutoPreprocessingMode.Off)
                return;

            _ = StartPreprocessingPipelineAsync(
                Program.AppSettings.Data.AutoPreprocessingMode,
                startedAutomatically: true,
                operationKey: "auto-preprocessing",
                operationName: "Auto-preprocessing");
        }

        private bool ConfirmReplaceCurrentArchive()
        {
            if (entries.Count == 0)
                return true;

            DialogResult result = MessageBox.Show(
                "A BibTeX archive is already loaded. Do you want to clear it and load a new one?",
                Program.APP_NAME,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return result == DialogResult.Yes;
        }

        private void ClearCurrentArchiveState(bool markChanged)
        {
            entries = new List<BibtexEntry>();
            visibleEntries = new List<BibtexEntry>();
            propertyGrid1.SelectedObject = null;
            propertyGrid1.Tag = null;
            richTextBox1.Clear();
            lblSelected.Text = "(-)";
            SetCurrentBibTex(null);
            LoadData(entries.ToArray());

            if (markChanged)
                Changed();
            else
                SetDatabaseChanged(false);
        }

        private async Task<bool> LoadBibTexFolderAsync(bool replaceExisting)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog()
            {
                SelectedPath = Program.AppSettings.Data.LastDirectory
            })
            {
                DialogResult result = folderDialog.ShowDialog(this);
                if (result != DialogResult.OK || string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                    return false;

                if (replaceExisting && ConfirmReplaceCurrentArchive() == false)
                    return false;

                StatusStripOperationHandle operation = StartTrackedOperation(
                    "load-bibtex",
                    replaceExisting ? "Open folder" : "Add folder",
                    folderDialog.SelectedPath);
                if (operation == null)
                    return false;

                Program.AppSettings.Data.LastDirectory = folderDialog.SelectedPath;
                ProcessLogScope log = BeginProcessLog(replaceExisting ? "Load BibTeX folder" : "Add BibTeX folder", folderDialog.SelectedPath);

                try
                {
                    if (replaceExisting)
                        ClearCurrentArchiveState(false);

                    var progress = new Progress<BibtexLoadProgress>(update =>
                    {
                        operation.Report(update?.Summary, update?.Details, isIndeterminate: update?.IsIndeterminate);
                        LogProcessProgress(log, update?.Summary, update?.Details);
                    });
                    BibtexLoadResult loadResult = await _bibtexLoadService.LoadFolderAsync(folderDialog.SelectedPath, progress);
                    var loadedEntries = loadResult.Entries;

                    SetCurrentBibTex(null);
                    entries.AddRange(loadedEntries);
                    LoadData(entries.ToArray());
                    Changed(!replaceExisting);

                    if (replaceExisting)
                        SetDatabaseChanged(false);

                    operation.Complete($"Loaded {loadedEntries.Count} record(s).", folderDialog.SelectedPath);
                    log.Complete($"Loaded {loadedEntries.Count} record(s).");
                    StartAutomaticBackgroundOperationsAfterLoad();
                    return true;
                }
                catch (Exception ex)
                {
                    operation.Fail(ex, "Failed");
                    log.Fail(ex, "Folder load failed.");
                    throw;
                }
                finally
                {
                    log.Dispose();
                }
            }
        }

        private async Task<bool> LoadBibTexFileAsync(bool replaceExisting)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                CheckPathExists = true,
                CheckFileExists = true,
                Filter = "Bibtex database *.bib|*.bib",
                InitialDirectory = string.IsNullOrWhiteSpace(Program.AppSettings.Data.LastBibTex)
                    ? Program.AppSettings.Data.LastDirectory
                    : Path.GetDirectoryName(Program.AppSettings.Data.LastBibTex)
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return false;

            if (replaceExisting && ConfirmReplaceCurrentArchive() == false)
                return false;

            string fileName = ofd.FileName;
            StatusStripOperationHandle operation = StartTrackedOperation(
                "load-bibtex",
                replaceExisting ? "Open file" : "Add file",
                fileName);
            if (operation == null)
                return false;

            Program.AppSettings.Data.LastDirectory = Path.GetDirectoryName(fileName);
            ProcessLogScope log = BeginProcessLog(replaceExisting ? "Load BibTeX file" : "Add BibTeX file", fileName);

            try
            {
                if (replaceExisting)
                    ClearCurrentArchiveState(false);

                var progress = new Progress<BibtexLoadProgress>(update =>
                {
                    operation.Report(update?.Summary, update?.Details, isIndeterminate: update?.IsIndeterminate);
                    LogProcessProgress(log, update?.Summary, update?.Details);
                });
                BibtexLoadResult loadResult = await _bibtexLoadService.LoadFileAsync(fileName, progress);
                var loadedEntries = loadResult.Entries;

                SetCurrentBibTex(fileName);
                entries.AddRange(loadedEntries);
                LoadData(entries.ToArray());
                Changed(!replaceExisting);

                if (replaceExisting)
                    SetDatabaseChanged(false);

                operation.Complete($"Loaded {loadedEntries.Count} record(s).", fileName);
                log.Complete($"Loaded {loadedEntries.Count} record(s).");
                StartAutomaticBackgroundOperationsAfterLoad();
                return true;
            }
            catch (Exception ex)
            {
                operation.Fail(ex, "Failed");
                log.Fail(ex, "File load failed.");
                throw;
            }
            finally
            {
                log.Dispose();
            }
        }

        private async void loadBibTexFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                bool loaded = await LoadBibTexFolderAsync(false);
                if (loaded)
                    lblStatus.Text = "Loaded.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private async void loadBibTexFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                bool loaded = await LoadBibTexFileAsync(false);
                if (loaded)
                    lblStatus.Text = "Loaded.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private async void loadReplaceBibTexFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                bool loaded = await LoadBibTexFileAsync(true);
                if (loaded)
                    lblStatus.Text = "Loaded as a new archive.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private async void loadReplaceBibTexFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                bool loaded = await LoadBibTexFolderAsync(true);
                if (loaded)
                    lblStatus.Text = "Loaded folder as a new archive.";
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

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClearCurrentArchiveState(true);
        }
    }
}
