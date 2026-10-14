using ScientificReviews.Bibtex;
using ScientificReviews.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private const string SmartSearchErrorPrefix = "Invalid smart search query:";

        private CheckBox _searchModeCheckBox;
        private ToolStripControlHost _searchModeCheckBoxHost;
        private CancellationTokenSource _searchCancellation;

        private sealed class PreparedSearchResult
        {
            public BibtexEntry[] Entries { get; set; }
            public DataTable Table { get; set; }
            public string ValidationMessage { get; set; }
        }

        private void CancelPendingSearch()
        {
            _searchCancellation?.Cancel();
            _searchCancellation = null;
        }

        private async void ScheduleSearch()
        {
            CancelPendingSearch();
            if (IsDisposed || Disposing)
                return;

            CancellationTokenSource cancellation = new CancellationTokenSource();
            _searchCancellation = cancellation;
            string query = txtSearch.Text;
            bool smartSearch = UseSmartSearch;
            BibtexEntry[] source = entries.ToArray();
            string[] columns = (Program.AppSettings.Data.Columns ?? Array.Empty<string>()).ToArray();
            string sortColumn = _currentSortColumnName;
            ListSortDirection? sortDirection = _currentSortDirection;

            try
            {
                // Wait for a pause in typing before touching the grid.
                await Task.Delay(200, cancellation.Token);
                PreparedSearchResult result = await Task.Run(() =>
                {
                    string validationMessage;
                    BibtexEntry[] matches = ApplySearchFilter(source, query, smartSearch, cancellation.Token, out validationMessage);
                    cancellation.Token.ThrowIfCancellationRequested();
                    matches = ApplyCurrentSort(matches, sortColumn, sortDirection, cancellation.Token);
                    DataTable table = BuildTable(matches, source, columns, cancellation.Token);
                    return new PreparedSearchResult
                    {
                        Entries = matches,
                        Table = table,
                        ValidationMessage = validationMessage
                    };
                }, cancellation.Token);

                if (cancellation.IsCancellationRequested || !ReferenceEquals(_searchCancellation, cancellation) || IsDisposed || Disposing)
                    return;

                BindGridResult(result.Entries, result.Table, result.ValidationMessage);
                UpdateSearchValidationStatus();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!cancellation.IsCancellationRequested && ReferenceEquals(_searchCancellation, cancellation) && !IsDisposed)
                    lblStatus.Text = "Search failed: " + ex.Message;
            }
            finally
            {
                if (ReferenceEquals(_searchCancellation, cancellation))
                    _searchCancellation = null;
                cancellation.Dispose();
            }
        }

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
            return ApplySearchFilter(sourceEntries, search, UseSmartSearch, CancellationToken.None, out validationMessage);
        }

        private BibtexEntry[] ApplySearchFilter(
            IEnumerable<BibtexEntry> sourceEntries,
            string search,
            bool useSmartSearch,
            CancellationToken cancellationToken,
            out string validationMessage)
        {
            BibtexEntry[] candidates = (sourceEntries ?? Array.Empty<BibtexEntry>())
                .Where(entry => entry != null)
                .ToArray();

            validationMessage = null;
            if (string.IsNullOrWhiteSpace(search))
                return candidates;

            if (!useSmartSearch)
                return ApplyClassicSearch(candidates, search, cancellationToken);

            SmartSearchParseResult parseResult = SmartSearchFilter.TryParse(search);
            if (!parseResult.Success)
            {
                validationMessage = $"{SmartSearchErrorPrefix} {parseResult.ErrorMessage}";
                return candidates;
            }

            List<BibtexEntry> matches = new List<BibtexEntry>();
            foreach (BibtexEntry entry in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (parseResult.Filter.IsMatch(entry))
                    matches.Add(entry);
            }
            return matches.ToArray();
        }

        private BibtexEntry[] ApplyClassicSearch(IEnumerable<BibtexEntry> sourceEntries, string search, CancellationToken cancellationToken)
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
            foreach (BibtexEntry entry in sourceEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string serializedEntry = bibtexExporter.EntryToString(entry) ?? string.Empty;
                if (searchTerms.Any(term => serializedEntry.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
                    filtered.Add(entry);
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
