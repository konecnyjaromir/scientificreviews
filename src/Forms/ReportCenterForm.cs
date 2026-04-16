using ScientificReviews.Reports;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public sealed class ReportCenterForm : Form
    {
        private const int LeftPanelMinimumWidth = 220;
        private const int RightPanelMinimumWidth = 360;
        private const int PreferredLeftPanelWidth = 280;

        private readonly OperationReportCenter _reportCenter;
        private bool _isRefreshing;
        private SplitContainer _splitContainer;
        private ListBox _lstReports;
        private TextBox _txtDetails;
        private Button _btnMarkRead;
        private Button _btnMarkAllRead;
        private Button _btnClear;
        private Button _btnClose;

        public ReportCenterForm(OperationReportCenter reportCenter)
        {
            _reportCenter = reportCenter ?? throw new ArgumentNullException(nameof(reportCenter));
            InitializeComponent();
            UpdateSplitterDistanceSafe();
            LoadReports();
        }

        private void InitializeComponent()
        {
            _splitContainer = new SplitContainer();
            _lstReports = new ListBox();
            _txtDetails = new TextBox();
            _btnMarkRead = new Button();
            _btnMarkAllRead = new Button();
            _btnClear = new Button();
            _btnClose = new Button();
            FlowLayoutPanel buttons = new FlowLayoutPanel();

            SuspendLayout();

            _splitContainer.Dock = DockStyle.Fill;
            _splitContainer.Orientation = Orientation.Vertical;

            _lstReports.Dock = DockStyle.Fill;
            _lstReports.IntegralHeight = false;
            _lstReports.SelectedIndexChanged += LstReports_SelectedIndexChanged;

            _txtDetails.Dock = DockStyle.Fill;
            _txtDetails.Multiline = true;
            _txtDetails.ReadOnly = true;
            _txtDetails.ScrollBars = ScrollBars.Both;
            _txtDetails.Font = new Font("Consolas", 9F);

            _splitContainer.Panel1.Controls.Add(_lstReports);
            _splitContainer.Panel2.Controls.Add(_txtDetails);

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
            ClientSize = new Size(920, 520);
            Controls.Add(_splitContainer);
            Controls.Add(buttons);
            MinimizeBox = false;
            MinimumSize = new Size(760, 420);
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

        private void LoadReports(Guid? selectedId = null)
        {
            _isRefreshing = true;
            List<OperationReportItem> reports = _reportCenter.GetSnapshot();
            Guid? effectiveSelection = selectedId;
            if (!effectiveSelection.HasValue && _lstReports.SelectedItem is OperationReportItem current)
                effectiveSelection = current.Id;

            _lstReports.BeginUpdate();
            _lstReports.Items.Clear();
            foreach (OperationReportItem report in reports)
                _lstReports.Items.Add(report);
            _lstReports.EndUpdate();

            if (_lstReports.Items.Count == 0)
            {
                _txtDetails.Text = "No reports available.";
                _btnMarkRead.Enabled = false;
                _isRefreshing = false;
                return;
            }

            int selectedIndex = 0;
            if (effectiveSelection.HasValue)
            {
                for (int i = 0; i < _lstReports.Items.Count; i++)
                {
                    if (((OperationReportItem)_lstReports.Items[i]).Id == effectiveSelection.Value)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            _lstReports.SelectedIndex = selectedIndex;
            OperationReportItem selectedReport = _lstReports.SelectedItem as OperationReportItem;
            _txtDetails.Text = BuildDetailsText(selectedReport);
            _btnMarkRead.Enabled = selectedReport != null && !selectedReport.IsRead;
            _isRefreshing = false;
        }

        private void LstReports_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isRefreshing)
                return;

            OperationReportItem report = _lstReports.SelectedItem as OperationReportItem;
            if (report == null)
            {
                _txtDetails.Text = "No report selected.";
                _btnMarkRead.Enabled = false;
                return;
            }

            _reportCenter.MarkRead(report.Id);
            LoadReports(report.Id);
            report = _lstReports.SelectedItem as OperationReportItem;
            _txtDetails.Text = BuildDetailsText(report);
            _btnMarkRead.Enabled = report != null && !report.IsRead;
        }

        private void BtnMarkRead_Click(object sender, EventArgs e)
        {
            OperationReportItem report = _lstReports.SelectedItem as OperationReportItem;
            if (report == null)
                return;

            _reportCenter.MarkRead(report.Id);
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

        private static string BuildDetailsText(OperationReportItem report)
        {
            if (report == null)
                return "No report selected.";

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(report.Title ?? "Report");
            builder.AppendLine(new string('=', 72));
            builder.AppendLine($"Created: {report.CreatedAt:G}");
            builder.AppendLine($"Severity: {report.Severity}");
            builder.AppendLine($"Status: {(report.IsRead ? "Read" : "Unread")}");

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

            if (report.Changes.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine($"Changes ({report.Changes.Count})");

                foreach (OperationReportChange change in report.Changes)
                {
                    builder.AppendLine($"[{change.RecordLabel}] {change.Summary}");
                    if (string.IsNullOrWhiteSpace(change.Details) == false)
                        builder.AppendLine(change.Details.Trim());
                    builder.AppendLine();
                }
            }

            return builder.ToString().TrimEnd();
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
    }
}
