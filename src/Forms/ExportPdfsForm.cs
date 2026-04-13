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
        private readonly bool _hasSelectedRecords;

        private CheckBox _chkSelectedOnly;
        private CheckBox _chkInjectDoi;
        private CheckBox _chkPackToFolder;
        private TextBox _txtOutputDirectory;
        private ComboBox _cmbFileNameMode;
        private Label _lblCustomPattern;
        private TextBox _txtCustomPattern;
        private Label _lblModeHint;
        private Label _lblProgress;
        private ProgressBar _progressBar;
        private Button _btnBrowse;
        private Button _btnStart;
        private Button _btnStop;
        private Button _btnClose;

        private CancellationTokenSource _exportCancellation;
        private bool _isRunning;

        private TableLayoutPanel _root;
        private Panel _headerPanel;
        private Label _lblTitle;
        private Label _lblSubtitle;
        private GroupBox _grpScope;
        private GroupBox _grpOptions;
        private GroupBox _grpOutput;
        private GroupBox _grpNaming;
        private TableLayoutPanel _scopeLayout;
        private TableLayoutPanel _optionsLayout;
        private TableLayoutPanel _outputLayout;
        private TableLayoutPanel _namingLayout;
        private FlowLayoutPanel _buttonPanel;
        private Label _lblOutputDirectory;
        private Label _lblFileNameMode;

        public ExportPdfsForm(
            string defaultOutputDirectory,
            bool hasSelectedRecords,
            Func<ExportPdfsRunOptions, IProgress<ExportPdfsProgress>, CancellationToken, Task<ExportPdfsRunResult>> exportRunner)
        {
            if (exportRunner == null)
                throw new ArgumentNullException(nameof(exportRunner));

            _exportRunner = exportRunner;
            _hasSelectedRecords = hasSelectedRecords;

            InitializeComponent();
            _txtOutputDirectory.Text = defaultOutputDirectory ?? string.Empty;

            UpdateCustomPatternVisibility();
            UpdateModeHint();
            SetRunningState(false);
        }

        public bool ExportSelectedOnly => _chkSelectedOnly.Checked;
        public bool InjectDoiMetadata => _chkInjectDoi.Checked;
        public bool PackToFolder => _chkPackToFolder.Checked;
        public string OutputDirectory => (_txtOutputDirectory.Text ?? string.Empty).Trim();
        public string CustomPattern => (_txtCustomPattern.Text ?? string.Empty).Trim();

        public PdfExportFileNameMode FileNameMode
        {
            get
            {
                switch (_cmbFileNameMode.SelectedIndex)
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

        private void InitializeComponent()
        {
            _root = new TableLayoutPanel();
            _headerPanel = new Panel();
            _lblTitle = new Label();
            _lblSubtitle = new Label();
            _grpScope = new GroupBox();
            _scopeLayout = new TableLayoutPanel();
            _chkSelectedOnly = new CheckBox();
            _grpOptions = new GroupBox();
            _optionsLayout = new TableLayoutPanel();
            _chkInjectDoi = new CheckBox();
            _chkPackToFolder = new CheckBox();
            _grpOutput = new GroupBox();
            _outputLayout = new TableLayoutPanel();
            _lblOutputDirectory = new Label();
            _txtOutputDirectory = new TextBox();
            _btnBrowse = new Button();
            _grpNaming = new GroupBox();
            _namingLayout = new TableLayoutPanel();
            _lblFileNameMode = new Label();
            _cmbFileNameMode = new ComboBox();
            _lblModeHint = new Label();
            _lblCustomPattern = new Label();
            _txtCustomPattern = new TextBox();
            _lblProgress = new Label();
            _progressBar = new ProgressBar();
            _buttonPanel = new FlowLayoutPanel();
            _btnClose = new Button();
            _btnStop = new Button();
            _btnStart = new Button();
            _root.SuspendLayout();
            _headerPanel.SuspendLayout();
            _grpScope.SuspendLayout();
            _scopeLayout.SuspendLayout();
            _grpOptions.SuspendLayout();
            _optionsLayout.SuspendLayout();
            _grpOutput.SuspendLayout();
            _outputLayout.SuspendLayout();
            _grpNaming.SuspendLayout();
            _namingLayout.SuspendLayout();
            _buttonPanel.SuspendLayout();
            SuspendLayout();
            //
            // _root
            //
            _root.ColumnCount = 1;
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _root.Controls.Add(_headerPanel, 0, 0);
            _root.Controls.Add(_grpScope, 0, 1);
            _root.Controls.Add(_grpOptions, 0, 2);
            _root.Controls.Add(_grpOutput, 0, 3);
            _root.Controls.Add(_grpNaming, 0, 4);
            _root.Controls.Add(_lblProgress, 0, 5);
            _root.Controls.Add(_progressBar, 0, 6);
            _root.Controls.Add(_buttonPanel, 0, 7);
            _root.Dock = DockStyle.Fill;
            _root.Location = new Point(0, 0);
            _root.Name = "_root";
            _root.Padding = new Padding(14);
            _root.RowCount = 8;
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 146F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _root.Size = new Size(620, 644);
            _root.TabIndex = 0;
            //
            // _headerPanel
            //
            _headerPanel.BackColor = Color.White;
            _headerPanel.Controls.Add(_lblTitle);
            _headerPanel.Controls.Add(_lblSubtitle);
            _headerPanel.Dock = DockStyle.Fill;
            _headerPanel.Location = new Point(17, 17);
            _headerPanel.Name = "_headerPanel";
            _headerPanel.Padding = new Padding(14, 10, 14, 10);
            _headerPanel.Size = new Size(586, 66);
            _headerPanel.TabIndex = 0;
            //
            // _lblTitle
            //
            _lblTitle.AutoSize = true;
            _lblTitle.Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
            _lblTitle.ForeColor = Color.FromArgb(35, 49, 66);
            _lblTitle.Location = new Point(10, 8);
            _lblTitle.Name = "_lblTitle";
            _lblTitle.Size = new Size(114, 30);
            _lblTitle.TabIndex = 0;
            _lblTitle.Text = "Export PDFs";
            //
            // _lblSubtitle
            //
            _lblSubtitle.AutoSize = true;
            _lblSubtitle.ForeColor = Color.FromArgb(90, 104, 120);
            _lblSubtitle.Location = new Point(12, 38);
            _lblSubtitle.Name = "_lblSubtitle";
            _lblSubtitle.Size = new Size(465, 20);
            _lblSubtitle.TabIndex = 1;
            _lblSubtitle.Text = "Choose the records, output folder, and naming rule for exported PDFs.";
            //
            // _grpScope
            //
            _grpScope.Controls.Add(_scopeLayout);
            _grpScope.Dock = DockStyle.Fill;
            _grpScope.Location = new Point(17, 89);
            _grpScope.Name = "_grpScope";
            _grpScope.Padding = new Padding(12, 10, 12, 12);
            _grpScope.Size = new Size(586, 66);
            _grpScope.TabIndex = 1;
            _grpScope.TabStop = false;
            _grpScope.Text = "Scope";
            //
            // _scopeLayout
            //
            _scopeLayout.ColumnCount = 1;
            _scopeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _scopeLayout.Controls.Add(_chkSelectedOnly, 0, 0);
            _scopeLayout.Dock = DockStyle.Fill;
            _scopeLayout.Location = new Point(12, 30);
            _scopeLayout.Name = "_scopeLayout";
            _scopeLayout.RowCount = 1;
            _scopeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _scopeLayout.Size = new Size(562, 24);
            _scopeLayout.TabIndex = 0;
            //
            // _chkSelectedOnly
            //
            _chkSelectedOnly.AutoSize = true;
            _chkSelectedOnly.Checked = false;
            _chkSelectedOnly.Enabled = _hasSelectedRecords;
            _chkSelectedOnly.Location = new Point(3, 1);
            _chkSelectedOnly.Margin = new Padding(3, 1, 3, 1);
            _chkSelectedOnly.Name = "_chkSelectedOnly";
            _chkSelectedOnly.Size = new Size(202, 24);
            _chkSelectedOnly.TabIndex = 0;
            _chkSelectedOnly.Text = "Export selected records only";
            _chkSelectedOnly.UseVisualStyleBackColor = true;
            //
            // _grpOptions
            //
            _grpOptions.Controls.Add(_optionsLayout);
            _grpOptions.Dock = DockStyle.Fill;
            _grpOptions.Location = new Point(17, 161);
            _grpOptions.Name = "_grpOptions";
            _grpOptions.Padding = new Padding(12, 10, 12, 12);
            _grpOptions.Size = new Size(586, 86);
            _grpOptions.TabIndex = 2;
            _grpOptions.TabStop = false;
            _grpOptions.Text = "Options";
            //
            // _optionsLayout
            //
            _optionsLayout.ColumnCount = 1;
            _optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _optionsLayout.Controls.Add(_chkInjectDoi, 0, 0);
            _optionsLayout.Controls.Add(_chkPackToFolder, 0, 1);
            _optionsLayout.Dock = DockStyle.Fill;
            _optionsLayout.Location = new Point(12, 30);
            _optionsLayout.Name = "_optionsLayout";
            _optionsLayout.RowCount = 2;
            _optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            _optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            _optionsLayout.Size = new Size(562, 44);
            _optionsLayout.TabIndex = 0;
            //
            // _chkInjectDoi
            //
            _chkInjectDoi.AutoSize = true;
            _chkInjectDoi.Checked = true;
            _chkInjectDoi.CheckState = CheckState.Checked;
            _chkInjectDoi.Location = new Point(3, 1);
            _chkInjectDoi.Margin = new Padding(3, 1, 3, 1);
            _chkInjectDoi.Name = "_chkInjectDoi";
            _chkInjectDoi.Size = new Size(211, 20);
            _chkInjectDoi.TabIndex = 0;
            _chkInjectDoi.Text = "Inject DOI into PDF metadata";
            _chkInjectDoi.UseVisualStyleBackColor = true;
            //
            // _chkPackToFolder
            //
            _chkPackToFolder.AutoSize = true;
            _chkPackToFolder.Checked = true;
            _chkPackToFolder.CheckState = CheckState.Checked;
            _chkPackToFolder.Location = new Point(3, 23);
            _chkPackToFolder.Margin = new Padding(3, 1, 3, 1);
            _chkPackToFolder.Name = "_chkPackToFolder";
            _chkPackToFolder.Size = new Size(118, 20);
            _chkPackToFolder.TabIndex = 1;
            _chkPackToFolder.Text = "Pack into folder";
            _chkPackToFolder.UseVisualStyleBackColor = true;
            //
            // _grpOutput
            //
            _grpOutput.Controls.Add(_outputLayout);
            _grpOutput.Dock = DockStyle.Fill;
            _grpOutput.Location = new Point(17, 253);
            _grpOutput.Name = "_grpOutput";
            _grpOutput.Padding = new Padding(12, 10, 12, 12);
            _grpOutput.Size = new Size(586, 74);
            _grpOutput.TabIndex = 3;
            _grpOutput.TabStop = false;
            _grpOutput.Text = "Output";
            //
            // _outputLayout
            //
            _outputLayout.ColumnCount = 3;
            _outputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            _outputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _outputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
            _outputLayout.Controls.Add(_lblOutputDirectory, 0, 0);
            _outputLayout.Controls.Add(_txtOutputDirectory, 1, 0);
            _outputLayout.Controls.Add(_btnBrowse, 2, 0);
            _outputLayout.Dock = DockStyle.Fill;
            _outputLayout.Location = new Point(12, 30);
            _outputLayout.Name = "_outputLayout";
            _outputLayout.RowCount = 1;
            _outputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _outputLayout.Size = new Size(562, 32);
            _outputLayout.TabIndex = 0;
            //
            // _lblOutputDirectory
            //
            _lblOutputDirectory.Dock = DockStyle.Fill;
            _lblOutputDirectory.Location = new Point(3, 5);
            _lblOutputDirectory.Margin = new Padding(3, 5, 3, 5);
            _lblOutputDirectory.Name = "_lblOutputDirectory";
            _lblOutputDirectory.Size = new Size(114, 22);
            _lblOutputDirectory.TabIndex = 0;
            _lblOutputDirectory.Text = "Target folder:";
            _lblOutputDirectory.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _txtOutputDirectory
            //
            _txtOutputDirectory.Dock = DockStyle.Fill;
            _txtOutputDirectory.Location = new Point(123, 2);
            _txtOutputDirectory.Margin = new Padding(3, 2, 3, 2);
            _txtOutputDirectory.Name = "_txtOutputDirectory";
            _txtOutputDirectory.Size = new Size(332, 27);
            _txtOutputDirectory.TabIndex = 1;
            //
            // _btnBrowse
            //
            _btnBrowse.AutoSize = true;
            _btnBrowse.Location = new Point(461, 2);
            _btnBrowse.Margin = new Padding(3, 2, 3, 2);
            _btnBrowse.Name = "_btnBrowse";
            _btnBrowse.Size = new Size(94, 28);
            _btnBrowse.TabIndex = 2;
            _btnBrowse.Text = "Browse...";
            _btnBrowse.UseVisualStyleBackColor = true;
            _btnBrowse.Click += new EventHandler(btnBrowse_Click);
            //
            // _grpNaming
            //
            _grpNaming.Controls.Add(_namingLayout);
            _grpNaming.Dock = DockStyle.Fill;
            _grpNaming.Location = new Point(17, 333);
            _grpNaming.Name = "_grpNaming";
            _grpNaming.Padding = new Padding(12, 10, 12, 12);
            _grpNaming.Size = new Size(586, 140);
            _grpNaming.TabIndex = 4;
            _grpNaming.TabStop = false;
            _grpNaming.Text = "File naming";
            //
            // _namingLayout
            //
            _namingLayout.ColumnCount = 2;
            _namingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            _namingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _namingLayout.Controls.Add(_lblFileNameMode, 0, 0);
            _namingLayout.Controls.Add(_cmbFileNameMode, 1, 0);
            _namingLayout.Controls.Add(_lblModeHint, 1, 1);
            _namingLayout.Controls.Add(_lblCustomPattern, 0, 2);
            _namingLayout.Controls.Add(_txtCustomPattern, 1, 2);
            _namingLayout.Dock = DockStyle.Fill;
            _namingLayout.Location = new Point(12, 30);
            _namingLayout.Name = "_namingLayout";
            _namingLayout.RowCount = 3;
            _namingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            _namingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            _namingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            _namingLayout.Size = new Size(562, 98);
            _namingLayout.TabIndex = 0;
            //
            // _lblFileNameMode
            //
            _lblFileNameMode.Dock = DockStyle.Fill;
            _lblFileNameMode.Location = new Point(3, 5);
            _lblFileNameMode.Margin = new Padding(3, 5, 3, 5);
            _lblFileNameMode.Name = "_lblFileNameMode";
            _lblFileNameMode.Size = new Size(114, 22);
            _lblFileNameMode.TabIndex = 0;
            _lblFileNameMode.Text = "Mode:";
            _lblFileNameMode.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _cmbFileNameMode
            //
            _cmbFileNameMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFileNameMode.FormattingEnabled = true;
            _cmbFileNameMode.Items.AddRange(new object[]
            {
                "Key",
                "Key + title",
                "Custom"
            });
            _cmbFileNameMode.Location = new Point(123, 2);
            _cmbFileNameMode.Margin = new Padding(3, 2, 3, 2);
            _cmbFileNameMode.Name = "_cmbFileNameMode";
            _cmbFileNameMode.Size = new Size(320, 28);
            _cmbFileNameMode.TabIndex = 1;
            _cmbFileNameMode.SelectedIndex = 1;
            _cmbFileNameMode.SelectedIndexChanged += new EventHandler(cmbFileNameMode_SelectedIndexChanged);
            //
            // _lblModeHint
            //
            _lblModeHint.AutoSize = true;
            _lblModeHint.ForeColor = Color.FromArgb(90, 104, 120);
            _lblModeHint.Location = new Point(123, 38);
            _lblModeHint.Margin = new Padding(3, 6, 3, 0);
            _lblModeHint.MaximumSize = new Size(380, 0);
            _lblModeHint.Name = "_lblModeHint";
            _lblModeHint.Size = new Size(159, 20);
            _lblModeHint.TabIndex = 2;
            _lblModeHint.Text = "Uses the entry key and title.";
            //
            // _lblCustomPattern
            //
            _lblCustomPattern.Dock = DockStyle.Fill;
            _lblCustomPattern.Location = new Point(3, 73);
            _lblCustomPattern.Margin = new Padding(3, 5, 3, 5);
            _lblCustomPattern.Name = "_lblCustomPattern";
            _lblCustomPattern.Size = new Size(114, 22);
            _lblCustomPattern.TabIndex = 3;
            _lblCustomPattern.Text = "Pattern:";
            _lblCustomPattern.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _txtCustomPattern
            //
            _txtCustomPattern.Dock = DockStyle.Fill;
            _txtCustomPattern.Location = new Point(123, 70);
            _txtCustomPattern.Margin = new Padding(3, 2, 3, 2);
            _txtCustomPattern.Name = "_txtCustomPattern";
            _txtCustomPattern.Size = new Size(436, 27);
            _txtCustomPattern.TabIndex = 4;
            _txtCustomPattern.Text = "<key>_<title>_<doi>";
            //
            // _lblProgress
            //
            _lblProgress.AutoSize = true;
            _lblProgress.Dock = DockStyle.Fill;
            _lblProgress.Location = new Point(17, 462);
            _lblProgress.Margin = new Padding(3, 6, 3, 0);
            _lblProgress.Name = "_lblProgress";
            _lblProgress.Size = new Size(586, 22);
            _lblProgress.TabIndex = 5;
            _lblProgress.Text = "Ready to export.";
            //
            // _progressBar
            //
            _progressBar.Dock = DockStyle.Fill;
            _progressBar.Location = new Point(17, 487);
            _progressBar.Maximum = 1000;
            _progressBar.Name = "_progressBar";
            _progressBar.Size = new Size(586, 20);
            _progressBar.TabIndex = 6;
            //
            // _buttonPanel
            //
            _buttonPanel.Controls.Add(_btnClose);
            _buttonPanel.Controls.Add(_btnStop);
            _buttonPanel.Controls.Add(_btnStart);
            _buttonPanel.Dock = DockStyle.Fill;
            _buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            _buttonPanel.Location = new Point(14, 520);
            _buttonPanel.Margin = new Padding(0, 10, 0, 0);
            _buttonPanel.Name = "_buttonPanel";
            _buttonPanel.Size = new Size(592, 110);
            _buttonPanel.TabIndex = 7;
            _buttonPanel.WrapContents = false;
            //
            // _btnClose
            //
            _btnClose.AutoSize = true;
            _btnClose.DialogResult = DialogResult.Cancel;
            _btnClose.Location = new Point(514, 3);
            _btnClose.Name = "_btnClose";
            _btnClose.Size = new Size(75, 30);
            _btnClose.TabIndex = 0;
            _btnClose.Text = "Close";
            _btnClose.UseVisualStyleBackColor = true;
            //
            // _btnStop
            //
            _btnStop.AutoSize = true;
            _btnStop.Enabled = false;
            _btnStop.Location = new Point(433, 3);
            _btnStop.Name = "_btnStop";
            _btnStop.Size = new Size(75, 30);
            _btnStop.TabIndex = 1;
            _btnStop.Text = "Stop";
            _btnStop.UseVisualStyleBackColor = true;
            _btnStop.Click += new EventHandler(btnStop_Click);
            //
            // _btnStart
            //
            _btnStart.AutoSize = true;
            _btnStart.Location = new Point(348, 3);
            _btnStart.Name = "_btnStart";
            _btnStart.Size = new Size(79, 30);
            _btnStart.TabIndex = 2;
            _btnStart.Text = "Export";
            _btnStart.UseVisualStyleBackColor = true;
            _btnStart.Click += new EventHandler(btnStart_Click);
            //
            // ExportPdfsForm
            //
            AcceptButton = _btnStart;
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(247, 249, 252);
            CancelButton = _btnClose;
            ClientSize = new Size(620, 644);
            Controls.Add(_root);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ExportPdfsForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Export PDFs";
            FormClosing += new FormClosingEventHandler(ExportPdfsForm_FormClosing);
            _root.ResumeLayout(false);
            _root.PerformLayout();
            _headerPanel.ResumeLayout(false);
            _headerPanel.PerformLayout();
            _grpScope.ResumeLayout(false);
            _scopeLayout.ResumeLayout(false);
            _scopeLayout.PerformLayout();
            _grpOptions.ResumeLayout(false);
            _optionsLayout.ResumeLayout(false);
            _optionsLayout.PerformLayout();
            _grpOutput.ResumeLayout(false);
            _outputLayout.ResumeLayout(false);
            _outputLayout.PerformLayout();
            _grpNaming.ResumeLayout(false);
            _namingLayout.ResumeLayout(false);
            _namingLayout.PerformLayout();
            _buttonPanel.ResumeLayout(false);
            _buttonPanel.PerformLayout();
            ResumeLayout(false);
        }

        private void cmbFileNameMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCustomPatternVisibility();
            UpdateModeHint();
        }

        private void UpdateCustomPatternVisibility()
        {
            bool isCustom = FileNameMode == PdfExportFileNameMode.Custom;
            _lblCustomPattern.Visible = isCustom;
            _txtCustomPattern.Visible = isCustom;
        }

        private void UpdateModeHint()
        {
            switch (FileNameMode)
            {
                case PdfExportFileNameMode.Key:
                    _lblModeHint.Text = "Names files using only the entry key.";
                    break;
                case PdfExportFileNameMode.Custom:
                    _lblModeHint.Text = "Use placeholders like <key>, <title>, <doi>, <author>, or <year>.";
                    break;
                default:
                    _lblModeHint.Text = "Names files using the entry key followed by the title.";
                    break;
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                if (Directory.Exists(OutputDirectory))
                    dialog.SelectedPath = OutputDirectory;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _txtOutputDirectory.Text = dialog.SelectedPath;
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
                _txtOutputDirectory.Focus();
                return false;
            }

            if (FileNameMode == PdfExportFileNameMode.Custom && string.IsNullOrWhiteSpace(CustomPattern))
            {
                MessageBox.Show("Custom pattern must not be empty.", Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtCustomPattern.Focus();
                return false;
            }

            return true;
        }

        private async Task RunExportAsync()
        {
            ExportPdfsRunOptions options = new ExportPdfsRunOptions
            {
                ExportSelectedOnly = ExportSelectedOnly,
                InjectDoiMetadata = InjectDoiMetadata,
                PackToFolder = PackToFolder,
                OutputDirectory = OutputDirectory,
                FileNameMode = FileNameMode,
                CustomPattern = CustomPattern
            };

            _exportCancellation = new CancellationTokenSource();
            IProgress<ExportPdfsProgress> progress = new Progress<ExportPdfsProgress>(UpdateProgress);

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
                if (_exportCancellation != null)
                {
                    _exportCancellation.Dispose();
                    _exportCancellation = null;
                }

                SetRunningState(false);
            }
        }

        private void UpdateProgress(ExportPdfsProgress progress)
        {
            if (progress == null)
                return;

            _lblProgress.Text = string.IsNullOrWhiteSpace(progress.StatusText)
                ? "Working..."
                : progress.StatusText;

            if (progress.Total > 0)
            {
                int value = (int)Math.Round(progress.Completed * 1000d / progress.Total);
                _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, value));
            }
            else
            {
                _progressBar.Value = 0;
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _btnStop.Enabled = false;
            _lblProgress.Text = "Stopping export...";
            _exportCancellation?.Cancel();
        }

        private void SetRunningState(bool isRunning)
        {
            _isRunning = isRunning;

            _chkSelectedOnly.Enabled = !isRunning && _hasSelectedRecords;
            _chkInjectDoi.Enabled = !isRunning;
            _chkPackToFolder.Enabled = !isRunning;
            _txtOutputDirectory.ReadOnly = isRunning;
            _cmbFileNameMode.Enabled = !isRunning;
            _txtCustomPattern.ReadOnly = isRunning;
            _btnBrowse.Enabled = !isRunning;
            _btnStart.Enabled = !isRunning;
            _btnStop.Enabled = isRunning;
            _btnClose.Enabled = !isRunning;
            ControlBox = !isRunning;
        }

        private void ExportPdfsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isRunning)
                e.Cancel = true;
        }
    }
}
