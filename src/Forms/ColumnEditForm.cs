using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public sealed class ColumnEditForm : Form
    {
        private readonly BibtexEntry[][] _scopes;
        private readonly ComboBox _operation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _scope = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _target = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
        private readonly ComboBox _source = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _separator = new TextBox { Text = "; " };
        private readonly TextBox _find = new TextBox();
        private readonly TextBox _value = new TextBox();
        private readonly CheckBox _matchCase = new CheckBox { Text = "Match case", AutoSize = true };
        private readonly CheckBox _removeSource = new CheckBox { Text = "Remove source column after merging", AutoSize = true };
        private readonly Label _hint = new Label { AutoSize = true, MaximumSize = new Size(630, 0) };
        private readonly TextBox _preview = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
        private readonly Button _apply = new Button { Text = "Apply", AutoSize = true, Enabled = false, DialogResult = DialogResult.OK };

        public List<ColumnEditChange> Changes { get; private set; } = new List<ColumnEditChange>();

        public ColumnEditForm(IEnumerable<string> columns, BibtexEntry[] all, BibtexEntry[] visible,
            BibtexEntry[] selected, bool caseSensitive, string initialColumn = null)
        {
            Text = "Columns merge/copy/replace";
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(690, 620);
            MinimumSize = new Size(640, 610);
            _scopes = new[] { all, visible, selected };
            _matchCase.Checked = caseSensitive;

            string[] tags = columns.Where(ColumnEditService.IsEditableColumn).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            _target.Items.AddRange(tags);
            _source.Items.AddRange(tags);
            _target.Text = initialColumn ?? tags.FirstOrDefault() ?? "";
            _source.SelectedIndex = Array.FindIndex(tags, tag => !string.Equals(tag, _target.Text, StringComparison.OrdinalIgnoreCase));
            _operation.Items.AddRange(new object[] { "Merge columns", "Copy column (overwrite target)", "Set all values", "Find and replace text", "Remove column values" });
            _operation.SelectedIndex = 0;
            _scope.Items.AddRange(new object[] { $"All records ({all.Length})", $"Visible records ({visible.Length})", $"Selected records ({selected.Length})" });
            _scope.SelectedIndex = 0;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 12 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddRow(layout, 0, "Operation", _operation);
            AddRow(layout, 1, "Apply to", _scope);
            AddRow(layout, 2, "Target column", _target);
            AddRow(layout, 3, "Source column", _source);
            AddRow(layout, 4, "Merge separator", _separator);
            AddRow(layout, 5, "Find text", _find);
            AddRow(layout, 6, "New / replacement value", _value);
            var switches = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            switches.Controls.AddRange(new Control[] { _matchCase, _removeSource });
            layout.Controls.Add(switches, 0, 7);
            layout.SetColumnSpan(switches, 2);
            layout.Controls.Add(_hint, 0, 8);
            layout.SetColumnSpan(_hint, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = "Preview (first 5 changed records)", AutoSize = true, Margin = new Padding(0, 10, 0, 5) }, 0, 9);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, 9), 2);
            layout.Controls.Add(_preview, 0, 10);
            layout.SetColumnSpan(_preview, 2);
            while (layout.RowStyles.Count < 10) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var previewButton = new Button { Text = "Preview", AutoSize = true };
            previewButton.Click += (sender, args) => PreviewChanges();
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            buttons.Controls.AddRange(new Control[] { cancel, _apply, previewButton });
            layout.Controls.Add(buttons, 0, 11);
            layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);
            AcceptButton = previewButton;
            CancelButton = cancel;

            foreach (Control control in new Control[] { _operation, _scope, _target, _source, _separator, _find, _value })
                control.TextChanged += (sender, args) => InvalidatePreview();
            _matchCase.CheckedChanged += (sender, args) => InvalidatePreview();
            _removeSource.CheckedChanged += (sender, args) => InvalidatePreview();
            _operation.SelectedIndexChanged += (sender, args) => UpdateOperationUi();
            UpdateOperationUi();
        }

        private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 5, 4, 5) }, 0, row);
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 3, 0, 3);
            layout.Controls.Add(control, 1, row);
        }

        private void InvalidatePreview()
        {
            Changes.Clear();
            _apply.Enabled = false;
            _preview.Text = "Choose options and click Preview. Changes can be undone with Ctrl+Z.";
        }

        private void UpdateOperationUi()
        {
            ColumnEditOperation operation = (ColumnEditOperation)_operation.SelectedIndex;
            _source.Enabled = operation == ColumnEditOperation.Merge || operation == ColumnEditOperation.Copy;
            _separator.Enabled = _removeSource.Enabled = operation == ColumnEditOperation.Merge;
            _find.Enabled = _matchCase.Enabled = operation == ColumnEditOperation.ReplaceText;
            _value.Enabled = operation == ColumnEditOperation.SetValue || operation == ColumnEditOperation.ReplaceText;
            string[] hints =
            {
                "Append non-empty source values to the target using the separator. Identical values are included once. The source is kept unless removal is checked.",
                "Overwrite target values with the first source value. Records without the source tag are skipped; empty source values overwrite the target with an empty value.",
                "Overwrite every target value in this scope. Missing tags are created. An empty value creates an empty tag; use Remove to delete tags.",
                "Replace all literal occurrences in existing target values. An empty replacement deletes the matching text. Missing tags are skipped.",
                "Delete all occurrences of this tag from records in this scope."
            };
            _hint.Text = hints[(int)operation];
            InvalidatePreview();
        }

        private void PreviewChanges()
        {
            _apply.Enabled = false;
            try
            {
                var options = new ColumnEditOptions
                {
                    Operation = (ColumnEditOperation)_operation.SelectedIndex,
                    Target = _target.Text.Trim(), Source = _source.Text.Trim(),
                    Separator = _separator.Text, Find = _find.Text, Value = _value.Text,
                    CaseSensitive = _matchCase.Checked, RemoveSource = _removeSource.Checked
                };
                Changes = ColumnEditService.Plan(_scopes[_scope.SelectedIndex], options);
                string[] samples = Changes.Take(5).Select(change =>
                {
                    var after = new BibtexEntry { Tags = change.Tags };
                    string beforeValue = DescribeValue(change.Entry, options.Target);
                    string afterValue = DescribeValue(after, options.Target);
                    string sample = $"{change.Entry.Key}: {options.Target}\r\n  Before: {beforeValue}\r\n  After: {afterValue}";
                    if (options.Operation == ColumnEditOperation.Merge && options.RemoveSource)
                        sample += $"\r\n  Source '{options.Source}' will be removed.";
                    return sample;
                }).ToArray();
                _preview.Text = $"{Changes.Count} record(s) will change in {_scope.Text}.\r\n\r\n" + string.Join("\r\n\r\n", samples);
                _apply.Enabled = Changes.Count > 0;
            }
            catch (ArgumentException ex)
            {
                Changes.Clear();
                _preview.Text = ex.Message;
            }
        }

        private static string DescribeValue(BibtexEntry entry, string target)
        {
            string[] values = (entry.Tags ?? Array.Empty<BibtexTag>())
                .Where(tag => tag != null && string.Equals(tag.Key, target, StringComparison.OrdinalIgnoreCase))
                .Select(tag => tag.Value == null ? "(null)" : tag.Value.Length == 0 ? "(empty)" : tag.Value).ToArray();
            string text = values.Length == 0 ? "(missing)" : string.Join(" | ", values);
            return text.Length > 300 ? text.Substring(0, 300) + "..." : text;
        }
    }
}
