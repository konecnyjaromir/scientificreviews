using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Helpers
{
    public sealed class StatusStripOperationUpdate
    {
        public string Summary { get; set; }
        public string Details { get; set; }
        public int? Completed { get; set; }
        public int? Total { get; set; }
        public bool? IsIndeterminate { get; set; }
    }

    public sealed class StatusStripOperationHandle : IDisposable
    {
        private readonly StatusStripOperationManager _manager;
        private readonly Guid _id;
        private bool _isFinished;

        internal StatusStripOperationHandle(StatusStripOperationManager manager, Guid id)
        {
            _manager = manager;
            _id = id;
        }

        public void Report(string summary = null, string details = null, int? completed = null, int? total = null, bool? isIndeterminate = null)
        {
            if (_isFinished)
                return;

            _manager.Report(_id, new StatusStripOperationUpdate
            {
                Summary = summary,
                Details = details,
                Completed = completed,
                Total = total,
                IsIndeterminate = isIndeterminate
            });
        }

        public void Complete(string summary = null, string details = null)
        {
            if (_isFinished)
                return;

            _isFinished = true;
            _manager.Complete(_id, summary, details);
        }

        public void Fail(string summary, string details = null)
        {
            if (_isFinished)
                return;

            _isFinished = true;
            _manager.Fail(_id, summary, details);
        }

        public void Fail(Exception exception, string summary = null)
        {
            if (exception == null)
            {
                Fail(summary ?? "Failed");
                return;
            }

            Fail(summary ?? "Failed", exception.Message);
        }

        public void Dispose()
        {
            if (_isFinished == false)
                Complete();
        }
    }

    public sealed class StatusStripOperationManager
    {
        private sealed class OperationState
        {
            public Guid Id { get; set; }
            public string Key { get; set; }
            public string Name { get; set; }
            public string Summary { get; set; }
            public string Details { get; set; }
            public DateTime StartedAt { get; set; }
            public string Status { get; set; }
            public ToolStripSeparator Separator { get; set; }
            public ToolStripStatusLabel Label { get; set; }
            public ToolStripProgressBar ProgressBar { get; set; }
        }

        private readonly object _sync = new object();
        private readonly StatusStrip _statusStrip;
        private readonly ToolStripItem _anchorItem;
        private readonly IWin32Window _owner;
        private readonly Dictionary<Guid, OperationState> _operationsById = new Dictionary<Guid, OperationState>();
        private readonly HashSet<string> _activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public StatusStripOperationManager(StatusStrip statusStrip, ToolStripItem anchorItem, IWin32Window owner)
        {
            _statusStrip = statusStrip ?? throw new ArgumentNullException(nameof(statusStrip));
            _anchorItem = anchorItem ?? throw new ArgumentNullException(nameof(anchorItem));
            _owner = owner;
        }

        public bool IsActive(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            lock (_sync)
            {
                return _activeKeys.Contains(key);
            }
        }

        public StatusStripOperationHandle StartOperation(string key, string name, string details = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Operation key must not be empty.", nameof(key));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Operation name must not be empty.", nameof(name));

            OperationState state;

            lock (_sync)
            {
                if (_activeKeys.Contains(key))
                    return null;

                _activeKeys.Add(key);
                state = new OperationState
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Name = name,
                    Summary = "Starting...",
                    Details = details,
                    StartedAt = DateTime.Now,
                    Status = "Running"
                };
                _operationsById[state.Id] = state;
            }

            PostToUi(() => AddOperationUi(state));
            return new StatusStripOperationHandle(this, state.Id);
        }

        internal void Report(Guid operationId, StatusStripOperationUpdate update)
        {
            PostToUi(() =>
            {
                var state = GetState(operationId);
                if (state == null || state.Label == null || state.ProgressBar == null)
                    return;

                if (string.IsNullOrWhiteSpace(update?.Summary) == false)
                    state.Summary = update.Summary;

                if (string.IsNullOrWhiteSpace(update?.Details) == false)
                    state.Details = update.Details;

                ApplyProgress(state, update);
                UpdateLabel(state);
            });
        }

        internal void Complete(Guid operationId, string summary, string details)
        {
            Finish(operationId, "Done", summary, details, false);
        }

        internal void Fail(Guid operationId, string summary, string details)
        {
            Finish(operationId, "Failed", summary, details, true);
        }

        private void Finish(Guid operationId, string status, string summary, string details, bool failed)
        {
            OperationState state;

            lock (_sync)
            {
                if (_operationsById.TryGetValue(operationId, out state) == false)
                    return;

                _activeKeys.Remove(state.Key);
            }

            PostToUi(() =>
            {
                state = GetState(operationId);
                if (state == null || state.Label == null || state.ProgressBar == null)
                    return;

                state.Status = status;
                if (string.IsNullOrWhiteSpace(summary) == false)
                    state.Summary = summary;
                if (string.IsNullOrWhiteSpace(details) == false)
                    state.Details = details;

                state.ProgressBar.Style = ProgressBarStyle.Continuous;
                state.ProgressBar.Maximum = 1;
                state.ProgressBar.Value = 1;

                state.Label.Text = failed
                    ? $"{state.Name}: failed"
                    : $"{state.Name}: done";

                ScheduleRemoval(operationId, failed ? 12000 : 6000);
            });
        }

        private void ApplyProgress(OperationState state, StatusStripOperationUpdate update)
        {
            if (update == null)
            {
                state.ProgressBar.Style = ProgressBarStyle.Marquee;
                state.ProgressBar.MarqueeAnimationSpeed = 20;
                return;
            }

            bool isIndeterminate = update.IsIndeterminate ?? (update.Total.HasValue == false || update.Completed.HasValue == false || update.Total.Value <= 0);
            if (isIndeterminate)
            {
                state.ProgressBar.Style = ProgressBarStyle.Marquee;
                state.ProgressBar.MarqueeAnimationSpeed = 20;
                return;
            }

            int total = Math.Max(1, update.Total.Value);
            int completed = Math.Max(0, Math.Min(total, update.Completed ?? 0));

            state.ProgressBar.Style = ProgressBarStyle.Continuous;
            state.ProgressBar.MarqueeAnimationSpeed = 0;
            state.ProgressBar.Maximum = total;
            state.ProgressBar.Value = Math.Min(total, completed);
        }

        private void UpdateLabel(OperationState state)
        {
            string text = state.Name;
            if (string.IsNullOrWhiteSpace(state.Summary) == false)
                text += ": " + state.Summary;

            state.Label.Text = text;
            state.Label.ToolTipText = BuildDetailsText(state);
        }

        private void AddOperationUi(OperationState state)
        {
            if (_statusStrip.IsDisposed)
                return;

            state.Separator = new ToolStripSeparator();
            state.Label = new ToolStripStatusLabel
            {
                Text = state.Name,
                IsLink = true
            };
            state.Label.Click += (sender, e) => ShowDetails(state.Id);

            state.ProgressBar = new ToolStripProgressBar
            {
                AutoSize = false,
                Width = 90,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 20
            };

            int anchorIndex = _statusStrip.Items.IndexOf(_anchorItem);
            if (anchorIndex < 0)
                anchorIndex = _statusStrip.Items.Count;

            _statusStrip.Items.Insert(anchorIndex, state.Separator);
            _statusStrip.Items.Insert(anchorIndex + 1, state.Label);
            _statusStrip.Items.Insert(anchorIndex + 2, state.ProgressBar);

            UpdateLabel(state);
        }

        private void ShowDetails(Guid operationId)
        {
            var state = GetState(operationId);
            if (state == null)
                return;

            string message = BuildDetailsText(state);
            MessageBox.Show(_owner, message, state.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string BuildDetailsText(OperationState state)
        {
            return
                $"Operation: {state.Name}\n" +
                $"Status: {state.Status}\n" +
                $"Started: {state.StartedAt:G}\n" +
                $"Summary: {state.Summary ?? string.Empty}\n\n" +
                $"{state.Details ?? "No details."}";
        }

        private async void ScheduleRemoval(Guid operationId, int delayMilliseconds)
        {
            await Task.Delay(delayMilliseconds).ConfigureAwait(false);

            PostToUi(() =>
            {
                var state = RemoveState(operationId);
                if (state == null)
                    return;

                if (state.Label != null)
                    _statusStrip.Items.Remove(state.Label);
                if (state.ProgressBar != null)
                    _statusStrip.Items.Remove(state.ProgressBar);
                if (state.Separator != null)
                    _statusStrip.Items.Remove(state.Separator);

                state.Label?.Dispose();
                state.ProgressBar?.Dispose();
                state.Separator?.Dispose();
            });
        }

        private OperationState GetState(Guid operationId)
        {
            lock (_sync)
            {
                _operationsById.TryGetValue(operationId, out OperationState state);
                return state;
            }
        }

        private OperationState RemoveState(Guid operationId)
        {
            lock (_sync)
            {
                if (_operationsById.TryGetValue(operationId, out OperationState state) == false)
                    return null;

                _operationsById.Remove(operationId);
                _activeKeys.Remove(state.Key);
                return state;
            }
        }

        private void PostToUi(Action action)
        {
            if (_statusStrip.IsDisposed)
                return;

            if (_statusStrip.InvokeRequired)
            {
                try
                {
                    _statusStrip.BeginInvoke((MethodInvoker)(() =>
                    {
                        if (_statusStrip.IsDisposed == false)
                            action();
                    }));
                }
                catch (ObjectDisposedException)
                {
                }
            }
            else
            {
                action();
            }
        }
    }
}
