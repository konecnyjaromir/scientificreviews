using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public enum PdfExportFileNameMode
    {
        Key,
        KeyTitle,
        Custom
    }

    public class ExportPdfsForm : Form
    {
        private readonly CheckBox chkSelectedOnly;
        private readonly CheckBox chkInjectDoi;
        private readonly TextBox txtOutputDirectory;
        private readonly ComboBox cmbFileNameMode;
        private readonly TextBox txtCustomPattern;
        private readonly Button btnBrowse;
        private readonly Button btnOk;
        private readonly Button btnCancel;

        public ExportPdfsForm(string defaultOutputDirectory, bool hasSelectedRecords)
        {
            Text = "Export PDFs";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 250);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 6,
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
                Enabled = hasSelectedRecords,
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

            var lblOutput = new Label
            {
                Text = "Output directory",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 10, 6)
            };
            layout.Controls.Add(lblOutput, 0, 2);

            txtOutputDirectory = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = defaultOutputDirectory ?? string.Empty
            };
            layout.Controls.Add(txtOutputDirectory, 1, 2);

            btnBrowse = new Button
            {
                Text = "Browse...",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            btnBrowse.Click += btnBrowse_Click;
            layout.Controls.Add(btnBrowse, 2, 2);

            var lblMode = new Label
            {
                Text = "File name format",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 10, 10, 6)
            };
            layout.Controls.Add(lblMode, 0, 3);

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
            layout.Controls.Add(cmbFileNameMode, 1, 3);
            layout.SetColumnSpan(cmbFileNameMode, 2);

            var lblCustom = new Label
            {
                Text = "Custom pattern",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 10, 10, 6)
            };
            layout.Controls.Add(lblCustom, 0, 4);

            txtCustomPattern = new TextBox
            {
                Dock = DockStyle.Fill,
                Enabled = false,
                Text = "<key>_<title>_<doi>"
            };
            layout.Controls.Add(txtCustomPattern, 1, 4);
            layout.SetColumnSpan(txtCustomPattern, 2);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 18, 0, 0)
            };
            layout.Controls.Add(buttons, 0, 5);
            layout.SetColumnSpan(buttons, 3);

            btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.None,
                AutoSize = true
            };
            btnOk.Click += btnOk_Click;
            buttons.Controls.Add(btnOk);

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                AutoSize = true
            };
            buttons.Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        public bool ExportSelectedOnly => chkSelectedOnly.Checked;
        public bool InjectDoiMetadata => chkInjectDoi.Checked;
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
            txtCustomPattern.Enabled = FileNameMode == PdfExportFileNameMode.Custom;
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

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                MessageBox.Show("Output directory must not be empty.", Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (FileNameMode == PdfExportFileNameMode.Custom && string.IsNullOrWhiteSpace(CustomPattern))
            {
                MessageBox.Show("Custom pattern must not be empty.", Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
