using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public class RenameTagForm : Form
    {
        private readonly ComboBox _cmbSourceTag;
        private readonly TextBox _txtNewTagName;

        public RenameTagForm(IEnumerable<string> availableTags, string selectedTagName = null)
        {
            if (availableTags == null)
                throw new ArgumentNullException(nameof(availableTags));

            Text = "Rename Tag";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(460, 150);

            Label lblSource = new Label
            {
                AutoSize = true,
                Text = "Original tag",
                Margin = new Padding(0, 0, 0, 6)
            };

            _cmbSourceTag = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Margin = new Padding(0, 0, 0, 12)
            };

            string[] tagArray = availableTags
                .Where(tag => string.IsNullOrWhiteSpace(tag) == false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _cmbSourceTag.Items.AddRange(tagArray.Cast<object>().ToArray());

            Label lblTarget = new Label
            {
                AutoSize = true,
                Text = "New tag name",
                Margin = new Padding(0, 0, 0, 6)
            };

            _txtNewTagName = new TextBox
            {
                Dock = DockStyle.Top
            };

            Button btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 90
            };

            Button btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90
            };

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnOk);

            TableLayoutPanel layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 5,
                Dock = DockStyle.Fill,
                Padding = new Padding(16)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(lblSource, 0, 0);
            layout.Controls.Add(_cmbSourceTag, 0, 1);
            layout.Controls.Add(lblTarget, 0, 2);
            layout.Controls.Add(_txtNewTagName, 0, 3);
            layout.Controls.Add(buttons, 0, 4);

            Controls.Add(layout);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            _cmbSourceTag.TextChanged += cmbSourceTag_TextChanged;

            if (string.IsNullOrWhiteSpace(selectedTagName) == false)
                _cmbSourceTag.Text = selectedTagName;
            else if (tagArray.Length > 0)
                _cmbSourceTag.SelectedIndex = 0;

            UpdateDefaultTargetName();
            Shown += RenameTagForm_Shown;
        }

        public string SourceTagName => (_cmbSourceTag.Text ?? string.Empty).Trim();

        public string NewTagName => (_txtNewTagName.Text ?? string.Empty).Trim();

        private void RenameTagForm_Shown(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SourceTagName))
                _cmbSourceTag.Focus();
            else
            {
                _txtNewTagName.Focus();
                _txtNewTagName.SelectAll();
            }
        }

        private void cmbSourceTag_TextChanged(object sender, EventArgs e)
        {
            UpdateDefaultTargetName();
        }

        private void UpdateDefaultTargetName()
        {
            string sourceTagName = SourceTagName;
            if (string.IsNullOrWhiteSpace(sourceTagName))
                return;

            string defaultTarget = sourceTagName + "_copy";
            if (string.IsNullOrWhiteSpace(_txtNewTagName.Text) ||
                _txtNewTagName.Text.EndsWith("_copy", StringComparison.OrdinalIgnoreCase))
            {
                _txtNewTagName.Text = defaultTarget;
            }
        }
    }
}
