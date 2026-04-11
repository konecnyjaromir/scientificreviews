using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private void addTag_Click(object sender, EventArgs e)
        {
            AddTag();
        }

        private void AddTag()
        {
            DataRowView drv = (DataRowView)bindingSource1.Current;
            if (drv == null)
                return;

            if (drv.Row != null)
            {
                BibtexEntry entry = (BibtexEntry)drv.Row["Entry"];

                InputGridForm frm = new InputGridForm();
                frm.Object = new BibtexTag();
                if (frm.ShowDialog(this) == DialogResult.OK)
                {
                    BibtexTag newTag = frm.Object as BibtexTag;
                    if (string.IsNullOrEmpty(newTag.Key))
                    {
                        lblStatus.Text = "Key should not be empty!";
                        return;
                    }

                    List<BibtexTag> list = entry.Tags.ToList();
                    list.Add(newTag);
                    entry.Tags = list.ToArray();
                    lblStatus.Text = string.Empty;
                    RefreshGrid(new[] { entry });
                }
            }
        }

        private void AddTagToSelected()
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return;

            InputGridForm frm = new InputGridForm();
            frm.Object = new BibtexTag();

            if (frm.ShowDialog(this) != DialogResult.OK)
                return;

            BibtexTag newTag = frm.Object as BibtexTag;

            if (string.IsNullOrEmpty(newTag.Key))
            {
                lblStatus.Text = "Key should not be empty!";
                return;
            }

            foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows)
            {
                if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                {
                    BibtexEntry entry = (BibtexEntry)drv.Row["Entry"];
                    if (entry != null)
                    {
                        List<BibtexTag> list = entry.Tags.ToList();
                        list.Add(newTag.DeepClone());
                        entry.Tags = list.ToArray();
                    }
                }
            }

            lblStatus.Text = string.Empty;
            RefreshGrid(GetSelectedOrdered());
        }

        private void addTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddTag();
        }

        private void removeTagsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            RemoveTags();
        }

        private void RemoveTags()
        {
            BibtexEntry[] selectedEntries = GetSelectedOrdered();
            if (selectedEntries.Length == 0 && bindingSource1.Current is DataRowView currentView && currentView.Row != null)
            {
                BibtexEntry currentEntry = currentView.Row["Entry"] as BibtexEntry;
                if (currentEntry != null)
                    selectedEntries = new[] { currentEntry };
            }

            if (selectedEntries.Length == 0)
                return;

            List<string> tags = new List<string>();
            foreach (BibtexEntry selectedEntry in selectedEntries)
            {
                foreach (BibtexTag item in selectedEntry.Tags ?? Array.Empty<BibtexTag>())
                {
                    if (string.IsNullOrWhiteSpace(item?.Key) == false && tags.Contains(item.Key) == false)
                        tags.Add(item.Key);
                }
            }

            if (tags.Count == 0)
                return;

            SelectForm frm = new SelectForm();
            frm.SetData(tags.ToArray());
            frm.SetSelection(tags.ToArray());
            if (frm.ShowDialog(this) != DialogResult.OK)
                return;

            HashSet<string> tagsToLeave = new HashSet<string>(frm.GetSelected(), StringComparer.Ordinal);
            foreach (BibtexEntry entry in selectedEntries)
            {
                entry.Tags = (entry.Tags ?? Array.Empty<BibtexTag>())
                    .Where(tag => tag != null && tagsToLeave.Contains(tag.Key))
                    .ToArray();
            }

            RefreshGrid(selectedEntries);
            Changed();
        }

        private void allowEditToolStripMenuItem_Click(object sender, EventArgs e)
        {
            allowEditToolStripMenuItem.Checked = !allowEditToolStripMenuItem.Checked;
            propertyGrid1.Enabled = allowEditToolStripMenuItem.Checked;
            SelectEntry();
        }

        private void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (propertyGrid1.SelectedObject is CustomClass == false)
                return;

            if (propertyGrid1.Tag is BibtexEntry entry == false)
                return;

            string name = e.ChangedItem.PropertyDescriptor.Name;
            object newValue = e.ChangedItem.Value;

            if (name == "entryKey")
                entry.Key = newValue?.ToString();
            else if (name == "entryType")
                entry.Type = newValue?.ToString();
            else
            {
                foreach (BibtexTag tag in entry.Tags)
                {
                    if (tag.Key == name)
                        tag.Value = newValue?.ToString();
                }
            }

            DataRowView drv = (DataRowView)bindingSource1.Current;
            drv.Row["Entry"] = entry;

            RefreshGrid(new[] { entry });
            Changed();
        }

        private void addTagToSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddTagToSelected();
        }

        private void deleteSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RemoveSelectedRecords();
        }

        private void btnDeleteTag_Click(object sender, EventArgs e)
        {
            RemoveTag();
        }

        private void RemoveTag()
        {
            bool readOnly = !allowEditToolStripMenuItem.Checked;

            if (propertyGrid1.Tag is BibtexEntry entry == false)
                return;

            GridItem gi = propertyGrid1.SelectedGridItem;
            if (gi == null || gi.GridItemType != GridItemType.Property)
                return;

            string name = gi.PropertyDescriptor?.Name ?? gi.Label;
            if (string.IsNullOrWhiteSpace(name))
                return;

            if (string.Equals(name, "entryKey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "entryType", StringComparison.OrdinalIgnoreCase))
                return;

            List<BibtexTag> list = entry.Tags.ToList();
            int removed = list.RemoveAll(t => string.Equals(t.Key, name, StringComparison.Ordinal));
            entry.Tags = list.ToArray();

            if (removed == 0)
                return;

            DataRowView drv = (DataRowView)bindingSource1.Current;
            if (drv.Row != null)
            {
                entry = (BibtexEntry)drv.Row["Entry"];
                ShowEntry(entry, txtSearch.Text);

                CustomClass customClass = new CustomClass();
                customClass.Add(new CustomProperty("entryKey", "Key", entry.Key, "Bibitem", readOnly, true));
                customClass.Add(new CustomProperty("entryType", "Type", entry.Type, "Bibitem", readOnly, true));
                foreach (BibtexTag tag in entry.Tags)
                {
                    CustomProperty item = new CustomProperty(tag.Key, tag.Key, tag.Value, "Parameters", readOnly, true);
                    customClass.Add(item);
                }

                propertyGrid1.Tag = entry;
                propertyGrid1.SelectedObject = customClass;
            }

            lblSelected.Text = $"({dataGridView1.SelectedRows.Count})";
            RefreshGrid(new[] { entry });
            Changed();
        }

        private void btnRemoveTags_Click(object sender, EventArgs e)
        {
            RemoveTags();
        }

        private void removeTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RemoveTag();
        }

        private void selectedDOIsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelected("doi");
        }

        private void selectedKeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelected("key");
        }

        private void selectedTitlesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelected("title");
        }

        private void CopySelected(string tagName)
        {
            List<string> parts = new List<string>();

            foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows)
            {
                if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                {
                    BibtexEntry entry = (BibtexEntry)drv.Row["Entry"];
                    if (entry == null)
                        continue;

                    if (string.Equals(tagName, "key", StringComparison.OrdinalIgnoreCase))
                    {
                        string k = (entry.Key ?? string.Empty).Trim();
                        if (k.Length > 0)
                            parts.Add(k);

                        continue;
                    }

                    if (entry.Tags != null)
                    {
                        foreach (BibtexTag tag in entry.Tags)
                        {
                            if (string.Equals(tag.Key, tagName, StringComparison.OrdinalIgnoreCase))
                            {
                                string value = (tag.Value ?? string.Empty).Trim();
                                if (value.Length > 0)
                                    parts.Add(value);
                                break;
                            }
                        }
                    }
                }
            }

            Clipboard.SetText(string.Join(",", parts));
            lblStatus.Text = "Copied to clipboard";
        }

        private void btnAddorEditTag_Click(object sender, EventArgs e)
        {
            AddOrEditTagToSelected();
        }

        private void AddOrEditTagToSelected()
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return;

            string key = (txtKey.Text ?? string.Empty).Trim();
            string value = (txtValue.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                lblStatus.Text = "Key should not be empty!";
                return;
            }

            if (string.Equals(key, "key", StringComparison.OrdinalIgnoreCase))
            {
                lblStatus.Text = "\"key\" is reserved (BibTeX entry key). Choose another tag name.";
                return;
            }

            Regex tagKeyRegex = new Regex(@"^[A-Za-z][A-Za-z0-9_\-:]*$", RegexOptions.Compiled);
            if (tagKeyRegex.IsMatch(key) == false)
            {
                lblStatus.Text = "Invalid tag key. Use letters/digits and _ - : (must start with a letter).";
                return;
            }

            foreach (DataGridViewRow dgvr in dataGridView1.SelectedRows)
            {
                if (dgvr.DataBoundItem is DataRowView drv && drv.Row != null)
                {
                    BibtexEntry entry = (BibtexEntry)drv.Row["Entry"];
                    if (entry != null)
                    {
                        List<BibtexTag> list = (entry.Tags ?? Array.Empty<BibtexTag>()).ToList();
                        int idx = list.FindIndex(t => t != null &&
                                                     string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0)
                            list[idx].Value = value;
                        else
                            list.Add(new BibtexTag { Key = key, Value = value });

                        entry.Tags = list.ToArray();
                    }
                }
            }

            lblStatus.Text = string.Empty;
            RefreshGrid(GetSelectedOrdered());
        }
    }
}
