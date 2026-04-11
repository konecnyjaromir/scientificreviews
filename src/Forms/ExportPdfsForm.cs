using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public enum PdfExportFileNameMode
    {
        Key,
        KeyTitle,
        Custom
    }

    public sealed class ExportPdfsRunOptions
    {
        public bool ExportSelectedOnly { get; set; }
        public bool InjectDoiMetadata { get; set; }
        public bool PackToFolder { get; set; }
        public string OutputDirectory { get; set; }
        public PdfExportFileNameMode FileNameMode { get; set; }
        public string CustomPattern { get; set; }
    }

    public sealed class ExportPdfsProgress
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Exported { get; set; }
        public int Skipped { get; set; }
        public int Injected { get; set; }
        public string StatusText { get; set; }
    }

    public sealed class ExportPdfsRunResult
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Exported { get; set; }
        public int Skipped { get; set; }
        public int Injected { get; set; }
        public bool Cancelled { get; set; }
    }

    public class ExportPdfsForm : Form
    {
        private readonly Func<ExportPdfsRunOptions, IProgress<ExportPdfsProgress>, CancellationToken, Task<ExportPdfsRunResult>> _exportRunner;
        private readonly CheckBox chkSelectedOnly;
        private readonly CheckBox chkInjectDoi;
        private readonly CheckBox chkPackToFolder;
        private readonly TextBox txtOutputDirectory;
        private readonly ComboBox cmbFileNameMode;
        private readonly Label lblCustomPattern;
        private readonly TextBox txtCustomPattern;
        private readonly Label lblProgress;
        private readonly ProgressBar progressBar;
        private readonly Button btnBrowse;
        private readonly Button btnStart;
        private readonly Button btnStop;
        private readonly Button btnClose;
        private readonly bool _hasSelectedRecords;

        private CancellationTokenSource _exportCancellation;
        private bool _isRunning;

        public ExportPdfsForm(
            string defaultOutputDirectory,
            bool hasSelectedRecords,
            Func<ExportPdfsRunOptions, IProgress<ExportPdfsProgress>, CancellationToken, Task<ExportPdfsRunResult>> exportRunner)
        {
            if (exportRunner == null)
                throw new ArgumentNullException(nameof(exportRunner));

            _exportRunner = exportRunner;
            _hasSelectedRecords = hasSelectedRecords;

            Text = "Export PDFs";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(580, 340);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 9,
                Padding = new Padding(12)
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(layout);

            chkSelectedOnly = new CheckBox
            {
                Text = "Export selected records only",
                AutoSize = true,
                Enabled = _hasSelectedRecords,
                Checked = false,
                Margin = new Padding(3, 6, 3, 10)
            };
            layout.Controls.Add(chkSelectedOnly, 0, 0);
            layout.SetColumnSpan(chkSelectedOnly, 3);

            chkInjectDoi = new CheckBox
            {
                Text = "Inject DOI into PDF metadata",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(3, 0, 3, 10)
            };
            layout.Controls.Add(chkInjectDoi, 0, 1);
            layout.SetColumnSpan(chkInjectDoi, 3);

            chkPackToFolder = new CheckBox
            {
                Text = "Pack to folder",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(3, 0, 3, 10)
            };
            layout.Controls.Add(chkPackToFolder, 0, 2);
            layout.SetColumnSpan(chkPackToFolder, 3);

            var lblOutput = new Label
            {
                Text = "Output directory",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 10, 6)
            };
            layout.Controls.Add(lblOutput, 0, 3);

            txtOutputDirectory = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = defaultOutputDirectory ?? string.Empty
            };
            layout.Controls.Add(txtOutputDirectory, 1, 3);

            btnBrowse = new Button
            {
                Text = "Browse...",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            btnBrowse.Click += btnBrowse_Click;
            layout.Controls.Add(btnBrowse, 2, 3);

            var lblMode = new Label
            {
                Text = "File name format",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 10, 10, 6)
            };
            layout.Controls.Add(lblMode, 0, 4);

            cmbFileNameMode = new ComboBox
            {
                Dock = DockStyle.Left,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220
            };
            cmbFileNameMode.Items.AddRange(new object[]
            {
                "Key",
                "Key_Title",
                "Custom"
            });
            cmbFileNameMode.SelectedIndex = 1;
            cmbFileNameMode.SelectedIndexChanged += cmbFileNameMode_SelectedIndexChanged;
            layout.Controls.Add(cmbFileNameMode, 1, 4);
            layout.SetColumnSpan(cmbFileNameMode, 2);

            lblCustomPattern = new Label
            {
                Text = "Custom pattern",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 10, 10, 6)
            };
            layout.Controls.Add(lblCustomPattern, 0, 5);

            txtCustomPattern = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = "<key>_<title>_<doi>"
            };
            layout.Controls.Add(txtCustomPattern, 1, 5);
            layout.SetColumnSpan(txtCustomPattern, 2);

            lblProgress = new Label
            {
                Text = "Ready to export.",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 14, 3, 6)
            };
            layout.Controls.Add(lblProgress, 0, 6);
            layout.SetColumnSpan(lblProgress, 3);

            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 1000,
                Value = 0,
                Margin = new Padding(3, 0, 3, 0)
            };
            layout.Controls.Add(progressBar, 0, 7);
            layout.SetColumnSpan(progressBar, 3);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 18, 0, 0)
            };
            layout.Controls.Add(buttons, 0, 8);
            layout.SetColumnSpan(buttons, 3);

            btnClose = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.Cancel,
                AutoSize = true
            };
            buttons.Controls.Add(btnClose);

            btnStop = new Button
            {
                Text = "Stop",
                AutoSize = true,
                Enabled = false
            };
            btnStop.Click += btnStop_Click;
            buttons.Controls.Add(btnStop);

            btnStart = new Button
            {
                Text = "Start export",
                DialogResult = DialogResult.None,
                AutoSize = true
            };
            btnStart.Click += btnStart_Click;
            buttons.Controls.Add(btnStart);

            AcceptButton = btnStart;
            CancelButton = btnClose;

            UpdateCustomPatternVisibility();
            SetRunningState(false);

            FormClosing += ExportPdfsForm_FormClosing;
        }

        public bool ExportSelectedOnly => chkSelectedOnly.Checked;
        public bool InjectDoiMetadata => chkInjectDoi.Checked;
        public bool PackToFolder => chkPackToFolder.Checked;
        public string OutputDirectory => (txtOutputDirectory.Text ?? string.Empty).Trim();
        public string CustomPattern => (txtCustomPattern.Text ?? string.Empty).Trim();

        public PdfExportFileNameMode FileNameMode
        {
            get
            {
                switch (cmbFileNameMode.SelectedIndex)
                {
                    case 0:
                        return PdfExportFileNameMode.Key;
                    case 2:
                        return PdfExportFileNameMode.Custom;
                    default:
                        return PdfExportFileNameMode.KeyTitle;
                }
            }
        }

        private void cmbFileNameMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCustomPatternVisibility();
        }

        private void UpdateCustomPatternVisibility()
        {
            bool isCustom = FileNameMode == PdfExportFileNameMode.Custom;
            lblCustomPattern.Visible = isCustom;
            txtCustomPattern.Visible = isCustom;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (Directory.Exists(OutputDirectory))
                {
                    dialog.SelectedPath = OutputDirectory;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtOutputDirectory.Text = dialog.SelectedPath;
                }
            }
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (ValidateInput() == false)
                return;

            await RunExportAsync();
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                MessageBox.Show("Output directory must not be empty.", Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (FileNameMode == PdfExportFileNameMode.Custom && string.IsNullOrWhiteSpace(CustomPattern))
            {
                MessageBox.Show("Custom pattern must not be empty.", Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private async Task RunExportAsync()
        {
            var options = new ExportPdfsRunOptions
            {
                ExportSelectedOnly = ExportSelectedOnly,
                InjectDoiMetadata = InjectDoiMetadata,
                PackToFolder = PackToFolder,
                OutputDirectory = OutputDirectory,
                FileNameMode = FileNameMode,
                CustomPattern = CustomPattern
            };

            _exportCancellation = new CancellationTokenSource();
            var progress = new Progress<ExportPdfsProgress>(UpdateProgress);

            try
            {
                SetRunningState(true);
                UpdateProgress(new ExportPdfsProgress
                {
                    StatusText = "Preparing export...",
                    Total = 0,
                    Completed = 0
                });

                ExportPdfsRunResult result = await _exportRunner(options, progress, _exportCancellation.Token);
                UpdateProgress(new ExportPdfsProgress
                {
                    Total = result.Total,
                    Completed = result.Completed,
                    Exported = result.Exported,
                    Skipped = result.Skipped,
                    Injected = result.Injected,
                    StatusText = result.Cancelled
                        ? $"Export cancelled. Finished {result.Completed}/{result.Total}."
                        : $"Export finished. Exported {result.Exported}, skipped {result.Skipped}, DOI injected into {result.Injected}."
                });

                string message = result.Cancelled
                    ? $"Export was cancelled.\n\nCompleted: {result.Completed}/{result.Total}\nExported: {result.Exported}\nSkipped: {result.Skipped}\nDOI injected: {result.Injected}"
                    : $"Export finished.\n\nExported: {result.Exported}\nSkipped: {result.Skipped}\nDOI injected: {result.Injected}";

                MessageBox.Show(message, Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateProgress(new ExportPdfsProgress
                {
                    StatusText = "Export failed."
                });
            }
            finally
            {
                _exportCancellation.Dispose();
                _exportCancellation = null;
                SetRunningState(false);
            }
        }

        private void UpdateProgress(ExportPdfsProgress progress)
        {
            if (progress == null)
                return;

            lblProgress.Text = string.IsNullOrWhiteSpace(progress.StatusText)
                ? "Working..."
                : progress.StatusText;

            if (progress.Total > 0)
            {
                int value = (int)Math.Round(progress.Completed * 1000d / progress.Total);
                progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, value));
            }
            else
            {
                progressBar.Value = 0;
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            btnStop.Enabled = false;
            lblProgress.Text = "Stopping export...";
            _exportCancellation?.Cancel();
        }

        private void SetRunningState(bool isRunning)
        {
            _isRunning = isRunning;

            chkSelectedOnly.Enabled = !isRunning && _hasSelectedRecords;
            chkInjectDoi.Enabled = !isRunning;
            chkPackToFolder.Enabled = !isRunning;
            txtOutputDirectory.ReadOnly = isRunning;
            cmbFileNameMode.Enabled = !isRunning;
            txtCustomPattern.ReadOnly = isRunning;
            btnBrowse.Enabled = !isRunning;
            btnStart.Enabled = !isRunning;
            btnStop.Enabled = isRunning;
            btnClose.Enabled = !isRunning;
            ControlBox = !isRunning;
        }

        private void ExportPdfsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isRunning)
            {
                e.Cancel = true;
            }
        }
    }
}
