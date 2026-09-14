using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private const string SmartSearchErrorPrefix = "Invalid smart search query:";

        private CheckBox _searchModeCheckBox;
        private ToolStripControlHost _searchModeCheckBoxHost;

        private bool UseSmartSearch
        {
            get => Program.AppSettings?.Data?.UseSmartSearch ?? true;
            set
            {
                if (Program.AppSettings?.Data != null)
                    Program.AppSettings.Data.UseSmartSearch = value;
            }
        }

        private void InitializeSearchUi()
        {
            _searchModeCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "Smart search",
                Checked = UseSmartSearch
            };
            _searchModeCheckBox.CheckedChanged += searchModeCheckBox_CheckedChanged;

            _searchModeCheckBoxHost = new ToolStripControlHost(_searchModeCheckBox)
            {
                AutoSize = false,
                Width = 105
            };

            int searchIndex = toolStrip1.Items.IndexOf(txtSearch);
            if (searchIndex >= 0)
                toolStrip1.Items.Insert(searchIndex + 1, _searchModeCheckBoxHost);
            else
                toolStrip1.Items.Add(_searchModeCheckBoxHost);

            UpdateSearchModeUi();
        }

        private BibtexEntry[] ApplySearchFilter(IEnumerable<BibtexEntry> sourceEntries, string search, out string validationMessage)
        {
            BibtexEntry[] candidates = (sourceEntries ?? Array.Empty<BibtexEntry>())
                .Where(entry => entry != null)
                .ToArray();

            validationMessage = null;
            if (string.IsNullOrWhiteSpace(search))
                return candidates;

            if (!UseSmartSearch)
                return ApplyClassicSearch(candidates, search);

            SmartSearchParseResult parseResult = SmartSearchFilter.TryParse(search);
            if (!parseResult.Success)
            {
                validationMessage = $"{SmartSearchErrorPrefix} {parseResult.ErrorMessage}";
                return candidates;
            }

            return candidates.Where(parseResult.Filter.IsMatch).ToArray();
        }

        private BibtexEntry[] ApplyClassicSearch(IEnumerable<BibtexEntry> sourceEntries, string search)
        {
            string normalizedSearch = (search ?? string.Empty).ToLowerInvariant();
            string[] searchTerms = normalizedSearch
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();

            if (searchTerms.Length == 0)
                return sourceEntries.ToArray();

            List<BibtexEntry> filtered = new List<BibtexEntry>();
            foreach (string term in searchTerms)
            {
                foreach (BibtexEntry entry in sourceEntries)
                {
                    string serializedEntry = bibtexExporter.EntryToString(entry) ?? string.Empty;
                    if (serializedEntry.ToLowerInvariant().Contains(term) && filtered.Contains(entry) == false)
                        filtered.Add(entry);
                }
            }

            return filtered.ToArray();
        }

        private void UpdateSearchModeUi()
        {
            if (_searchModeCheckBox == null)
                return;

            if (_searchModeCheckBox.Checked != UseSmartSearch)
                _searchModeCheckBox.Checked = UseSmartSearch;

            if (UseSmartSearch)
            {
                _searchModeCheckBoxHost.ToolTipText = "Smart search is active. Uncheck to switch to classic full-text search.";
                txtSearch.ToolTipText = "Examples: title:\"machine learning\" AND author:novak, year:2020-2025, year>2025 OR doi:10.1000/xyz";
            }
            else
            {
                _searchModeCheckBoxHost.ToolTipText = "Classic full-text search is active. Check to switch to smart query search.";
                txtSearch.ToolTipText = "Classic mode searches the whole record. Separate values by comma for OR.";
            }
        }

        private void UpdateSearchValidationStatus(string preferredStatus = null)
        {
            if (UseSmartSearch && string.IsNullOrWhiteSpace(txtSearch.Text) == false)
            {
                SmartSearchParseResult parseResult = SmartSearchFilter.TryParse(txtSearch.Text);
                if (!parseResult.Success)
                {
                    lblStatus.Text = $"{SmartSearchErrorPrefix} {parseResult.ErrorMessage}";
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(preferredStatus) == false)
            {
                lblStatus.Text = preferredStatus;
                return;
            }

            if ((lblStatus.Text ?? string.Empty).StartsWith(SmartSearchErrorPrefix, StringComparison.Ordinal))
                lblStatus.Text = string.Empty;
        }

        private void searchModeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UseSmartSearch = _searchModeCheckBox?.Checked ?? true;
            UpdateSearchModeUi();

            string statusMessage = UseSmartSearch
                ? "Smart search enabled."
                : "Classic search enabled.";

            RefreshGrid(statusMessage: statusMessage);
            UpdateSearchValidationStatus(statusMessage);
        }
    }
}
