using Newtonsoft.Json;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public sealed class FirstTimeUseForm : Form
    {
        private static readonly Color Ink = Color.FromArgb(31, 43, 57);
        private static readonly Color Muted = Color.FromArgb(102, 117, 130);
        private static readonly Color Accent = Color.FromArgb(33, 112, 139);
        private static readonly Color Surface = Color.FromArgb(246, 249, 250);
        private static readonly Color Sidebar = Color.FromArgb(28, 49, 64);
        private static readonly string[] StepNames = { "Welcome", "Opening archives", "PDFs", "Metadata", "Safety", "Review" };

        private readonly AppSettingsData draft;
        private readonly Panel content = new Panel();
        private readonly Label stepTitle = new Label();
        private readonly Label stepDescription = new Label();
        private readonly Label progress = new Label();
        private readonly Button backButton = new Button();
        private readonly Button nextButton = new Button();
        private readonly Button laterButton = new Button();
        private readonly Label[] stepLabels = new Label[StepNames.Length];
        private int step;

        public FirstTimeUseForm(AppSettingsData currentSettings)
        {
            if (currentSettings == null)
                throw new ArgumentNullException(nameof(currentSettings));

            draft = JsonConvert.DeserializeObject<AppSettingsData>(JsonConvert.SerializeObject(currentSettings))
                ?? new AppSettingsData();
            if (draft.AllowBackup && string.IsNullOrWhiteSpace(draft.BackupFolder))
                draft.BackupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Scientific Reviews Backups");
            BuildWindow();
            ShowStep();
        }

        private void BuildWindow()
        {
            Text = "First Time Use | Scientific Reviews";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(830, 570);
            MinimumSize = Size;
            BackColor = Surface;
            Font = new Font("Segoe UI", 9F);

            Panel right = new Panel { Dock = DockStyle.Fill, BackColor = Surface };
            Panel left = new Panel { Dock = DockStyle.Left, Width = 210, BackColor = Sidebar };
            Label brand = new Label
            {
                Text = "SCIENTIFIC\r\nREVIEWS",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
                Location = new Point(25, 28),
                Size = new Size(170, 60)
            };
            left.Controls.Add(brand);
            Label guide = new Label
            {
                Text = "SETUP GUIDE",
                ForeColor = Color.FromArgb(157, 197, 207),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Location = new Point(26, 115),
                Size = new Size(160, 22)
            };
            left.Controls.Add(guide);
            for (int i = 0; i < StepNames.Length; i++)
            {
                Label label = new Label
                {
                    Location = new Point(24, 155 + i * 49),
                    Size = new Size(178, 36),
                    Padding = new Padding(10, 8, 0, 0),
                    Text = (i + 1) + "   " + StepNames[i],
                    ForeColor = Color.FromArgb(180, 201, 209),
                    Font = new Font("Segoe UI", 9F)
                };
                stepLabels[i] = label;
                left.Controls.Add(label);
            }
            left.Controls.Add(new Label
            {
                Text = "You can return to this guide\r\nfrom Project > Settings.",
                ForeColor = Color.FromArgb(157, 197, 207),
                Location = new Point(26, 485),
                Size = new Size(175, 48)
            });

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = Color.White };
            backButton.Text = "Back";
            backButton.Location = new Point(30, 18);
            backButton.Size = new Size(98, 36);
            backButton.Click += (sender, args) => { if (step > 0) { step--; ShowStep(); } };
            laterButton.Text = "Later";
            laterButton.Location = new Point(365, 18);
            laterButton.Size = new Size(98, 36);
            laterButton.Click += (sender, args) => { DialogResult = DialogResult.Cancel; Close(); };
            nextButton.Location = new Point(475, 18);
            nextButton.Size = new Size(132, 36);
            nextButton.BackColor = Accent;
            nextButton.ForeColor = Color.White;
            nextButton.FlatStyle = FlatStyle.Flat;
            nextButton.FlatAppearance.BorderSize = 0;
            nextButton.Click += NextButton_Click;
            footer.Controls.Add(backButton);
            footer.Controls.Add(laterButton);
            footer.Controls.Add(nextButton);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 139, BackColor = Surface, Padding = new Padding(30, 22, 25, 0) };
            progress.Dock = DockStyle.Top;
            progress.Height = 25;
            progress.ForeColor = Accent;
            progress.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            stepTitle.Dock = DockStyle.Top;
            stepTitle.Height = 47;
            stepTitle.Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold);
            stepTitle.ForeColor = Ink;
            stepDescription.Dock = DockStyle.Fill;
            stepDescription.ForeColor = Muted;
            stepDescription.Font = new Font("Segoe UI", 9.5F);
            header.Controls.Add(stepDescription);
            header.Controls.Add(stepTitle);
            header.Controls.Add(progress);

            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(30, 0, 35, 18);
            right.Controls.Add(content);
            right.Controls.Add(header);
            right.Controls.Add(footer);
            Controls.Add(right);
            Controls.Add(left);
            AcceptButton = nextButton;
        }

        private void ShowStep()
        {
            progress.Text = "STEP " + (step + 1) + " OF " + StepNames.Length;
            stepTitle.Text = StepNames[step];
            backButton.Enabled = step > 0;
            nextButton.Text = step == StepNames.Length - 1 ? "Finish setup" : "Continue";
            laterButton.Text = step == StepNames.Length - 1 ? "Later" : "Skip for now";

            for (int i = 0; i < stepLabels.Length; i++)
            {
                stepLabels[i].BackColor = i == step ? Accent : Sidebar;
                stepLabels[i].ForeColor = i == step ? Color.White : Color.FromArgb(180, 201, 209);
                stepLabels[i].Font = new Font("Segoe UI", 9F, i == step ? FontStyle.Bold : FontStyle.Regular);
            }

            content.Controls.Clear();
            FlowLayoutPanel card = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(23, 20, 23, 18)
            };
            content.Controls.Add(card);

            switch (step)
            {
                case 0: BuildWelcome(card); break;
                case 1: BuildArchives(card); break;
                case 2: BuildPdfs(card); break;
                case 3: BuildMetadata(card); break;
                case 4: BuildSafety(card); break;
                case 5: BuildReview(card); break;
            }
        }

        private void BuildWelcome(FlowLayoutPanel card)
        {
            stepDescription.Text = "A few choices make your first archive easier to work with.";
            AddHeading(card, "Welcome to your workspace");
            AddNote(card, "This guide covers how archives open, where PDFs live, optional metadata access, and backup behavior. You can leave optional fields empty and change everything later in Settings.");
            AddHeading(card, "What happens next");
            AddNote(card, "Continue through the steps, then review your choices. Settings are saved only when you finish the guide.");
        }

        private void BuildArchives(FlowLayoutPanel card)
        {
            stepDescription.Text = "Choose what happens when you open a BibTeX archive.";
            AddHeading(card, "Automatic preprocessing");
            ComboBox mode = AddCombo(card, "Run after opening an archive", new[] { "Off", "Fast", "Normal", "Deep" }, draft.AutoPreprocessingMode.ToString());
            mode.SelectedIndexChanged += (sender, args) => draft.AutoPreprocessingMode = (AutoPreprocessingMode)Enum.Parse(typeof(AutoPreprocessingMode), mode.Text);
            AddNote(card, "Fast normalizes DOI and page tags, then pairs PDFs. Normal and Deep also run metadata and key operations. Off leaves records unchanged.");
            AddHeading(card, "Import mode");
            ComboBox import = AddCombo(card, "Default Open/Add mode", new[] { "Normal", "Raw" }, draft.OpenAddMode.ToString());
            import.SelectedIndexChanged += (sender, args) => draft.OpenAddMode = (OpenAddMode)Enum.Parse(typeof(OpenAddMode), import.Text);
            AddNote(card, "Raw opens the original data without post-load preprocessing.");
        }

        private void BuildPdfs(FlowLayoutPanel card)
        {
            stepDescription.Text = "Point the app to the folder that holds your research PDFs.";
            AddHeading(card, "PDF library");
            AddPath(card, "Source PDF folder (optional)", draft.PdfFolder, value => draft.PdfFolder = value);
            AddCheck(card, "Search PDF subfolders", draft.RecursivePdfSearch, value => draft.RecursivePdfSearch = value);
            AddNote(card, "You can set the folder later. PDF pairing uses this location when available.");
        }

        private void BuildMetadata(FlowLayoutPanel card)
        {
            stepDescription.Text = "Set optional details for metadata and Journal Citation Reports.";
            AddHeading(card, "Metadata services");
            AddText(card, "Contact email (optional)", draft.MetadataContactEmail, false, value => draft.MetadataContactEmail = value);
            AddNote(card, "A contact email helps identify your requests to metadata providers.");
            AddText(card, "JCR API key (optional)", draft.JcrApiKey, true, value => draft.JcrApiKey = value);
            AddNote(card, "Leave the key blank if you do not use Journal Citation Reports.");
        }

        private void BuildSafety(FlowLayoutPanel card)
        {
            stepDescription.Text = "Decide how the app protects your work.";
            AddHeading(card, "Automatic backups");
            AddCheck(card, "Create backup snapshots after changes", draft.AllowBackup, value => draft.AllowBackup = value);
            AddPath(card, "Backup folder", draft.BackupFolder, value => draft.BackupFolder = value);
            AddHeading(card, "Save and close warnings");
            AddCheck(card, "Ask before overwriting the current archive", !draft.SaveWithoutApprove, value => draft.SaveWithoutApprove = !value);
            AddCheck(card, "Ask what to do with unsaved changes on close", !draft.UnsafeClosing, value => draft.UnsafeClosing = !value);
        }

        private void BuildReview(FlowLayoutPanel card)
        {
            stepDescription.Text = "Check your choices before saving them.";
            AddHeading(card, "Your setup");
            AddNote(card, "Archive preprocessing: " + draft.AutoPreprocessingMode + "\r\nImport mode: " + draft.OpenAddMode
                + "\r\nPDF folder: " + DisplayPath(draft.PdfFolder)
                + "\r\nSearch PDF subfolders: " + YesNo(draft.RecursivePdfSearch)
                + "\r\nMetadata email: " + (string.IsNullOrWhiteSpace(draft.MetadataContactEmail) ? "Not set" : draft.MetadataContactEmail)
                + "\r\nJCR API key: " + (string.IsNullOrWhiteSpace(draft.JcrApiKey) ? "Not set" : "Set")
                + "\r\nAutomatic backups: " + YesNo(draft.AllowBackup)
                + "\r\nBackup folder: " + DisplayPath(draft.BackupFolder)
                + "\r\nSave warning: " + YesNo(!draft.SaveWithoutApprove)
                + "\r\nUnsaved changes warning: " + YesNo(!draft.UnsafeClosing));
            AddNote(card, "Finish saves these settings. You can run this guide again from Project > Settings > First Time Use.");
        }

        private void NextButton_Click(object sender, EventArgs e)
        {
            if (step < StepNames.Length - 1)
            {
                step++;
                ShowStep();
                return;
            }

            if (draft.AllowBackup && string.IsNullOrWhiteSpace(draft.BackupFolder))
            {
                MessageBox.Show(this, "Choose a backup folder or turn off automatic backups before finishing.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                step = 4;
                ShowStep();
                return;
            }

            if (!string.IsNullOrWhiteSpace(draft.PdfFolder) && !Directory.Exists(draft.PdfFolder))
            {
                MessageBox.Show(this, "The PDF folder does not exist. Choose an existing folder or leave it empty.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                step = 2;
                ShowStep();
                return;
            }

            AppSettingsData previousSettings = Program.AppSettings.Data;
            try
            {
                if (draft.AllowBackup)
                    Directory.CreateDirectory(draft.BackupFolder);
                draft.FirstTimeUseCompleted = true;
                Program.AppSettings.Data = draft;
                Program.AppSettings.SaveSettings("First Time Use setup");
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Program.AppSettings.Data = previousSettings;
                MessageBox.Show(this, "Could not save setup: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void AddHeading(FlowLayoutPanel card, string title)
        {
            card.Controls.Add(new Label { Text = title, ForeColor = Ink, Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold), Size = new Size(495, 34), Margin = new Padding(0, 2, 0, 3) });
        }

        private static void AddNote(FlowLayoutPanel card, string text)
        {
            card.Controls.Add(new Label { Text = text, ForeColor = Muted, Font = new Font("Segoe UI", 9F), Size = new Size(495, Math.Max(48, 23 * (text.Split('\n').Length + 2))), Margin = new Padding(0, 0, 0, 12) });
        }

        private static ComboBox AddCombo(FlowLayoutPanel card, string label, string[] choices, string selected)
        {
            AddFieldLabel(card, label);
            ComboBox combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 335, Height = 30, Margin = new Padding(0, 0, 0, 12) };
            combo.Items.AddRange(choices);
            combo.SelectedItem = selected;
            if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
            card.Controls.Add(combo);
            return combo;
        }

        private static void AddText(FlowLayoutPanel card, string label, string value, bool secret, Action<string> changed)
        {
            AddFieldLabel(card, label);
            TextBox box = new TextBox { Text = value ?? string.Empty, Width = 470, UseSystemPasswordChar = secret, Margin = new Padding(0, 0, 0, 12) };
            box.TextChanged += (sender, args) => changed(box.Text.Trim());
            card.Controls.Add(box);
        }

        private void AddPath(FlowLayoutPanel card, string label, string value, Action<string> changed)
        {
            AddFieldLabel(card, label);
            FlowLayoutPanel row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Size = new Size(495, 40), Margin = new Padding(0, 0, 0, 12) };
            TextBox box = new TextBox { Text = value ?? string.Empty, Width = 380, Margin = new Padding(0, 4, 8, 0) };
            box.TextChanged += (sender, args) => changed(box.Text.Trim());
            Button browse = new Button { Text = "Browse", Size = new Size(85, 30), Margin = new Padding(0) };
            browse.Click += (sender, args) =>
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = label;
                    if (Directory.Exists(box.Text)) dialog.SelectedPath = box.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.SelectedPath;
                }
            };
            row.Controls.Add(box);
            row.Controls.Add(browse);
            card.Controls.Add(row);
        }

        private static void AddCheck(FlowLayoutPanel card, string label, bool value, Action<bool> changed)
        {
            CheckBox check = new CheckBox { Text = label, Checked = value, ForeColor = Ink, Size = new Size(495, 32), Margin = new Padding(0, 0, 0, 9) };
            check.CheckedChanged += (sender, args) => changed(check.Checked);
            card.Controls.Add(check);
        }

        private static void AddFieldLabel(FlowLayoutPanel card, string text)
        {
            card.Controls.Add(new Label { Text = text, ForeColor = Ink, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Size = new Size(495, 25), Margin = new Padding(0, 0, 0, 0) });
        }

        private static string DisplayPath(string path) => string.IsNullOrWhiteSpace(path) ? "Not set" : path;
        private static string YesNo(bool value) => value ? "Yes" : "No";
    }
}
