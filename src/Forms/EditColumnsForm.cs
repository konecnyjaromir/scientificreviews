using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class EditColumnsForm : Form
    {
        private readonly BindingList<string> _columns = new BindingList<string>();
        private int _dragIndex = -1;

        public EditColumnsForm()
        {
            InitializeComponent();

            // Bind list
            lstColumns.DataSource = _columns;

            // Enable drag & drop reorder
            lstColumns.AllowDrop = true;
            lstColumns.DragOver += lstColumns_DragOver;
            lstColumns.DragDrop += lstColumns_DragDrop;
        }

        /// <summary>
        /// Input: current columns
        /// </summary>
        public void SetColumns(string[] columns)
        {
            _columns.RaiseListChangedEvents = false;
            try
            {
                _columns.Clear();
                if (columns == null) return;

                foreach (var c in columns)
                {
                    var v = (c ?? string.Empty).Trim();
                    if (v.Length == 0) continue;
                    if (_columns.Contains(v)) continue; // keep unique
                    _columns.Add(v);
                }
            }
            finally
            {
                _columns.RaiseListChangedEvents = true;
                _columns.ResetBindings();
            }

            if (_columns.Count > 0)
                lstColumns.SelectedIndex = 0;
        }

        /// <summary>
        /// Output: edited columns
        /// </summary>
        public string[] GetColumns()
        {
            return _columns
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => x.Length > 0)
                .ToArray();
        }

        // --- Optional compatibility with your old InputBoxForm usage (CSV) ---
        public void SetText(string csv)
        {
            var arr = (csv ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();

            SetColumns(arr);
        }

        public string GetText()
        {
            return string.Join(",", GetColumns());
        }
        // -------------------------------------------------------------------

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddFromTextbox();
        }

        private void txtNew_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                AddFromTextbox();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void AddFromTextbox()
        {
            var value = (txtNew.Text ?? string.Empty).Trim();
            if (value.Length == 0) return;

            if (_columns.Contains(value))
            {
                MessageBox.Show(this, "This column already exists.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtNew.SelectAll();
                txtNew.Focus();
                return;
            }

            _columns.Add(value);
            lstColumns.SelectedIndex = _columns.Count - 1;

            txtNew.Clear();
            txtNew.Focus();
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditSelected();
        }

        private void lstColumns_DoubleClick(object sender, EventArgs e)
        {
            EditSelected();
        }

        private void EditSelected()
        {
            int i = lstColumns.SelectedIndex;
            if (i < 0) return;

            string current = _columns[i];
            string edited = Prompt.ShowDialog(this, "Edit column name:", "Edit Column", current);
            if (edited == null) return; // cancelled

            edited = edited.Trim();
            if (edited.Length == 0) return;

            if (!string.Equals(current, edited, StringComparison.Ordinal) && _columns.Contains(edited))
            {
                MessageBox.Show(this, "This column already exists.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _columns[i] = edited;
            lstColumns.SelectedIndex = i;
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            RemoveSelected();
        }

        private void RemoveSelected()
        {
            int i = lstColumns.SelectedIndex;
            if (i < 0) return;

            _columns.RemoveAt(i);
            if (_columns.Count == 0) return;

            if (i >= _columns.Count) i = _columns.Count - 1;
            lstColumns.SelectedIndex = i;
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            MoveSelected(-1);
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            MoveSelected(+1);
        }

        private void MoveSelected(int delta)
        {
            int i = lstColumns.SelectedIndex;
            if (i < 0) return;

            int j = i + delta;
            if (j < 0 || j >= _columns.Count) return;

            string tmp = _columns[i];
            _columns[i] = _columns[j];
            _columns[j] = tmp;

            lstColumns.SelectedIndex = j;
        }

        private void lstColumns_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                RemoveSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                EditSelected();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Up)
            {
                MoveSelected(-1);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Down)
            {
                MoveSelected(+1);
                e.Handled = true;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Safety: ensure there is at least one column? (Optional)
            // if (_columns.Count == 0) { MessageBox.Show(...); this.DialogResult = DialogResult.None; }
        }

        // --- Drag & Drop reorder ---
        private void lstColumns_MouseDown(object sender, MouseEventArgs e)
        {
            _dragIndex = lstColumns.IndexFromPoint(e.Location);
            if (_dragIndex >= 0 && _dragIndex < _columns.Count)
            {
                lstColumns.DoDragDrop(_columns[_dragIndex], DragDropEffects.Move);
            }
        }

        private void lstColumns_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(string)))
                e.Effect = DragDropEffects.Move;
            else
                e.Effect = DragDropEffects.None;
        }

        private void lstColumns_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(string))) return;
            if (_dragIndex < 0 || _dragIndex >= _columns.Count) return;

            var clientPoint = lstColumns.PointToClient(new System.Drawing.Point(e.X, e.Y));
            int dropIndex = lstColumns.IndexFromPoint(clientPoint);
            if (dropIndex < 0) dropIndex = _columns.Count - 1;

            if (dropIndex == _dragIndex) return;

            string item = _columns[_dragIndex];
            _columns.RemoveAt(_dragIndex);

            if (dropIndex > _dragIndex) dropIndex--; // list shrank above the drop position

            _columns.Insert(dropIndex, item);
            lstColumns.SelectedIndex = dropIndex;
        }
        // ---------------------------

        /// <summary>
        /// Tiny prompt dialog for editing text (no extra form file needed).
        /// Returns null when cancelled.
        /// </summary>
        private static class Prompt
        {
            public static string ShowDialog(IWin32Window owner, string text, string caption, string defaultValue)
            {
                using (var form = new Form())
                using (var lbl = new Label())
                using (var tb = new TextBox())
                using (var ok = new Button())
                using (var cancel = new Button())
                {
                    form.Text = caption;
                    form.FormBorderStyle = FormBorderStyle.FixedDialog;
                    form.StartPosition = FormStartPosition.CenterParent;
                    form.MinimizeBox = false;
                    form.MaximizeBox = false;
                    form.ShowInTaskbar = false;
                    form.ClientSize = new System.Drawing.Size(420, 120);

                    lbl.AutoSize = true;
                    lbl.Text = text;
                    lbl.Left = 12;
                    lbl.Top = 12;

                    tb.Left = 12;
                    tb.Top = 35;
                    tb.Width = 396;
                    tb.Text = defaultValue ?? string.Empty;

                    ok.Text = "OK";
                    ok.DialogResult = DialogResult.OK;
                    ok.Left = 242;
                    ok.Top = 75;
                    ok.Width = 80;

                    cancel.Text = "Cancel";
                    cancel.DialogResult = DialogResult.Cancel;
                    cancel.Left = 328;
                    cancel.Top = 75;
                    cancel.Width = 80;

                    form.AcceptButton = ok;
                    form.CancelButton = cancel;

                    form.Controls.Add(lbl);
                    form.Controls.Add(tb);
                    form.Controls.Add(ok);
                    form.Controls.Add(cancel);

                    var result = form.ShowDialog(owner);
                    return result == DialogResult.OK ? tb.Text : null;
                }
            }
        }
    }
}
