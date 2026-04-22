using ScientificReviews.Reports;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public sealed class ReportCenterForm : Form
    {
        private const int DefaultClientWidth = 1350;
        private const int DefaultClientHeight = 620;
        private const int DesiredClientWidth = 1688;
        private const int DesiredClientHeight = 620;
        private const int ScreenWidthMargin = 80;
        private const int ScreenHeightMargin = 120;
        private const int LeftPanelMinimumWidth = 240;
        private const int RightPanelMinimumWidth = 420;
        private const int PreferredLeftPanelWidth = 300;
        private const string ReportLoadingPlaceholder = "Please wait, processing report for you...";

        private readonly OperationReportCenter _reportCenter;
        private bool _isRefreshing;
        private SplitContainer _splitContainer;
        private TreeView _treeReports;
        private RichTextBox _rtbDetails;
        private Button _btnMarkRead;
        private Button _btnMarkAllRead;
        private Button _btnClear;
        private Button _btnClose;
        private List<OperationReportItem> _snapshot = new List<OperationReportItem>();
        private Font _treeBoldFont;
        private Font _detailFont;
        private Font _detailBoldFont;
        private Font _titleFont;
        private Font _sectionFont;
        private CancellationTokenSource _detailRenderCts;

        public ReportCenterForm(OperationReportCenter reportCenter)
        {
            _reportCenter = reportCenter ?? throw new ArgumentNullException(nameof(reportCenter));
            InitializeComponent();
            ApplyInitialWindowSize();
            UpdateSplitterDistanceSafe();
            LoadReports();
        }

        private void InitializeComponent()
        {
            _splitContainer = new SplitContainer();
            _treeReports = new TreeView();
            _rtbDetails = new RichTextBox();
            _btnMarkRead = new Button();
            _btnMarkAllRead = new Button();
            _btnClear = new Button();
            _btnClose = new Button();
            FlowLayoutPanel buttons = new FlowLayoutPanel();

            SuspendLayout();

            _splitContainer.Dock = DockStyle.Fill;
            _splitContainer.Orientation = Orientation.Vertical;

            _treeReports.Dock = DockStyle.Fill;
            _treeReports.HideSelection = false;
            _treeReports.AfterSelect += TreeReports_AfterSelect;

            _rtbDetails.Dock = DockStyle.Fill;
            _rtbDetails.ReadOnly = true;
            _rtbDetails.WordWrap = false;
            _rtbDetails.DetectUrls = false;
            _detailFont = new Font("Consolas", 9F, FontStyle.Regular);
            _detailBoldFont = new Font("Consolas", 9F, FontStyle.Bold);
            _titleFont = new Font("Consolas", 12F, FontStyle.Bold);
            _sectionFont = new Font("Consolas", 10F, FontStyle.Bold);
            _treeBoldFont = new Font(_treeReports.Font, FontStyle.Bold);
            _rtbDetails.Font = _detailFont;
            _rtbDetails.BackColor = Color.White;
            _rtbDetails.BorderStyle = BorderStyle.None;

            _splitContainer.Panel1.Controls.Add(_treeReports);
            _splitContainer.Panel2.Controls.Add(_rtbDetails);

            buttons.Dock = DockStyle.Bottom;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Height = 42;
            buttons.Padding = new Padding(8);

            _btnClose.AutoSize = true;
            _btnClose.Text = "Close";
            _btnClose.DialogResult = DialogResult.OK;

            _btnClear.AutoSize = true;
            _btnClear.Text = "Clear";
            _btnClear.Click += BtnClear_Click;

            _btnMarkAllRead.AutoSize = true;
            _btnMarkAllRead.Text = "Mark all read";
            _btnMarkAllRead.Click += BtnMarkAllRead_Click;

            _btnMarkRead.AutoSize = true;
            _btnMarkRead.Text = "Mark read";
            _btnMarkRead.Click += BtnMarkRead_Click;

            buttons.Controls.Add(_btnClose);
            buttons.Controls.Add(_btnClear);
            buttons.Controls.Add(_btnMarkAllRead);
            buttons.Controls.Add(_btnMarkRead);

            AcceptButton = _btnClose;
            AutoScaleDimensions = new SizeF(8F, 16F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(DefaultClientWidth, DefaultClientHeight);
            Controls.Add(_splitContainer);
            Controls.Add(buttons);
            MinimizeBox = false;
            MinimumSize = new Size(1120, 480);
            Name = "ReportCenterForm";
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Notifications and Reports";
            Shown += ReportCenterForm_Shown;
            SizeChanged += ReportCenterForm_SizeChanged;

            ResumeLayout(false);
        }

        private void ReportCenterForm_Shown(object sender, EventArgs e)
        {
            UpdateSplitterDistanceSafe();
        }

        private void ReportCenterForm_SizeChanged(object sender, EventArgs e)
        {
            UpdateSplitterDistanceSafe();
        }

        private void ApplyInitialWindowSize()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            int maxClientWidth = Math.Max(MinimumSize.Width, workingArea.Width - ScreenWidthMargin);
            int maxClientHeight = Math.Max(MinimumSize.Height, workingArea.Height - ScreenHeightMargin);

            int targetWidth = Math.Min(DesiredClientWidth, maxClientWidth);
            int targetHeight = Math.Min(DesiredClientHeight, maxClientHeight);

            ClientSize = new Size(targetWidth, targetHeight);
        }

        private void LoadReports(Guid? selectedId = null)
        {
            _isRefreshing = true;
            _snapshot = (_reportCenter.GetSnapshot() ?? new List<OperationReportItem>())
                .Where(report => report != null)
                .ToList();
            Guid? effectiveSelection = selectedId ?? GetSelectedReportId();

            Dictionary<Guid, List<OperationReportItem>> childrenByParent = _snapshot
                .Where(report => report.ParentId.HasValue)
                .GroupBy(report => report.ParentId.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(item => item.CreatedAt).ToList());

            HashSet<Guid> knownIds = new HashSet<Guid>(_snapshot.Select(item => item.Id));
            List<OperationReportItem> roots = _snapshot
                .Where(report => !report.ParentId.HasValue || !knownIds.Contains(report.ParentId.Value))
                .OrderByDescending(report => report.CreatedAt)
                .ToList();

            _treeReports.BeginUpdate();
            _treeReports.Nodes.Clear();
            foreach (OperationReportItem root in roots)
                _treeReports.Nodes.Add(CreateReportNode(root, childrenByParent));
            _treeReports.EndUpdate();

            if (_treeReports.Nodes.Count == 0)
            {
                _rtbDetails.Clear();
                AppendPlainLine("No reports available.", SystemColors.ControlText);
                _btnMarkRead.Enabled = false;
                _isRefreshing = false;
                return;
            }

            TreeNode selectedNode = FindNodeByReportId(_treeReports.Nodes, effectiveSelection);
            if (selectedNode == null)
                selectedNode = _treeReports.Nodes[0];

            _treeReports.SelectedNode = selectedNode;
            selectedNode.EnsureVisible();
            RenderSelectedReport();
            _isRefreshing = false;
        }

        private TreeNode CreateReportNode(OperationReportItem report, Dictionary<Guid, List<OperationReportItem>> childrenByParent)
        {
            TreeNode node = new TreeNode(BuildNodeText(report))
            {
                Tag = report
            };

            node.NodeFont = report.IsRead
                ? _treeReports.Font
                : _treeBoldFont;
            node.ForeColor = GetSeverityColor(report.Severity);

            if (childrenByParent.TryGetValue(report.Id, out List<OperationReportItem> children))
            {
                foreach (OperationReportItem child in children)
                    node.Nodes.Add(CreateReportNode(child, childrenByParent));
            }

            return node;
        }

        private void TreeReports_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_isRefreshing)
                return;

            OperationReportItem report = e.Node?.Tag as OperationReportItem;
            if (report == null)
            {
                _btnMarkRead.Enabled = false;
                return;
            }

            _reportCenter.MarkRead(report.Id, true);
            LoadReports(report.Id);
        }

        private void BtnMarkRead_Click(object sender, EventArgs e)
        {
            OperationReportItem report = _treeReports.SelectedNode?.Tag as OperationReportItem;
            if (report == null)
                return;

            _reportCenter.MarkRead(report.Id, true);
            LoadReports(report.Id);
        }

        private void BtnMarkAllRead_Click(object sender, EventArgs e)
        {
            _reportCenter.MarkAllRead();
            LoadReports();
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                this,
                "Do you want to clear all notifications and reports?",
                Program.APP_NAME,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            _reportCenter.Clear();
            LoadReports();
        }

        private void RenderSelectedReport()
        {
            OperationReportItem report = _treeReports.SelectedNode?.Tag as OperationReportItem;

            if (report == null)
            {
                _detailRenderCts?.Cancel();
                _rtbDetails.Clear();
                AppendPlainLine("No report selected.", SystemColors.ControlText);
                _btnMarkRead.Enabled = false;
                return;
            }

            List<OperationReportItem> descendants = GetDescendants(report.Id) ?? new List<OperationReportItem>();
            _btnMarkRead.Enabled = !report.IsRead || descendants.Any(item => !item.IsRead);
            if (ShouldRenderAsynchronously(report, descendants))
            {
                StartAsyncDetailsRender(report, descendants);
                return;
            }

            _detailRenderCts?.Cancel();
            _rtbDetails.Clear();

            AppendTitle(report.Title ?? "Report");
            AppendSeparator();
            AppendMetaLine("Created", report.CreatedAt.ToString("G"));
            AppendMetaLine("Severity", report.Severity.ToString());
            AppendMetaLine("Status", report.IsRead ? "Read" : "Unread");
            if (descendants.Count > 0)
                AppendMetaLine("Child reports", descendants.Count.ToString());

            if (string.IsNullOrWhiteSpace(report.Summary) == false)
            {
                AppendSectionHeader("Summary");
                AppendParagraph(report.Summary.Trim());
            }

            if (string.IsNullOrWhiteSpace(report.Details) == false)
            {
                AppendSectionHeader("Details");
                AppendPlainBlock(report.Details.Trim(), SystemColors.ControlText);
            }

            if (descendants.Count > 0)
            {
                AppendSectionHeader("Pipeline");
                AppendPipeline(descendants);
            }

            if (report.Changes.Count > 0)
            {
                AppendSectionHeader(descendants.Count > 0
                    ? $"Aggregated changes ({report.Changes.Count})"
                    : $"Changes ({report.Changes.Count})");
                AppendChanges(report.Changes);
            }

            List<OperationReportItem> changeChildren = descendants
                .Where(item => item != null)
                .Where(item => item.Changes.Count > 0)
                .ToList();
            if (changeChildren.Count > 0)
            {
                AppendSectionHeader("Subprocess changes");
                foreach (OperationReportItem child in changeChildren)
                {
                    AppendSubprocessHeader(child);
                    AppendChanges(child.Changes);
                }
            }

            _rtbDetails.SelectionStart = 0;
            _rtbDetails.SelectionLength = 0;
        }

        private bool ShouldRenderAsynchronously(OperationReportItem report, List<OperationReportItem> descendants)
        {
            if (report == null)
                return false;

            ReportRenderOptimizationSettings optimizationSettings = GetReportRenderOptimizationSettings();
            if (optimizationSettings.DisableAsyncOptimization)
                return false;

            int estimatedLineCount = CountLines(report.Details) + CountChanges(report.Changes);
            int estimatedCharacterCount = (report.Summary ?? string.Empty).Length + (report.Details ?? string.Empty).Length;

            foreach (OperationReportItem child in descendants ?? new List<OperationReportItem>())
            {
                estimatedLineCount += CountLines(child.Summary) + CountLines(child.Details) + CountChanges(child.Changes) + 3;
                estimatedCharacterCount += (child.Title ?? string.Empty).Length + (child.Summary ?? string.Empty).Length + (child.Details ?? string.Empty).Length;
            }

            return estimatedLineCount >= optimizationSettings.LineThreshold
                || estimatedCharacterCount >= optimizationSettings.CharacterThreshold;
        }

        private void StartAsyncDetailsRender(OperationReportItem report, List<OperationReportItem> descendants)
        {
            _detailRenderCts?.Cancel();
            _detailRenderCts?.Dispose();
            _detailRenderCts = new CancellationTokenSource();
            CancellationToken token = _detailRenderCts.Token;
            Guid reportId = report.Id;
            List<OperationReportItem> descendantsSnapshot = descendants?
                .Where(item => item != null)
                .ToList()
                ?? new List<OperationReportItem>();
            List<OperationReportItem> snapshotCopy = (_snapshot ?? new List<OperationReportItem>())
                .Where(item => item != null)
                .ToList();

            ShowLoadingPlaceholder();

            Task.Run(() =>
            {
                return BuildPlainTextReport(report, descendantsSnapshot, snapshotCopy, token);
            }, token).ContinueWith(task =>
            {
                if (IsDisposed || token.IsCancellationRequested)
                    return;

                if (task.IsFaulted)
                {
                    ShowRenderError(task.Exception?.GetBaseException()?.Message);
                    return;
                }

                OperationReportItem selectedReport = _treeReports.SelectedNode?.Tag as OperationReportItem;
                if (selectedReport == null || selectedReport.Id != reportId)
                    return;

                ApplyPlainTextDetails(task.Result);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ShowLoadingPlaceholder()
        {
            _rtbDetails.Clear();
            _rtbDetails.ForeColor = SystemColors.GrayText;
            _rtbDetails.SelectionFont = new Font(_detailFont, FontStyle.Italic);
            _rtbDetails.Text = ReportLoadingPlaceholder;
            _rtbDetails.SelectionStart = 0;
            _rtbDetails.SelectionLength = 0;
        }

        private void ShowRenderError(string message)
        {
            _rtbDetails.Clear();
            _rtbDetails.ForeColor = Color.FromArgb(176, 32, 37);
            _rtbDetails.SelectionFont = _detailFont;
            _rtbDetails.Text = string.IsNullOrWhiteSpace(message)
                ? "Report rendering failed."
                : "Report rendering failed." + Environment.NewLine + Environment.NewLine + message;
            _rtbDetails.SelectionStart = 0;
            _rtbDetails.SelectionLength = 0;
        }

        private void ApplyPlainTextDetails(string text)
        {
            _rtbDetails.Clear();
            _rtbDetails.ForeColor = SystemColors.ControlText;
            _rtbDetails.Font = _detailFont;
            _rtbDetails.Text = text ?? string.Empty;
            _rtbDetails.SelectionStart = 0;
            _rtbDetails.SelectionLength = 0;
        }

        private List<OperationReportItem> GetDescendants(Guid rootId)
        {
            List<OperationReportItem> safeSnapshot = (_snapshot ?? new List<OperationReportItem>())
                .Where(report => report != null)
                .ToList();

            Dictionary<Guid, List<OperationReportItem>> lookup = BuildChildrenLookup(safeSnapshot);

            List<OperationReportItem> descendants = new List<OperationReportItem>();
            CollectDescendants(rootId, lookup, descendants);
            return descendants;
        }

        private static void CollectDescendants(
            Guid parentId,
            Dictionary<Guid, List<OperationReportItem>> lookup,
            List<OperationReportItem> destination)
        {
            if (!lookup.TryGetValue(parentId, out List<OperationReportItem> children))
                return;

            foreach (OperationReportItem child in children.Where(item => item != null))
            {
                destination.Add(child);
                CollectDescendants(child.Id, lookup, destination);
            }
        }

        private void AppendPipeline(List<OperationReportItem> descendants)
        {
            Dictionary<Guid, List<OperationReportItem>> lookup = BuildChildrenLookup(
                (_snapshot ?? new List<OperationReportItem>())
                    .Where(report => report != null));

            OperationReportItem selected = _treeReports.SelectedNode?.Tag as OperationReportItem;
            if (selected == null)
                return;

            AppendPipelineRecursive(selected.Id, lookup, 0);
            AppendBlankLine();
        }

        private void AppendPipelineRecursive(Guid parentId, Dictionary<Guid, List<OperationReportItem>> lookup, int depth)
        {
            if (!lookup.TryGetValue(parentId, out List<OperationReportItem> children))
                return;

            foreach (OperationReportItem child in children)
            {
                string indent = new string(' ', depth * 2);
                string line = $"{indent}- {child.Title}: {child.Summary}";
                AppendPlainLine(line.TrimEnd(' ', ':'), GetSeverityColor(child.Severity));
                AppendPipelineRecursive(child.Id, lookup, depth + 1);
            }
        }

        private static Dictionary<Guid, List<OperationReportItem>> BuildChildrenLookup(IEnumerable<OperationReportItem> reports)
        {
            return (reports ?? Enumerable.Empty<OperationReportItem>())
                .Where(report => report != null && report.ParentId.HasValue)
                .GroupBy(report => report.ParentId.Value)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.CreatedAt).ToList());
        }

        private static string BuildPlainTextReport(
            OperationReportItem report,
            List<OperationReportItem> descendants,
            List<OperationReportItem> snapshot,
            CancellationToken cancellationToken)
        {
            StringBuilder builder = new StringBuilder();

            cancellationToken.ThrowIfCancellationRequested();
            builder.AppendLine(report?.Title ?? "Report");
            builder.AppendLine(new string('-', 72));
            builder.AppendLine("Created: " + report.CreatedAt.ToString("G"));
            builder.AppendLine("Severity: " + report.Severity);
            builder.AppendLine("Status: " + (report.IsRead ? "Read" : "Unread"));
            if ((descendants?.Count ?? 0) > 0)
                builder.AppendLine("Child reports: " + descendants.Count);

            if (string.IsNullOrWhiteSpace(report.Summary) == false)
            {
                builder.AppendLine();
                builder.AppendLine("Summary");
                builder.AppendLine(report.Summary.Trim());
            }

            if (string.IsNullOrWhiteSpace(report.Details) == false)
            {
                builder.AppendLine();
                builder.AppendLine("Details");
                builder.AppendLine(report.Details.Trim());
            }

            cancellationToken.ThrowIfCancellationRequested();
            if ((descendants?.Count ?? 0) > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Pipeline");
                Dictionary<Guid, List<OperationReportItem>> lookup = BuildChildrenLookup(snapshot);
                AppendPipelinePlainText(builder, report.Id, lookup, 0, cancellationToken);
            }

            if (report.Changes.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine((descendants?.Count ?? 0) > 0
                    ? $"Aggregated changes ({report.Changes.Count})"
                    : $"Changes ({report.Changes.Count})");
                AppendChangesPlainText(builder, report.Changes, cancellationToken);
            }

            List<OperationReportItem> changeChildren = (descendants ?? new List<OperationReportItem>())
                .Where(item => item != null && item.Changes.Count > 0)
                .ToList();
            if (changeChildren.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Subprocess changes");
                foreach (OperationReportItem child in changeChildren)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    builder.AppendLine($"{child.Title}  [{child.CreatedAt:HH:mm:ss}]  {child.Severity}");
                    if (string.IsNullOrWhiteSpace(child.Summary) == false)
                        builder.AppendLine(child.Summary.Trim());
                    builder.AppendLine();
                    AppendChangesPlainText(builder, child.Changes, cancellationToken);
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendPipelinePlainText(
            StringBuilder builder,
            Guid parentId,
            Dictionary<Guid, List<OperationReportItem>> lookup,
            int depth,
            CancellationToken cancellationToken)
        {
            if (!lookup.TryGetValue(parentId, out List<OperationReportItem> children))
                return;

            foreach (OperationReportItem child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string indent = new string(' ', depth * 2);
                string line = $"{indent}- {child.Title}: {child.Summary}";
                builder.AppendLine(line.TrimEnd(' ', ':'));
                AppendPipelinePlainText(builder, child.Id, lookup, depth + 1, cancellationToken);
            }
        }

        private static void AppendChangesPlainText(
            StringBuilder builder,
            IEnumerable<OperationReportChange> changes,
            CancellationToken cancellationToken)
        {
            foreach (OperationReportChange change in changes ?? Enumerable.Empty<OperationReportChange>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.AppendLine(new string('=', 72));
                builder.AppendLine(change.RecordLabel ?? "<unnamed record>");
                if (string.IsNullOrWhiteSpace(change.Summary) == false)
                    builder.AppendLine(change.Summary);
                if (string.IsNullOrWhiteSpace(change.Details) == false)
                    builder.AppendLine(change.Details);
                builder.AppendLine();
            }
        }

        private static ReportRenderOptimizationSettings GetReportRenderOptimizationSettings()
        {
            PerformanceOptimizationMode mode = Program.AppSettings?.Data?.PerformanceOptimizationMode
                ?? PerformanceOptimizationMode.OptimizeForQualityPerformanceRatio;

            switch (mode)
            {
                case PerformanceOptimizationMode.OptimizeForPerformance:
                    return new ReportRenderOptimizationSettings(50, 4000, false);
                case PerformanceOptimizationMode.OptimizeForQuality:
                    return new ReportRenderOptimizationSettings(300, 24000, false);
                case PerformanceOptimizationMode.NoOptimization:
                    return new ReportRenderOptimizationSettings(int.MaxValue, int.MaxValue, true);
                case PerformanceOptimizationMode.OptimizeForQualityPerformanceRatio:
                default:
                    return new ReportRenderOptimizationSettings(150, 12000, false);
            }
        }

        private static int CountChanges(IEnumerable<OperationReportChange> changes)
        {
            int total = 0;
            foreach (OperationReportChange change in changes ?? Enumerable.Empty<OperationReportChange>())
            {
                total += 3;
                total += CountLines(change?.Summary);
                total += CountLines(change?.Details);
            }

            return total;
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            int lineCount = 1;
            foreach (char character in text)
            {
                if (character == '\n')
                    lineCount++;
            }

            return lineCount;
        }

        private readonly struct ReportRenderOptimizationSettings
        {
            public ReportRenderOptimizationSettings(int lineThreshold, int characterThreshold, bool disableAsyncOptimization)
            {
                LineThreshold = lineThreshold;
                CharacterThreshold = characterThreshold;
                DisableAsyncOptimization = disableAsyncOptimization;
            }

            public int LineThreshold { get; }
            public int CharacterThreshold { get; }
            public bool DisableAsyncOptimization { get; }
        }

        private void AppendChanges(IEnumerable<OperationReportChange> changes)
        {
            foreach (OperationReportChange change in changes)
                AppendChangeBlock(change);
        }

        private void AppendChangeBlock(OperationReportChange change)
        {
            Color headerBackColor = GetBlockBackground(change.Kind);

            AppendBlockLine(new string('=', 72), SystemColors.GrayText, Color.White, false);
            AppendBlockLine(change.RecordLabel ?? "<unnamed record>", SystemColors.ControlText, headerBackColor, true);
            AppendBlockLine(change.Summary ?? string.Empty, SystemColors.ControlText, headerBackColor, false);

            string[] detailLines = (change.Details ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (string rawLine in detailLines)
            {
                string line = rawLine ?? string.Empty;
                Color color = GetDiffColor(line);
                AppendBlockLine(line, color, headerBackColor, false);
            }

            AppendBlankLine();
        }

        private void AppendSubprocessHeader(OperationReportItem report)
        {
            AppendBlockLine(
                $"{report.Title}  [{report.CreatedAt:HH:mm:ss}]  {report.Severity}",
                GetSeverityColor(report.Severity),
                Color.FromArgb(245, 245, 245),
                true);

            if (string.IsNullOrWhiteSpace(report.Summary) == false)
                AppendBlockLine(report.Summary.Trim(), SystemColors.ControlText, Color.FromArgb(245, 245, 245), false);

            AppendBlankLine();
        }

        private void AppendTitle(string text)
        {
            AppendStyledText(text + Environment.NewLine, SystemColors.ControlText, Color.White, _titleFont);
        }

        private void AppendSectionHeader(string text)
        {
            AppendBlankLine();
            AppendStyledText(text + Environment.NewLine, Color.FromArgb(0, 86, 155), Color.White, _sectionFont);
        }

        private void AppendMetaLine(string label, string value)
        {
            AppendStyledText(label + ": ", SystemColors.ControlText, Color.White, _detailBoldFont);
            AppendStyledText(value + Environment.NewLine, SystemColors.ControlText, Color.White, _detailFont);
        }

        private void AppendParagraph(string text)
        {
            AppendPlainBlock(text, SystemColors.ControlText);
            AppendBlankLine();
        }

        private void AppendPlainBlock(string text, Color color)
        {
            foreach (string line in (text ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                AppendPlainLine(line, color);
        }

        private void AppendPlainLine(string text, Color color)
        {
            AppendStyledText((text ?? string.Empty) + Environment.NewLine, color, Color.White, _detailFont);
        }

        private void AppendBlockLine(string text, Color foreColor, Color backColor, bool bold)
        {
            AppendStyledText((text ?? string.Empty) + Environment.NewLine, foreColor, backColor, bold ? _detailBoldFont : _detailFont);
        }

        private void AppendSeparator()
        {
            AppendPlainLine(new string('-', 72), SystemColors.GrayText);
        }

        private void AppendBlankLine()
        {
            AppendStyledText(Environment.NewLine, SystemColors.ControlText, Color.White, _detailFont);
        }

        private void AppendStyledText(string text, Color foreColor, Color backColor, Font font)
        {
            _rtbDetails.SelectionStart = _rtbDetails.TextLength;
            _rtbDetails.SelectionLength = 0;
            _rtbDetails.SelectionColor = foreColor;
            _rtbDetails.SelectionBackColor = backColor;
            _rtbDetails.SelectionFont = font;
            _rtbDetails.AppendText(text);
            _rtbDetails.SelectionColor = _rtbDetails.ForeColor;
            _rtbDetails.SelectionBackColor = _rtbDetails.BackColor;
            _rtbDetails.SelectionFont = _rtbDetails.Font;
        }

        private Guid? GetSelectedReportId()
        {
            return (_treeReports.SelectedNode?.Tag as OperationReportItem)?.Id;
        }

        private static TreeNode FindNodeByReportId(TreeNodeCollection nodes, Guid? reportId)
        {
            if (!reportId.HasValue)
                return null;

            foreach (TreeNode node in nodes)
            {
                OperationReportItem report = node.Tag as OperationReportItem;
                if (report != null && report.Id == reportId.Value)
                    return node;

                TreeNode child = FindNodeByReportId(node.Nodes, reportId);
                if (child != null)
                    return child;
            }

            return null;
        }

        private static string BuildNodeText(OperationReportItem report)
        {
            string unreadPrefix = report.IsRead ? string.Empty : "[new] ";
            string title = string.IsNullOrWhiteSpace(report.Title) ? "Report" : report.Title.Trim();
            return $"{unreadPrefix}{report.CreatedAt:HH:mm:ss} {title}";
        }

        private static Color GetSeverityColor(OperationReportSeverity severity)
        {
            switch (severity)
            {
                case OperationReportSeverity.Error:
                    return Color.FromArgb(176, 32, 37);
                case OperationReportSeverity.Warning:
                    return Color.FromArgb(156, 101, 0);
                default:
                    return Color.FromArgb(36, 78, 120);
            }
        }

        private static Color GetBlockBackground(OperationReportChangeKind kind)
        {
            switch (kind)
            {
                case OperationReportChangeKind.Added:
                    return Color.FromArgb(235, 247, 235);
                case OperationReportChangeKind.Removed:
                    return Color.FromArgb(252, 236, 236);
                default:
                    return Color.FromArgb(240, 244, 249);
            }
        }

        private static Color GetDiffColor(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return SystemColors.ControlText;

            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("+"))
                return Color.FromArgb(31, 121, 31);
            if (trimmed.StartsWith("-"))
                return Color.FromArgb(176, 32, 37);
            if (trimmed.StartsWith("~"))
                return Color.FromArgb(36, 78, 120);

            return SystemColors.ControlText;
        }

        private void UpdateSplitterDistanceSafe()
        {
            if (_splitContainer == null || _splitContainer.IsDisposed)
                return;

            if (_splitContainer.IsHandleCreated == false)
                return;

            int minimumLeft = LeftPanelMinimumWidth;
            int minimumRight = RightPanelMinimumWidth;
            int maximumLeft = _splitContainer.Width - minimumRight;
            if (maximumLeft < minimumLeft)
                return;

            int preferredLeft = Math.Min(PreferredLeftPanelWidth, maximumLeft);
            int safeDistance = Math.Max(minimumLeft, preferredLeft);
            safeDistance = Math.Min(safeDistance, maximumLeft);

            if (_splitContainer.SplitterDistance != safeDistance)
                _splitContainer.SplitterDistance = safeDistance;

            if (_splitContainer.Panel1MinSize != minimumLeft)
                _splitContainer.Panel1MinSize = minimumLeft;

            if (_splitContainer.Panel2MinSize != minimumRight)
                _splitContainer.Panel2MinSize = minimumRight;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _detailRenderCts?.Cancel();
                _detailRenderCts?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
