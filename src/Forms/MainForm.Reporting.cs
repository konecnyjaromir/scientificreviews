using ScientificReviews.Bibtex;
using ScientificReviews.Reports;
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private readonly Stack<Guid> _reportScopeStack = new Stack<Guid>();

        private sealed class ReportScopeContext : IDisposable
        {
            private readonly MainForm _owner;
            private readonly OperationReportItem _report;
            private bool _disposed;

            public ReportScopeContext(MainForm owner, OperationReportItem report)
            {
                _owner = owner;
                _report = report;
            }

            public void Complete(
                string summary,
                string details = null,
                OperationReportSeverity severity = OperationReportSeverity.Info,
                EntryChangeReport changeReport = null)
            {
                _report.Summary = summary?.Trim();
                _report.Details = AppendChangeSummary(details, changeReport);
                _report.Severity = severity;
                ApplyChangeReport(_report, changeReport);
                _owner._reportCenter.Update(_report);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _owner.PopReportScope(_report.Id);
            }
        }

        private void InitializeReportCenter()
        {
            _reportCenter.Changed += ReportCenter_Changed;
            if (lblReports != null && lblReports.Image == null)
                lblReports.Image = SystemIcons.Information.ToBitmap();
            UpdateReportIndicator();
        }

        private void ReportCenter_Changed(object sender, EventArgs e)
        {
            if (statusStrip1 == null || statusStrip1.IsDisposed)
                return;

            if (statusStrip1.InvokeRequired)
            {
                statusStrip1.BeginInvoke((MethodInvoker)UpdateReportIndicator);
                return;
            }

            UpdateReportIndicator();
        }

        private void UpdateReportIndicator()
        {
            if (lblReports == null)
                return;

            int unreadCount = _reportCenter.GetUnreadCount();
            lblReports.Text = unreadCount > 0
                ? $"Notifications ({unreadCount})"
                : "Notifications";
            lblReports.ToolTipText = unreadCount > 0
                ? $"{unreadCount} unread report(s). Click to open the report center."
                : "Click to open the report center.";
        }

        private void lblReports_Click(object sender, EventArgs e)
        {
            using (ReportCenterForm form = new ReportCenterForm(_reportCenter))
            {
                form.ShowDialog(this);
            }
        }

        private EntryChangeSnapshot CaptureEntryChanges(IEnumerable<BibtexEntry> sourceEntries)
        {
            return EntryChangeTracker.Capture(sourceEntries);
        }

        private EntryChangeReport BuildEntryChangeReport(EntryChangeSnapshot snapshot, IEnumerable<BibtexEntry> currentEntries = null)
        {
            return snapshot?.Build(currentEntries ?? entries);
        }

        private void PublishReport(
            string title,
            string summary,
            string details = null,
            OperationReportSeverity severity = OperationReportSeverity.Info,
            EntryChangeReport changeReport = null)
        {
            OperationReportItem report = new OperationReportItem
            {
                ParentId = GetCurrentReportParentId(),
                Title = string.IsNullOrWhiteSpace(title) ? "Operation" : title.Trim(),
                Summary = summary?.Trim(),
                Details = AppendChangeSummary(details, changeReport),
                Severity = severity
            };

            ApplyChangeReport(report, changeReport);

            _reportCenter.Add(report);
        }

        private ReportScopeContext BeginReportScope(
            string title,
            string summary,
            string details = null,
            OperationReportSeverity severity = OperationReportSeverity.Info)
        {
            OperationReportItem report = new OperationReportItem
            {
                ParentId = GetCurrentReportParentId(),
                Title = string.IsNullOrWhiteSpace(title) ? "Operation" : title.Trim(),
                Summary = summary?.Trim(),
                Details = details?.Trim(),
                Severity = severity
            };

            _reportCenter.Add(report);
            _reportScopeStack.Push(report.Id);
            return new ReportScopeContext(this, report);
        }

        private static string AppendChangeSummary(string details, EntryChangeReport changeReport)
        {
            string changeSummary = BuildChangeSummary(changeReport);
            if (string.IsNullOrWhiteSpace(changeSummary))
                return details;

            if (string.IsNullOrWhiteSpace(details))
                return changeSummary;

            return details.Trim() + Environment.NewLine + Environment.NewLine + changeSummary;
        }

        private static string BuildChangeSummary(EntryChangeReport changeReport)
        {
            if (changeReport == null || !changeReport.HasChanges)
                return null;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Tracked changes");
            builder.AppendLine($"Modified: {changeReport.ModifiedEntries}");
            builder.AppendLine($"Removed: {changeReport.RemovedEntries}");
            builder.AppendLine($"Added: {changeReport.AddedEntries}");
            builder.Append($"Total changed records: {changeReport.TotalChangedEntries}");
            return builder.ToString();
        }

        private Guid? GetCurrentReportParentId()
        {
            return _reportScopeStack.Count > 0
                ? (Guid?)_reportScopeStack.Peek()
                : null;
        }

        private void PopReportScope(Guid reportId)
        {
            if (_reportScopeStack.Count == 0)
                return;

            if (_reportScopeStack.Peek() == reportId)
            {
                _reportScopeStack.Pop();
                return;
            }

            Stack<Guid> rebuilt = new Stack<Guid>(_reportScopeStack.Reverse().Where(id => id != reportId));
            _reportScopeStack.Clear();
            foreach (Guid id in rebuilt.Reverse())
                _reportScopeStack.Push(id);
        }

        private static void ApplyChangeReport(OperationReportItem report, EntryChangeReport changeReport)
        {
            report.Changes.Clear();
            foreach (OperationReportChange change in changeReport?.Changes ?? new List<OperationReportChange>())
                report.Changes.Add(change);
        }

        private static string BuildReportList(IEnumerable<string> values, string header)
        {
            List<string> items = (values ?? Enumerable.Empty<string>())
                .Where(value => string.IsNullOrWhiteSpace(value) == false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (items.Count == 0)
                return null;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(header);
            foreach (string item in items)
                builder.AppendLine("- " + item);

            return builder.ToString().TrimEnd();
        }
    }
}
