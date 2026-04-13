using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public sealed class ExportDatabaseForm : Form
    {
        private readonly Func<DatabaseExportOptions, IProgress<DatabaseExportProgress>, CancellationToken, Task<DatabaseExportRunResult>> _exportRunner;

        private ComboBox _cmbScope;
        private ComboBox _cmbMode;
        private ComboBox _cmbSeparator;
        private TextBox _txtCustomSeparator;
        private RadioButton _rbBib;
        private RadioButton _rbCsv;
        private Label _lblSeparator;
        private Label _lblCustomSeparator;
        private Label _lblModeHint;
        private Label _lblProgress;
        private GroupBox _grpCsv;
        private ProgressBar _progressBar;
        private Button _btnStart;
        private Button _btnStop;
        private Button _btnClose;

        private CancellationTokenSource _exportCancellation;
        private bool _isRunning;
        private bool _suppressFormatEvents;
        private TableLayoutPanel root;
        private Panel headerPanel;
        private Label lblTitle;
        private Label lblSubtitle;
        private GroupBox grpScope;
        private TableLayoutPanel scopeLayout;
        private Label lblScope;
        private GroupBox grpFormat;
        private TableLayoutPanel formatLayout;
        private Label lblFormat;
        private GroupBox grpMode;
        private TableLayoutPanel modeLayout;
        private Label lblMode;
        private TableLayoutPanel csvLayout;
        private FlowLayoutPanel buttonPanel;
        private string _selectedOutputFilePath;

        public ExportDatabaseForm(
            DatabaseExportOptions defaultOptions,
            Func<DatabaseExportOptions, IProgress<DatabaseExportProgress>, CancellationToken, Task<DatabaseExportRunResult>> exportRunner)
        {
            if (exportRunner == null)
                throw new ArgumentNullException(nameof(exportRunner));

            _exportRunner = exportRunner;
            InitializeComponent();
            ApplyDefaults(defaultOptions ?? new DatabaseExportOptions());
        }

        public DatabaseExportOptions GetOptions()
        {
            return new DatabaseExportOptions
            {
                Scope = GetSelectedScope(),
                Format = GetSelectedFormat(),
                Mode = GetSelectedMode(),
                CsvSeparator = GetSelectedSeparator(),
                OutputFilePath = (_selectedOutputFilePath ?? string.Empty).Trim()
            };
        }

        private void InitializeComponent()
        {
            this.root = new System.Windows.Forms.TableLayoutPanel();
            this.headerPanel = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblSubtitle = new System.Windows.Forms.Label();
            this.grpScope = new System.Windows.Forms.GroupBox();
            this.scopeLayout = new System.Windows.Forms.TableLayoutPanel();
            this.lblScope = new System.Windows.Forms.Label();
            this._cmbScope = new System.Windows.Forms.ComboBox();
            this.grpFormat = new System.Windows.Forms.GroupBox();
            this.formatLayout = new System.Windows.Forms.TableLayoutPanel();
            this.lblFormat = new System.Windows.Forms.Label();
            this._rbBib = new System.Windows.Forms.RadioButton();
            this._rbCsv = new System.Windows.Forms.RadioButton();
            this.grpMode = new System.Windows.Forms.GroupBox();
            this.modeLayout = new System.Windows.Forms.TableLayoutPanel();
            this.lblMode = new System.Windows.Forms.Label();
            this._cmbMode = new System.Windows.Forms.ComboBox();
            this._lblModeHint = new System.Windows.Forms.Label();
            this._grpCsv = new System.Windows.Forms.GroupBox();
            this.csvLayout = new System.Windows.Forms.TableLayoutPanel();
            this._lblSeparator = new System.Windows.Forms.Label();
            this._cmbSeparator = new System.Windows.Forms.ComboBox();
            this._lblCustomSeparator = new System.Windows.Forms.Label();
            this._txtCustomSeparator = new System.Windows.Forms.TextBox();
            this._lblProgress = new System.Windows.Forms.Label();
            this._progressBar = new System.Windows.Forms.ProgressBar();
            this.buttonPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._btnClose = new System.Windows.Forms.Button();
            this._btnStop = new System.Windows.Forms.Button();
            this._btnStart = new System.Windows.Forms.Button();
            this.root.SuspendLayout();
            this.headerPanel.SuspendLayout();
            this.grpScope.SuspendLayout();
            this.scopeLayout.SuspendLayout();
            this.grpFormat.SuspendLayout();
            this.formatLayout.SuspendLayout();
            this.grpMode.SuspendLayout();
            this.modeLayout.SuspendLayout();
            this._grpCsv.SuspendLayout();
            this.csvLayout.SuspendLayout();
            this.buttonPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // root
            // 
            this.root.ColumnCount = 1;
            this.root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.root.Controls.Add(this.headerPanel, 0, 0);
            this.root.Controls.Add(this.grpScope, 0, 1);
            this.root.Controls.Add(this.grpFormat, 0, 2);
            this.root.Controls.Add(this.grpMode, 0, 3);
            this.root.Controls.Add(this._grpCsv, 0, 4);
            this.root.Controls.Add(this._lblProgress, 0, 5);
            this.root.Controls.Add(this._progressBar, 0, 6);
            this.root.Controls.Add(this.buttonPanel, 0, 7);
            this.root.Dock = System.Windows.Forms.DockStyle.Fill;
            this.root.Location = new System.Drawing.Point(0, 0);
            this.root.Name = "root";
            this.root.Padding = new System.Windows.Forms.Padding(14);
            this.root.RowCount = 8;
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 76F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 96F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 92F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.root.Size = new System.Drawing.Size(620, 560);
            this.root.TabIndex = 0;
            // 
            // headerPanel
            // 
            this.headerPanel.BackColor = System.Drawing.Color.White;
            this.headerPanel.Controls.Add(this.lblTitle);
            this.headerPanel.Controls.Add(this.lblSubtitle);
            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.headerPanel.Location = new System.Drawing.Point(17, 17);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Padding = new System.Windows.Forms.Padding(14, 10, 14, 10);
            this.headerPanel.Size = new System.Drawing.Size(586, 66);
            this.headerPanel.TabIndex = 0;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 12.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(49)))), ((int)(((byte)(66)))));
            this.lblTitle.Location = new System.Drawing.Point(10, 8);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(173, 30);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Export Database";
            // 
            // lblSubtitle
            // 
            this.lblSubtitle.AutoSize = true;
            this.lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(104)))), ((int)(((byte)(120)))));
            this.lblSubtitle.Location = new System.Drawing.Point(12, 38);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(508, 20);
            this.lblSubtitle.TabIndex = 1;
            this.lblSubtitle.Text = "Choose what to export, the target format, and how tags should be mapped.";
            // 
            // grpScope
            // 
            this.grpScope.Controls.Add(this.scopeLayout);
            this.grpScope.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpScope.Location = new System.Drawing.Point(17, 89);
            this.grpScope.Name = "grpScope";
            this.grpScope.Padding = new System.Windows.Forms.Padding(12, 10, 12, 12);
            this.grpScope.Size = new System.Drawing.Size(586, 66);
            this.grpScope.TabIndex = 1;
            this.grpScope.TabStop = false;
            this.grpScope.Text = "Scope";
            // 
            // scopeLayout
            // 
            this.scopeLayout.ColumnCount = 2;
            this.scopeLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 115F));
            this.scopeLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.scopeLayout.Controls.Add(this.lblScope, 0, 0);
            this.scopeLayout.Controls.Add(this._cmbScope, 1, 0);
            this.scopeLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.scopeLayout.Location = new System.Drawing.Point(12, 30);
            this.scopeLayout.Name = "scopeLayout";
            this.scopeLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.scopeLayout.Size = new System.Drawing.Size(562, 24);
            this.scopeLayout.TabIndex = 0;
            // 
            // lblScope
            // 
            this.lblScope.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblScope.Location = new System.Drawing.Point(3, 5);
            this.lblScope.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.lblScope.Name = "lblScope";
            this.lblScope.Size = new System.Drawing.Size(109, 14);
            this.lblScope.TabIndex = 0;
            this.lblScope.Text = "Export what:";
            this.lblScope.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _cmbScope
            // 
            this._cmbScope.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._cmbScope.FormattingEnabled = true;
            this._cmbScope.Items.AddRange(new object[] {
            "Visible",
            "Selected",
            "All"});
            this._cmbScope.Location = new System.Drawing.Point(118, 2);
            this._cmbScope.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._cmbScope.Name = "_cmbScope";
            this._cmbScope.Size = new System.Drawing.Size(320, 28);
            this._cmbScope.TabIndex = 1;
            // 
            // grpFormat
            // 
            this.grpFormat.Controls.Add(this.formatLayout);
            this.grpFormat.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpFormat.Location = new System.Drawing.Point(17, 161);
            this.grpFormat.Name = "grpFormat";
            this.grpFormat.Padding = new System.Windows.Forms.Padding(12, 10, 12, 12);
            this.grpFormat.Size = new System.Drawing.Size(586, 70);
            this.grpFormat.TabIndex = 2;
            this.grpFormat.TabStop = false;
            this.grpFormat.Text = "Format";
            // 
            // formatLayout
            // 
            this.formatLayout.ColumnCount = 3;
            this.formatLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 115F));
            this.formatLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.formatLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.formatLayout.Controls.Add(this.lblFormat, 0, 0);
            this.formatLayout.Controls.Add(this._rbBib, 1, 0);
            this.formatLayout.Controls.Add(this._rbCsv, 2, 0);
            this.formatLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formatLayout.Location = new System.Drawing.Point(12, 30);
            this.formatLayout.Name = "formatLayout";
            this.formatLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.formatLayout.Size = new System.Drawing.Size(562, 28);
            this.formatLayout.TabIndex = 0;
            // 
            // lblFormat
            // 
            this.lblFormat.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFormat.Location = new System.Drawing.Point(3, 5);
            this.lblFormat.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.lblFormat.Name = "lblFormat";
            this.lblFormat.Size = new System.Drawing.Size(109, 18);
            this.lblFormat.TabIndex = 0;
            this.lblFormat.Text = "File type:";
            this.lblFormat.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _rbBib
            // 
            this._rbBib.AutoSize = true;
            this._rbBib.Location = new System.Drawing.Point(118, 3);
            this._rbBib.Name = "_rbBib";
            this._rbBib.Size = new System.Drawing.Size(114, 22);
            this._rbBib.TabIndex = 1;
            this._rbBib.Text = "BibTeX (.bib)";
            this._rbBib.CheckedChanged += new System.EventHandler(this.FormatRadio_CheckedChanged);
            // 
            // _rbCsv
            // 
            this._rbCsv.AutoSize = true;
            this._rbCsv.Location = new System.Drawing.Point(238, 3);
            this._rbCsv.Name = "_rbCsv";
            this._rbCsv.Size = new System.Drawing.Size(93, 22);
            this._rbCsv.TabIndex = 2;
            this._rbCsv.Text = "CSV (.csv)";
            this._rbCsv.CheckedChanged += new System.EventHandler(this.FormatRadio_CheckedChanged);
            // 
            // grpMode
            // 
            this.grpMode.Controls.Add(this.modeLayout);
            this.grpMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpMode.Location = new System.Drawing.Point(17, 237);
            this.grpMode.Name = "grpMode";
            this.grpMode.Padding = new System.Windows.Forms.Padding(12, 10, 12, 12);
            this.grpMode.Size = new System.Drawing.Size(586, 90);
            this.grpMode.TabIndex = 3;
            this.grpMode.TabStop = false;
            this.grpMode.Text = "Mode";
            // 
            // modeLayout
            // 
            this.modeLayout.ColumnCount = 2;
            this.modeLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 115F));
            this.modeLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.modeLayout.Controls.Add(this.lblMode, 0, 0);
            this.modeLayout.Controls.Add(this._cmbMode, 1, 0);
            this.modeLayout.Controls.Add(this._lblModeHint, 1, 1);
            this.modeLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.modeLayout.Location = new System.Drawing.Point(12, 30);
            this.modeLayout.Name = "modeLayout";
            this.modeLayout.RowCount = 2;
            this.modeLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.modeLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.modeLayout.Size = new System.Drawing.Size(562, 48);
            this.modeLayout.TabIndex = 0;
            // 
            // lblMode
            // 
            this.lblMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMode.Location = new System.Drawing.Point(3, 5);
            this.lblMode.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.lblMode.Name = "lblMode";
            this.lblMode.Size = new System.Drawing.Size(109, 22);
            this.lblMode.TabIndex = 0;
            this.lblMode.Text = "Export mode:";
            this.lblMode.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _cmbMode
            // 
            this._cmbMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._cmbMode.FormattingEnabled = true;
            this._cmbMode.Items.AddRange(new object[] {
            "Normal (all tags)",
            "As columns",
            "As standard"});
            this._cmbMode.Location = new System.Drawing.Point(118, 2);
            this._cmbMode.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._cmbMode.Name = "_cmbMode";
            this._cmbMode.Size = new System.Drawing.Size(320, 28);
            this._cmbMode.TabIndex = 1;
            this._cmbMode.SelectedIndexChanged += new System.EventHandler(this.cmbMode_SelectedIndexChanged);
            // 
            // _lblModeHint
            // 
            this._lblModeHint.AutoSize = true;
            this._lblModeHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(104)))), ((int)(((byte)(120)))));
            this._lblModeHint.Location = new System.Drawing.Point(118, 38);
            this._lblModeHint.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._lblModeHint.MaximumSize = new System.Drawing.Size(380, 0);
            this._lblModeHint.Name = "_lblModeHint";
            this._lblModeHint.Size = new System.Drawing.Size(167, 10);
            this._lblModeHint.TabIndex = 2;
            this._lblModeHint.Text = "Normal exports all tags.";
            // 
            // _grpCsv
            // 
            this._grpCsv.Controls.Add(this.csvLayout);
            this._grpCsv.Dock = System.Windows.Forms.DockStyle.Fill;
            this._grpCsv.Location = new System.Drawing.Point(17, 333);
            this._grpCsv.Name = "_grpCsv";
            this._grpCsv.Padding = new System.Windows.Forms.Padding(12, 10, 12, 12);
            this._grpCsv.Size = new System.Drawing.Size(586, 86);
            this._grpCsv.TabIndex = 4;
            this._grpCsv.TabStop = false;
            this._grpCsv.Text = "CSV options";
            // 
            // csvLayout
            // 
            this.csvLayout.ColumnCount = 2;
            this.csvLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 115F));
            this.csvLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.csvLayout.Controls.Add(this._lblSeparator, 0, 0);
            this.csvLayout.Controls.Add(this._cmbSeparator, 1, 0);
            this.csvLayout.Controls.Add(this._lblCustomSeparator, 0, 1);
            this.csvLayout.Controls.Add(this._txtCustomSeparator, 1, 1);
            this.csvLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.csvLayout.Location = new System.Drawing.Point(12, 30);
            this.csvLayout.Name = "csvLayout";
            this.csvLayout.RowCount = 2;
            this.csvLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.csvLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.csvLayout.Size = new System.Drawing.Size(562, 44);
            this.csvLayout.TabIndex = 0;
            // 
            // _lblSeparator
            // 
            this._lblSeparator.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lblSeparator.Location = new System.Drawing.Point(3, 5);
            this._lblSeparator.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this._lblSeparator.Name = "_lblSeparator";
            this._lblSeparator.Size = new System.Drawing.Size(109, 24);
            this._lblSeparator.TabIndex = 0;
            this._lblSeparator.Text = "Separator:";
            this._lblSeparator.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _cmbSeparator
            // 
            this._cmbSeparator.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._cmbSeparator.FormattingEnabled = true;
            this._cmbSeparator.Items.AddRange(new object[] {
            "Comma (,)",
            "Semicolon (;)",
            "Tab",
            "Custom"});
            this._cmbSeparator.Location = new System.Drawing.Point(118, 2);
            this._cmbSeparator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._cmbSeparator.Name = "_cmbSeparator";
            this._cmbSeparator.Size = new System.Drawing.Size(320, 28);
            this._cmbSeparator.TabIndex = 1;
            this._cmbSeparator.SelectedIndexChanged += new System.EventHandler(this.cmbSeparator_SelectedIndexChanged);
            // 
            // _lblCustomSeparator
            // 
            this._lblCustomSeparator.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lblCustomSeparator.Location = new System.Drawing.Point(3, 39);
            this._lblCustomSeparator.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this._lblCustomSeparator.Name = "_lblCustomSeparator";
            this._lblCustomSeparator.Size = new System.Drawing.Size(109, 24);
            this._lblCustomSeparator.TabIndex = 2;
            this._lblCustomSeparator.Text = "Custom value:";
            this._lblCustomSeparator.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _txtCustomSeparator
            // 
            this._txtCustomSeparator.Location = new System.Drawing.Point(118, 36);
            this._txtCustomSeparator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._txtCustomSeparator.Name = "_txtCustomSeparator";
            this._txtCustomSeparator.Size = new System.Drawing.Size(260, 27);
            this._txtCustomSeparator.TabIndex = 3;
            // 
            // _lblProgress
            // 
            this._lblProgress.AutoSize = true;
            this._lblProgress.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lblProgress.Location = new System.Drawing.Point(17, 428);
            this._lblProgress.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this._lblProgress.Name = "_lblProgress";
            this._lblProgress.Size = new System.Drawing.Size(586, 22);
            this._lblProgress.TabIndex = 5;
            this._lblProgress.Text = "Ready to export.";
            // 
            // _progressBar
            // 
            this._progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._progressBar.Location = new System.Drawing.Point(17, 453);
            this._progressBar.Maximum = 1000;
            this._progressBar.Name = "_progressBar";
            this._progressBar.Size = new System.Drawing.Size(586, 20);
            this._progressBar.TabIndex = 6;
            // 
            // buttonPanel
            // 
            this.buttonPanel.Controls.Add(this._btnClose);
            this.buttonPanel.Controls.Add(this._btnStop);
            this.buttonPanel.Controls.Add(this._btnStart);
            this.buttonPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttonPanel.Location = new System.Drawing.Point(14, 486);
            this.buttonPanel.Margin = new System.Windows.Forms.Padding(0, 10, 0, 0);
            this.buttonPanel.Name = "buttonPanel";
            this.buttonPanel.Size = new System.Drawing.Size(592, 60);
            this.buttonPanel.TabIndex = 7;
            this.buttonPanel.WrapContents = false;
            // 
            // _btnClose
            // 
            this._btnClose.AutoSize = true;
            this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnClose.Location = new System.Drawing.Point(514, 3);
            this._btnClose.Name = "_btnClose";
            this._btnClose.Size = new System.Drawing.Size(75, 30);
            this._btnClose.TabIndex = 0;
            this._btnClose.Text = "Close";
            this._btnClose.UseVisualStyleBackColor = true;
            // 
            // _btnStop
            // 
            this._btnStop.AutoSize = true;
            this._btnStop.Enabled = false;
            this._btnStop.Location = new System.Drawing.Point(433, 3);
            this._btnStop.Name = "_btnStop";
            this._btnStop.Size = new System.Drawing.Size(75, 30);
            this._btnStop.TabIndex = 1;
            this._btnStop.Text = "Stop";
            this._btnStop.UseVisualStyleBackColor = true;
            this._btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // _btnStart
            // 
            this._btnStart.AutoSize = true;
            this._btnStart.Location = new System.Drawing.Point(352, 3);
            this._btnStart.Name = "_btnStart";
            this._btnStart.Size = new System.Drawing.Size(75, 30);
            this._btnStart.TabIndex = 2;
            this._btnStart.Text = "Export";
            this._btnStart.UseVisualStyleBackColor = true;
            this._btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // ExportDatabaseForm
            // 
            this.AcceptButton = this._btnStart;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(247)))), ((int)(((byte)(249)))), ((int)(((byte)(252)))));
            this.CancelButton = this._btnClose;
            this.ClientSize = new System.Drawing.Size(620, 560);
            this.Controls.Add(this.root);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExportDatabaseForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Export";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ExportDatabaseForm_FormClosing);
            this.root.ResumeLayout(false);
            this.root.PerformLayout();
            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            this.grpScope.ResumeLayout(false);
            this.scopeLayout.ResumeLayout(false);
            this.grpFormat.ResumeLayout(false);
            this.formatLayout.ResumeLayout(false);
            this.formatLayout.PerformLayout();
            this.grpMode.ResumeLayout(false);
            this.modeLayout.ResumeLayout(false);
            this.modeLayout.PerformLayout();
            this._grpCsv.ResumeLayout(false);
            this.csvLayout.ResumeLayout(false);
            this.csvLayout.PerformLayout();
            this.buttonPanel.ResumeLayout(false);
            this.buttonPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        private void ApplyDefaults(DatabaseExportOptions options)
        {
            _suppressFormatEvents = true;
            try
            {
                _cmbScope.SelectedIndex = Math.Max(0, _cmbScope.FindStringExact(GetScopeLabel(options.Scope)));
                _cmbMode.SelectedIndex = Math.Max(0, _cmbMode.FindStringExact(GetModeLabel(options.Mode)));
                _selectedOutputFilePath = options.OutputFilePath ?? string.Empty;
                _rbBib.Checked = options.Format != DatabaseExportFormat.Csv;
                _rbCsv.Checked = options.Format == DatabaseExportFormat.Csv;
                ApplySeparator(options.CsvSeparator);
            }
            finally
            {
                _suppressFormatEvents = false;
            }

            UpdateModeHint();
            UpdateCsvVisibility();
            SetRunningState(false);
        }

        private void FormatRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressFormatEvents)
                return;

            UpdateCsvVisibility();
            UpdateSuggestedOutputFilePath(true);
        }

        private void cmbSeparator_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCustomSeparatorVisibility();
        }

        private void cmbMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateModeHint();
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (PromptForOutputFile() == false || ValidateInput() == false)
                return;

            await RunExportAsync();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _btnStop.Enabled = false;
            _lblProgress.Text = "Stopping export...";
            _exportCancellation?.Cancel();
        }

        private bool PromptForOutputFile()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.CheckPathExists = true;
                dialog.Filter = GetSelectedFormat() == DatabaseExportFormat.Csv
                    ? "CSV files *.csv|*.csv"
                    : "Bibtex database *.bib|*.bib";
                dialog.Title = GetSelectedFormat() == DatabaseExportFormat.Csv
                    ? "Export CSV"
                    : "Export BibTeX";

                string suggestedPath = GetSuggestedOutputFilePath();
                if (string.IsNullOrWhiteSpace(suggestedPath) == false)
                {
                    string directory = Path.GetDirectoryName(suggestedPath);
                    if (string.IsNullOrWhiteSpace(directory) == false)
                        dialog.InitialDirectory = directory;

                    dialog.FileName = Path.GetFileName(suggestedPath);
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _selectedOutputFilePath = dialog.FileName;
                    return true;
                }
            }

            return false;
        }

        private void ExportDatabaseForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isRunning)
                e.Cancel = true;
        }

        private bool ValidateInput()
        {
            if (GetSelectedFormat() == DatabaseExportFormat.Csv && string.IsNullOrEmpty(GetSelectedSeparator()))
            {
                MessageBox.Show("CSV separator cannot be empty.", Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtCustomSeparator.Focus();
                return false;
            }

            return true;
        }

        private async Task RunExportAsync()
        {
            DatabaseExportOptions options = GetOptions();
            _exportCancellation = new CancellationTokenSource();
            IProgress<DatabaseExportProgress> progress = new Progress<DatabaseExportProgress>(UpdateProgress);

            try
            {
                SetRunningState(true);
                UpdateProgress(new DatabaseExportProgress
                {
                    Total = 0,
                    Completed = 0,
                    StatusText = "Preparing export..."
                });

                DatabaseExportRunResult result = await _exportRunner(options, progress, _exportCancellation.Token);
                UpdateProgress(new DatabaseExportProgress
                {
                    Total = result.Total,
                    Completed = result.Completed,
                    StatusText = result.Cancelled
                        ? $"Export cancelled. Finished {result.Completed}/{result.Total}."
                        : $"Export finished. Exported {result.Completed} record(s)."
                });

                string formatLabel = result.Format == DatabaseExportFormat.Csv ? "CSV" : "BibTeX";
                string message = result.Cancelled
                    ? $"Export was cancelled.\n\nCompleted: {result.Completed}/{result.Total}"
                    : $"{formatLabel} export finished.\n\nExported records: {result.Completed}\nFile: {result.OutputFilePath}";

                MessageBox.Show(message, Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (result.Cancelled == false)
                {
                    SetRunningState(false);
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateProgress(new DatabaseExportProgress
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

        private void UpdateProgress(DatabaseExportProgress progress)
        {
            if (progress == null)
                return;

            _lblProgress.Text = string.IsNullOrWhiteSpace(progress.StatusText) ? "Working..." : progress.StatusText;
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

        private void SetRunningState(bool isRunning)
        {
            _isRunning = isRunning;

            _cmbScope.Enabled = !isRunning;
            _cmbMode.Enabled = !isRunning;
            _cmbSeparator.Enabled = !isRunning;
            _txtCustomSeparator.ReadOnly = isRunning;
            _rbBib.Enabled = !isRunning;
            _rbCsv.Enabled = !isRunning;
            _btnStart.Enabled = !isRunning;
            _btnStop.Enabled = isRunning;
            _btnClose.Enabled = !isRunning;
            ControlBox = !isRunning;
        }

        private DatabaseExportScope GetSelectedScope()
        {
            switch (_cmbScope.SelectedItem?.ToString())
            {
                case "Visible":
                    return DatabaseExportScope.Visible;
                case "Selected":
                    return DatabaseExportScope.Selected;
                default:
                    return DatabaseExportScope.All;
            }
        }

        private DatabaseExportFormat GetSelectedFormat()
        {
            return _rbCsv.Checked ? DatabaseExportFormat.Csv : DatabaseExportFormat.Bib;
        }

        private DatabaseExportMode GetSelectedMode()
        {
            switch (_cmbMode.SelectedItem?.ToString())
            {
                case "As columns":
                    return DatabaseExportMode.AsColumns;
                case "As standard":
                    return DatabaseExportMode.AsStandard;
                default:
                    return DatabaseExportMode.Normal;
            }
        }

        private string GetSelectedSeparator()
        {
            switch (_cmbSeparator.SelectedItem?.ToString())
            {
                case "Comma (,)":
                    return ",";
                case "Semicolon (;)":
                    return ";";
                case "Tab":
                    return "\t";
                default:
                    return (_txtCustomSeparator.Text ?? string.Empty).Trim();
            }
        }

        private void ApplySeparator(string separator)
        {
            string normalizedSeparator = NormalizeSeparator(separator);
            switch (normalizedSeparator)
            {
                case ",":
                    _cmbSeparator.SelectedItem = "Comma (,)";
                    break;
                case ";":
                    _cmbSeparator.SelectedItem = "Semicolon (;)";
                    break;
                case "\t":
                    _cmbSeparator.SelectedItem = "Tab";
                    break;
                default:
                    _cmbSeparator.SelectedItem = "Custom";
                    _txtCustomSeparator.Text = normalizedSeparator;
                    break;
            }

            UpdateCustomSeparatorVisibility();
        }

        private void UpdateCsvVisibility()
        {
            bool isCsv = GetSelectedFormat() == DatabaseExportFormat.Csv;
            _grpCsv.Enabled = isCsv;
        }

        private string GetSuggestedOutputFilePath()
        {
            return BuildOutputFilePath(GetSelectedFormat(), _selectedOutputFilePath, false);
        }

        private void UpdateSuggestedOutputFilePath(bool replaceExistingExtension)
        {
            _selectedOutputFilePath = BuildOutputFilePath(GetSelectedFormat(), _selectedOutputFilePath, replaceExistingExtension);
        }

        private string BuildOutputFilePath(DatabaseExportFormat format, string path, bool replaceExistingExtension)
        {
            path = (path ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string desiredExtension = format == DatabaseExportFormat.Csv ? ".csv" : ".bib";
            string currentExtension = Path.GetExtension(path);

            if (string.IsNullOrWhiteSpace(currentExtension))
                return path + desiredExtension;

            if (replaceExistingExtension &&
                (string.Equals(currentExtension, ".bib", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(currentExtension, ".csv", StringComparison.OrdinalIgnoreCase)))
                return Path.ChangeExtension(path, desiredExtension);

            return path;
        }

        private void UpdateModeHint()
        {
            switch (GetSelectedMode())
            {
                case DatabaseExportMode.AsColumns:
                    _lblModeHint.Text = "Exports only tags listed in custom columns from View -> Columns.";
                    break;
                case DatabaseExportMode.AsStandard:
                    _lblModeHint.Text = "Exports only tags listed in Standard columns from Settings.";
                    break;
                default:
                    _lblModeHint.Text = "Exports all tags found in the chosen records.";
                    break;
            }
        }

        private void UpdateCustomSeparatorVisibility()
        {
            bool isCustom = string.Equals(_cmbSeparator.SelectedItem?.ToString(), "Custom", StringComparison.Ordinal);
            _lblCustomSeparator.Visible = isCustom;
            _txtCustomSeparator.Visible = isCustom;
        }

        private static string NormalizeSeparator(string separator)
        {
            if (string.IsNullOrWhiteSpace(separator))
                return ",";

            if (string.Equals(separator, "TAB", StringComparison.OrdinalIgnoreCase) || string.Equals(separator, "\\t", StringComparison.OrdinalIgnoreCase))
                return "\t";

            return separator;
        }

        private static string GetScopeLabel(DatabaseExportScope scope)
        {
            switch (scope)
            {
                case DatabaseExportScope.Visible:
                    return "Visible";
                case DatabaseExportScope.Selected:
                    return "Selected";
                default:
                    return "All";
            }
        }

        private static string GetModeLabel(DatabaseExportMode mode)
        {
            switch (mode)
            {
                case DatabaseExportMode.AsColumns:
                    return "As columns";
                case DatabaseExportMode.AsStandard:
                    return "As standard";
                default:
                    return "Normal (all tags)";
            }
        }
    }
}
