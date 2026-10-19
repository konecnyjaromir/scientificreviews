using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private ContextMenuStrip _columnContextMenu;
        private ToolStripMenuItem _editColumnValuesMenuItem;
        private string _contextColumnName;

        private void InitializeColumnEditingUi()
        {
            var editColumns = new ToolStripMenuItem("Columns merge/copy/replace");
            editColumns.Click += (sender, args) => EditColumnValues();
            viewToolStripMenuItem.DropDownItems.Add(editColumns);

            // Keep the menu alive while WinForms finishes its close/click processing.
            // The form's component container disposes it with the main window.
            _columnContextMenu = new ContextMenuStrip(components);
            _editColumnValuesMenuItem = new ToolStripMenuItem("Columns merge/copy/replace");
            _editColumnValuesMenuItem.Click += (sender, args) => EditColumnValues(_contextColumnName);
            _columnContextMenu.Items.Add(_editColumnValuesMenuItem);
        }

        private void EditColumnValues(string initialColumn = null)
        {
            if (IsBlockingUiActive || entries.Count == 0) return;
            CancelPendingSearch();
            // Resolve the current query synchronously so Visible always matches the search box.
            RefreshGrid();
            BibtexEntry[] visible = dataGridView1.Rows.Cast<DataGridViewRow>()
                .Select(row => (row.DataBoundItem as DataRowView)?.Row["Entry"] as BibtexEntry)
                .Where(entry => entry != null).ToArray();
            using (var dialog = new ColumnEditForm(GetOrderedTagKeys(entries), entries.ToArray(), visible,
                GetSelectedOrdered(), SearchCaseSensitive, initialColumn))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Changes.Count == 0) return;
                ApplyColumnChanges(dialog.Changes);
            }
        }

        private void ApplyColumnChanges(System.Collections.Generic.IReadOnlyList<ColumnEditChange> changes)
        {
            if (changes.Count == 0 || IsBlockingUiActive) return;
            BibtexEntry[] selected = GetSelectedOrdered();
            foreach (ColumnEditChange change in changes)
                change.Entry.Tags = change.Tags;
            Changed();
            RefreshGrid(selected, statusMessage: $"Column values updated in {changes.Count} record(s). Use Ctrl+Z to undo.");
        }
    }
}
