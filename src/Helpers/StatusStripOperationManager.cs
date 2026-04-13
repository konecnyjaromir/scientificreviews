using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Helpers
{
    internal sealed class StatusStripOperationSnapshot
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Summary { get; set; }
        public string Details { get; set; }
        public string Status { get; set; }
        public DateTime StartedAt { get; set; }
        public int? Completed { get; set; }
        public int? Total { get; set; }
        public bool IsIndeterminate { get; set; }
        public bool CanCancel { get; set; }
        public bool IsFinished { get; set; }
    }

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

        public void RegisterCancellation(Action cancelAction)
        {
            if (_isFinished)
                return;

            _manager.RegisterCancellation(_id, cancelAction);
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

        public void Cancel(string summary = null, string details = null)
        {
            if (_isFinished)
                return;

            _isFinished = true;
            _manager.Cancel(_id, summary, details);
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
            public int? Completed { get; set; }
            public int? Total { get; set; }
            public bool IsIndeterminate { get; set; }
            public Action CancelAction { get; set; }
            public bool CancellationRequested { get; set; }
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

        internal void Cancel(Guid operationId, string summary, string details)
        {
            Finish(operationId, "Cancelled", summary, details, false);
        }

        internal void RegisterCancellation(Guid operationId, Action cancelAction)
        {
            lock (_sync)
            {
                if (_operationsById.TryGetValue(operationId, out OperationState state))
                    state.CancelAction = cancelAction;
            }
        }

        internal bool RequestCancel(Guid operationId)
        {
            Action cancelAction = null;

            lock (_sync)
            {
                if (_operationsById.TryGetValue(operationId, out OperationState state) == false)
                    return false;

                if (state.CancelAction == null || state.CancellationRequested || !string.Equals(state.Status, "Running", StringComparison.OrdinalIgnoreCase))
                    return false;

                state.CancellationRequested = true;
                state.Status = "Stopping";
                cancelAction = state.CancelAction;
            }

            PostToUi(() =>
            {
                var state = GetState(operationId);
                if (state == null)
                    return;

                UpdateLabel(state);
            });

            Task.Run(() =>
            {
                try
                {
                    cancelAction?.Invoke();
                }
                catch
                {
                }
            });

            return true;
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
                state.CancellationRequested = false;
                state.CancelAction = null;

                state.ProgressBar.Style = ProgressBarStyle.Continuous;
                state.ProgressBar.Maximum = 1;
                state.ProgressBar.Value = 1;

                state.Label.Text = failed
                    ? $"{state.Name}: failed"
                    : string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                        ? $"{state.Name}: cancelled"
                        : $"{state.Name}: done";

                ScheduleRemoval(operationId, failed ? 12000 : 6000);
            });
        }

        private void ApplyProgress(OperationState state, StatusStripOperationUpdate update)
        {
            if (update == null)
            {
                state.IsIndeterminate = true;
                state.Completed = null;
                state.Total = null;
                state.ProgressBar.Style = ProgressBarStyle.Marquee;
                state.ProgressBar.MarqueeAnimationSpeed = 20;
                return;
            }

            bool isIndeterminate = update.IsIndeterminate ?? (update.Total.HasValue == false || update.Completed.HasValue == false || update.Total.Value <= 0);
            state.IsIndeterminate = isIndeterminate;
            if (isIndeterminate)
            {
                state.Completed = update.Completed;
                state.Total = update.Total;
                state.ProgressBar.Style = ProgressBarStyle.Marquee;
                state.ProgressBar.MarqueeAnimationSpeed = 20;
                return;
            }

            int total = Math.Max(1, update.Total.Value);
            int completed = Math.Max(0, Math.Min(total, update.Completed ?? 0));
            state.Total = total;
            state.Completed = completed;

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
            StatusStripOperationSnapshot snapshot = GetSnapshot(operationId);
            if (snapshot == null)
                return;

            using (StatusStripOperationDetailsForm form = new StatusStripOperationDetailsForm(this, operationId, snapshot))
            {
                form.ShowDialog(_owner);
            }
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

        internal StatusStripOperationSnapshot GetSnapshot(Guid operationId)
        {
            lock (_sync)
            {
                if (_operationsById.TryGetValue(operationId, out OperationState state) == false)
                    return null;

                return new StatusStripOperationSnapshot
                {
                    Id = state.Id,
                    Name = state.Name,
                    Summary = state.Summary,
                    Details = state.Details,
                    Status = state.Status,
                    StartedAt = state.StartedAt,
                    Completed = state.Completed,
                    Total = state.Total,
                    IsIndeterminate = state.IsIndeterminate,
                    CanCancel = state.CancelAction != null && state.CancellationRequested == false && string.Equals(state.Status, "Running", StringComparison.OrdinalIgnoreCase),
                    IsFinished = !string.Equals(state.Status, "Running", StringComparison.OrdinalIgnoreCase) && !string.Equals(state.Status, "Stopping", StringComparison.OrdinalIgnoreCase)
                };
            }
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

    internal sealed class StatusStripOperationDetailsForm : Form
    {
        private readonly StatusStripOperationManager _manager;
        private readonly Guid _operationId;
        private readonly Timer _refreshTimer;
        private StatusStripOperationSnapshot _lastSnapshot;
        private Label _lblName;
        private Label _lblStatus;
        private Label _lblStarted;
        private Label _lblSummary;
        private Label _lblProgress;
        private ProgressBar _progressBar;
        private TextBox _txtDetails;
        private Button _btnStop;
        private Button _btnClose;

        public StatusStripOperationDetailsForm(StatusStripOperationManager manager, Guid operationId, StatusStripOperationSnapshot initialSnapshot)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _operationId = operationId;
            _lastSnapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
            _refreshTimer = new Timer();

            InitializeComponent();
            ApplySnapshot(_lastSnapshot);

            _refreshTimer.Interval = 300;
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private void InitializeComponent()
        {
            _lblName = new Label();
            _lblStatus = new Label();
            _lblStarted = new Label();
            _lblSummary = new Label();
            _lblProgress = new Label();
            _progressBar = new ProgressBar();
            _txtDetails = new TextBox();
            _btnStop = new Button();
            _btnClose = new Button();
            TableLayoutPanel root = new TableLayoutPanel();
            FlowLayoutPanel buttons = new FlowLayoutPanel();

            root.SuspendLayout();
            buttons.SuspendLayout();
            SuspendLayout();

            root.ColumnCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.RowCount = 8;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lblName.AutoSize = true;
            _lblName.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            _lblName.Margin = new Padding(0, 0, 0, 8);

            _lblStatus.AutoSize = true;
            _lblStatus.Margin = new Padding(0, 0, 0, 6);

            _lblStarted.AutoSize = true;
            _lblStarted.Margin = new Padding(0, 0, 0, 6);

            _lblSummary.AutoSize = true;
            _lblSummary.MaximumSize = new Size(500, 0);
            _lblSummary.Margin = new Padding(0, 0, 0, 10);

            _lblProgress.AutoSize = true;
            _lblProgress.Margin = new Padding(0, 0, 0, 6);

            _progressBar.Dock = DockStyle.Fill;
            _progressBar.Maximum = 1000;
            _progressBar.Margin = new Padding(0, 0, 0, 10);

            _txtDetails.Dock = DockStyle.Fill;
            _txtDetails.Multiline = true;
            _txtDetails.ReadOnly = true;
            _txtDetails.ScrollBars = ScrollBars.Vertical;

            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Margin = new Padding(0, 10, 0, 0);
            buttons.WrapContents = false;

            _btnClose.AutoSize = true;
            _btnClose.DialogResult = DialogResult.OK;
            _btnClose.Text = "Close";

            _btnStop.AutoSize = true;
            _btnStop.Text = "Stop";
            _btnStop.Click += btnStop_Click;

            buttons.Controls.Add(_btnClose);
            buttons.Controls.Add(_btnStop);

            root.Controls.Add(_lblName, 0, 0);
            root.Controls.Add(_lblStatus, 0, 1);
            root.Controls.Add(_lblStarted, 0, 2);
            root.Controls.Add(_lblSummary, 0, 3);
            root.Controls.Add(_lblProgress, 0, 4);
            root.Controls.Add(_progressBar, 0, 5);
            root.Controls.Add(_txtDetails, 0, 6);
            root.Controls.Add(buttons, 0, 7);

            AcceptButton = _btnClose;
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(560, 360);
            Controls.Add(root);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "StatusStripOperationDetailsForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Operation Status";
            FormClosed += StatusStripOperationDetailsForm_FormClosed;

            root.ResumeLayout(false);
            root.PerformLayout();
            buttons.ResumeLayout(false);
            buttons.PerformLayout();
            ResumeLayout(false);
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            StatusStripOperationSnapshot snapshot = _manager.GetSnapshot(_operationId);
            if (snapshot != null)
                _lastSnapshot = snapshot;

            ApplySnapshot(_lastSnapshot);
        }

        private void ApplySnapshot(StatusStripOperationSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            _lblName.Text = snapshot.Name;
            _lblStatus.Text = $"Status: {snapshot.Status}";
            _lblStarted.Text = $"Started: {snapshot.StartedAt:G}";
            _lblSummary.Text = $"Summary: {snapshot.Summary ?? string.Empty}";
            _txtDetails.Text = snapshot.Details ?? "No details.";

            if (snapshot.IsIndeterminate || snapshot.Total.HasValue == false || snapshot.Total.Value <= 0)
            {
                _progressBar.Style = ProgressBarStyle.Marquee;
                _progressBar.MarqueeAnimationSpeed = 20;
                _lblProgress.Text = "Progress: working...";
            }
            else
            {
                int total = Math.Max(1, snapshot.Total.Value);
                int completed = Math.Max(0, Math.Min(total, snapshot.Completed ?? 0));
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.MarqueeAnimationSpeed = 0;
                _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, (int)Math.Round(completed * 1000d / total)));
                _lblProgress.Text = $"Progress: {completed}/{total}";
            }

            _btnStop.Enabled = snapshot.CanCancel;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (_manager.RequestCancel(_operationId))
                _btnStop.Enabled = false;
        }

        private void StatusStripOperationDetailsForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        }
    }
}
