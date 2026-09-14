using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using ScientificReviews.Logs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private bool _isInitializingRawModeUi;

        private OpenAddMode DefaultOpenAddMode
        {
            get => Program.AppSettings?.Data?.OpenAddMode ?? OpenAddMode.Normal;
            set
            {
                if (Program.AppSettings?.Data != null)
                    Program.AppSettings.Data.OpenAddMode = value;
            }
        }

        private bool IsRawModeEnabled => rawModeToolStripMenuItem != null && rawModeToolStripMenuItem.Checked;

        private void InitializeOpenAddModeUi()
        {
            UpdateOpenAddModeUi();
        }

        private void UpdateOpenAddModeUi()
        {
            if (rawModeToolStripMenuItem == null)
                return;

            bool rawModeEnabled = DefaultOpenAddMode == OpenAddMode.Raw;
            if (rawModeToolStripMenuItem.Checked == rawModeEnabled)
                return;

            _isInitializingRawModeUi = true;
            try
            {
                rawModeToolStripMenuItem.Checked = rawModeEnabled;
            }
            finally
            {
                _isInitializingRawModeUi = false;
            }
        }

        private void UpdateWindowTitle()
        {
            string title = Program.APP_NAME;
            string currentSessionTitle = GetCurrentBibTexSessionTitle();
            if (string.IsNullOrWhiteSpace(currentSessionTitle) == false)
            {
                title += " - " + currentSessionTitle;
            }

            Text = title;
        }

        private void SetCurrentBibTex(string filePath)
        {
            SetCurrentBibTexSources(string.IsNullOrWhiteSpace(filePath) ? Array.Empty<string>() : new[] { filePath }, filePath);
        }

        private void SetCurrentBibTexSources(IEnumerable<string> sourcePaths, string currentFilePath = null)
        {
            string[] normalizedSourcePaths = NormalizeBibTexSourcePaths(sourcePaths);

            _currentBibTexSourcePaths.Clear();
            _currentBibTexSourcePaths.AddRange(normalizedSourcePaths);

            if (string.IsNullOrWhiteSpace(currentFilePath))
                _currentBibTexPath = normalizedSourcePaths.Length == 1 ? normalizedSourcePaths[0] : null;
            else
                _currentBibTexPath = Path.GetFullPath(currentFilePath);

            if (string.IsNullOrWhiteSpace(_currentBibTexPath) == false)
                Program.AppSettings.Data.LastBibTex = _currentBibTexPath;

            UpdateWindowTitle();
        }

        private void AddCurrentBibTexSources(IEnumerable<string> sourcePaths)
        {
            string[] normalizedSourcePaths = NormalizeBibTexSourcePaths(sourcePaths);
            if (normalizedSourcePaths.Length == 0)
                return;

            string[] combinedSourcePaths = _currentBibTexSourcePaths
                .Concat(normalizedSourcePaths)
                .Where(path => string.IsNullOrWhiteSpace(path) == false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            SetCurrentBibTexSources(combinedSourcePaths);
        }

        private string[] NormalizeBibTexSourcePaths(IEnumerable<string> sourcePaths)
        {
            return (sourcePaths ?? Array.Empty<string>())
                .Where(path => string.IsNullOrWhiteSpace(path) == false)
                .Select(path => Path.GetFullPath(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private string[] GetBibTexSourcePaths(string sourcePath, bool isFolderLoad)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return Array.Empty<string>();

            if (!isFolderLoad)
                return NormalizeBibTexSourcePaths(new[] { sourcePath });

            return Directory
                .GetFiles(sourcePath, "*.bib", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private string GetCurrentBibTexSessionTitle()
        {
            return string.Join(",",
                _currentBibTexSourcePaths
                    .Select(Path.GetFileName)
                    .Where(fileName => string.IsNullOrWhiteSpace(fileName) == false));
        }

        private string GetCurrentBibTexSessionSaveName()
        {
            string[] nameParts = _currentBibTexSourcePaths
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .Where(name => string.IsNullOrWhiteSpace(name) == false)
                .Select(SanitizeFileNamePart)
                .Where(name => string.IsNullOrWhiteSpace(name) == false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (nameParts.Length == 0)
                return null;

            return string.Join("_", nameParts);
        }

        private string SanitizeFileNamePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] sanitized = value
                .Trim()
                .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
                .ToArray();

            string result = new string(sanitized).Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }

        private string GetDefaultPdfExportDirectory()
        {
            if (string.IsNullOrWhiteSpace(_currentBibTexPath) == false)
            {
                string bibDirectory = Path.GetDirectoryName(_currentBibTexPath);
                if (string.IsNullOrWhiteSpace(bibDirectory) == false)
                    return bibDirectory;
            }

            if (_currentBibTexSourcePaths.Count > 0)
            {
                string bibDirectory = Path.GetDirectoryName(_currentBibTexSourcePaths[0]);
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

        private async Task<bool> LoadBibTexFolderAsync(bool replaceExisting, bool runPostLoadPreprocessing = true)
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
                    folderDialog.SelectedPath,
                    cancelAction: null);
                if (operation == null)
                    return false;

                Program.AppSettings.Data.LastDirectory = folderDialog.SelectedPath;
                ProcessLogScope log = BeginProcessLog(replaceExisting ? "Load BibTeX folder" : "Add BibTeX folder", folderDialog.SelectedPath);
                using (CancellationTokenSource cancellation = new CancellationTokenSource())
                {
                    operation.RegisterCancellation(cancellation.Cancel);

                    try
                    {
                        if (replaceExisting)
                            ClearCurrentArchiveState(false);

                        var progress = new Progress<BibtexLoadProgress>(update =>
                        {
                            operation.Report(update?.Summary, update?.Details, isIndeterminate: update?.IsIndeterminate);
                            LogProcessProgress(log, update?.Summary, update?.Details);
                        });
                        BibtexLoadResult loadResult = await _bibtexLoadService.LoadFolderAsync(folderDialog.SelectedPath, progress, cancellation.Token);
                        var loadedEntries = loadResult.Entries;
                        string[] sourcePaths = GetBibTexSourcePaths(loadResult.SourcePath, loadResult.IsFolderLoad);

                        if (replaceExisting)
                            SetCurrentBibTexSources(sourcePaths);
                        else
                            AddCurrentBibTexSources(sourcePaths);
                        entries.AddRange(loadedEntries);
                        LoadData(entries.ToArray());
                        Changed(!replaceExisting);

                        if (replaceExisting)
                            SetDatabaseChanged(false);

                        operation.Complete($"Loaded {loadedEntries.Count} record(s).", folderDialog.SelectedPath);
                        log.Complete($"Loaded {loadedEntries.Count} record(s).");
                        if (runPostLoadPreprocessing)
                            StartAutomaticBackgroundOperationsAfterLoad();
                        return true;
                    }
                    catch (OperationCanceledException)
                    {
                        operation.Cancel("Cancelled", "Folder loading was stopped by user.");
                        lblStatus.Text = "Folder load cancelled.";
                        log.Complete("Folder load cancelled.");
                        return false;
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
        }

        private async Task<bool> LoadBibTexFileAsync(bool replaceExisting, bool runPostLoadPreprocessing = true)
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
                fileName,
                cancelAction: null);
            if (operation == null)
                return false;

            Program.AppSettings.Data.LastDirectory = Path.GetDirectoryName(fileName);
            ProcessLogScope log = BeginProcessLog(replaceExisting ? "Load BibTeX file" : "Add BibTeX file", fileName);
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                operation.RegisterCancellation(cancellation.Cancel);

                try
                {
                    if (replaceExisting)
                        ClearCurrentArchiveState(false);

                    var progress = new Progress<BibtexLoadProgress>(update =>
                    {
                        operation.Report(update?.Summary, update?.Details, isIndeterminate: update?.IsIndeterminate);
                        LogProcessProgress(log, update?.Summary, update?.Details);
                    });
                    BibtexLoadResult loadResult = await _bibtexLoadService.LoadFileAsync(fileName, progress, cancellation.Token);
                    var loadedEntries = loadResult.Entries;
                    string[] sourcePaths = GetBibTexSourcePaths(loadResult.SourcePath, loadResult.IsFolderLoad);

                    if (replaceExisting)
                        SetCurrentBibTexSources(sourcePaths, fileName);
                    else
                        AddCurrentBibTexSources(sourcePaths);
                    entries.AddRange(loadedEntries);
                    LoadData(entries.ToArray());
                    Changed(!replaceExisting);

                    if (replaceExisting)
                        SetDatabaseChanged(false);

                    operation.Complete($"Loaded {loadedEntries.Count} record(s).", fileName);
                    log.Complete($"Loaded {loadedEntries.Count} record(s).");
                    if (runPostLoadPreprocessing)
                        StartAutomaticBackgroundOperationsAfterLoad();
                    return true;
                }
                catch (OperationCanceledException)
                {
                    operation.Cancel("Cancelled", "File loading was stopped by user.");
                    lblStatus.Text = "File load cancelled.";
                    log.Complete("File load cancelled.");
                    return false;
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
        }

        private async void loadBibTexFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (IsRawModeEnabled)
            {
                loadBibTexFolderRawToolStripMenuItem_Click(sender, e);
                return;
            }

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

        private async void loadBibTexFolderRawToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                bool loaded = await LoadBibTexFolderAsync(false, false);
                if (loaded)
                    lblStatus.Text = "Added folder as raw.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private async void loadBibTexFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (IsRawModeEnabled)
            {
                loadBibTexFileRawToolStripMenuItem_Click(sender, e);
                return;
            }

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

        private async void loadBibTexFileRawToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                bool loaded = await LoadBibTexFileAsync(false, false);
                if (loaded)
                    lblStatus.Text = "Added file as raw.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private async void loadReplaceBibTexFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (IsRawModeEnabled)
            {
                loadReplaceBibTexFileRawToolStripMenuItem_Click(sender, e);
                return;
            }

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
            if (IsRawModeEnabled)
            {
                loadReplaceBibTexFolderRawToolStripMenuItem_Click(sender, e);
                return;
            }

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

        private async void loadReplaceBibTexFileRawToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                bool loaded = await LoadBibTexFileAsync(true, false);
                if (loaded)
                    lblStatus.Text = "Loaded file as a raw archive.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }
        }

        private async void loadReplaceBibTexFolderRawToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                bool loaded = await LoadBibTexFolderAsync(true, false);
                if (loaded)
                    lblStatus.Text = "Loaded folder as a raw archive.";
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

        private void rawModeToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            if (_isInitializingRawModeUi)
                return;

            DefaultOpenAddMode = rawModeToolStripMenuItem.Checked
                ? OpenAddMode.Raw
                : OpenAddMode.Normal;

            string statusMessage = rawModeToolStripMenuItem.Checked
                ? "Raw Mode enabled."
                : "Normal open/add mode enabled.";

            lblStatus.Text = statusMessage;
        }
    }
}
