using System;
using System.Collections.Generic;
using System.Linq;

namespace ScientificReviews.Reports
{
    public enum OperationReportSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class OperationReportChange
    {
        public string RecordLabel { get; set; }
        public string Summary { get; set; }
        public string Details { get; set; }
    }

    public sealed class OperationReportItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Details { get; set; }
        public OperationReportSeverity Severity { get; set; } = OperationReportSeverity.Info;
        public bool IsRead { get; set; }
        public List<OperationReportChange> Changes { get; } = new List<OperationReportChange>();

        public override string ToString()
        {
            string unreadPrefix = IsRead ? string.Empty : "[new] ";
            string title = string.IsNullOrWhiteSpace(Title) ? "Report" : Title.Trim();
            return $"{unreadPrefix}{CreatedAt:HH:mm:ss} {title}";
        }
    }

    public sealed class OperationReportCenter
    {
        private readonly object _sync = new object();
        private readonly List<OperationReportItem> _reports = new List<OperationReportItem>();

        public event EventHandler Changed;

        public void Add(OperationReportItem report)
        {
            if (report == null)
                return;

            lock (_sync)
            {
                _reports.Insert(0, report);
            }

            OnChanged();
        }

        public List<OperationReportItem> GetSnapshot()
        {
            lock (_sync)
            {
                return _reports
                    .Select(CloneReport)
                    .ToList();
            }
        }

        public int GetUnreadCount()
        {
            lock (_sync)
            {
                return _reports.Count(report => !report.IsRead);
            }
        }

        public void MarkRead(Guid reportId)
        {
            bool changed = false;

            lock (_sync)
            {
                OperationReportItem report = _reports.FirstOrDefault(item => item.Id == reportId);
                if (report != null && !report.IsRead)
                {
                    report.IsRead = true;
                    changed = true;
                }
            }

            if (changed)
                OnChanged();
        }

        public void MarkAllRead()
        {
            bool changed = false;

            lock (_sync)
            {
                foreach (OperationReportItem report in _reports)
                {
                    if (report.IsRead)
                        continue;

                    report.IsRead = true;
                    changed = true;
                }
            }

            if (changed)
                OnChanged();
        }

        public void Clear()
        {
            lock (_sync)
            {
                _reports.Clear();
            }

            OnChanged();
        }

        private static OperationReportItem CloneReport(OperationReportItem source)
        {
            OperationReportItem clone = new OperationReportItem
            {
                Id = source.Id,
                CreatedAt = source.CreatedAt,
                Title = source.Title,
                Summary = source.Summary,
                Details = source.Details,
                Severity = source.Severity,
                IsRead = source.IsRead
            };

            foreach (OperationReportChange change in source.Changes)
            {
                clone.Changes.Add(new OperationReportChange
                {
                    RecordLabel = change.RecordLabel,
                    Summary = change.Summary,
                    Details = change.Details
                });
            }

            return clone;
        }

        private void OnChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
