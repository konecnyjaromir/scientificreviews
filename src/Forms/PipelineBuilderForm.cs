using ScientificReviews.Pipelines;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public sealed class PipelineBuilderForm : Form
    {
        private const int PreferredPipelinesPanelWidth = 620;
        private const int MinimumPipelinesPanelWidth = 520;
        private const int MinimumRightPanelWidth = 360;

        private readonly List<CustomPipelineDefinition> _pipelines;
        private readonly SplitContainer _mainSplitContainer;
        private readonly ListBox _pipelinesListBox;
        private readonly ListBox _stepsListBox;
        private readonly PropertyGrid _stepPropertyGrid;
        private readonly Button _renamePipelineButton;
        private readonly Button _deletePipelineButton;
        private readonly Button _addStepButton;
        private readonly Button _removeStepButton;
        private readonly Button _moveStepUpButton;
        private readonly Button _moveStepDownButton;
        private readonly ContextMenuStrip _addStepMenu;

        public PipelineBuilderForm(IEnumerable<CustomPipelineDefinition> pipelines)
        {
            _pipelines = PipelineDefinitionHelper.NormalizeCustomPipelines(pipelines)
                .Select(pipeline => pipeline.DeepClone())
                .ToList();

            Text = "Pipeline Builder";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1100, 620);
            Size = new Size(1200, 760);

            _mainSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1
            };

            Controls.Add(_mainSplitContainer);
            Controls.Add(BuildBottomPanel());

            TableLayoutPanel pipelinesPanel = BuildPipelinesPanel();
            TableLayoutPanel stepsPanel = BuildStepsPanel();

            SplitContainer rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 340
            };

            TableLayoutPanel propertyPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8)
            };
            propertyPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            propertyPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            propertyPanel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Step settings"
            }, 0, 0);

            _stepPropertyGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                HelpVisible = true,
                ToolbarVisible = false
            };
            _stepPropertyGrid.PropertyValueChanged += stepPropertyGrid_PropertyValueChanged;
            propertyPanel.Controls.Add(_stepPropertyGrid, 0, 1);

            rightSplit.Panel1.Controls.Add(stepsPanel);
            rightSplit.Panel2.Controls.Add(propertyPanel);

            _mainSplitContainer.Panel1.Controls.Add(pipelinesPanel);
            _mainSplitContainer.Panel2.Controls.Add(rightSplit);

            _pipelinesListBox = FindControl<ListBox>(pipelinesPanel, "pipelinesListBox");
            _stepsListBox = FindControl<ListBox>(stepsPanel, "stepsListBox");
            _renamePipelineButton = FindControl<Button>(pipelinesPanel, "renamePipelineButton");
            _deletePipelineButton = FindControl<Button>(pipelinesPanel, "deletePipelineButton");
            _addStepButton = FindControl<Button>(stepsPanel, "addStepButton");
            _removeStepButton = FindControl<Button>(stepsPanel, "removeStepButton");
            _moveStepUpButton = FindControl<Button>(stepsPanel, "moveStepUpButton");
            _moveStepDownButton = FindControl<Button>(stepsPanel, "moveStepDownButton");

            _addStepMenu = BuildAddStepMenu();
            Shown += PipelineBuilderForm_Shown;
            Resize += PipelineBuilderForm_Resize;

            _pipelinesListBox.SelectedIndexChanged += pipelinesListBox_SelectedIndexChanged;
            _stepsListBox.SelectedIndexChanged += stepsListBox_SelectedIndexChanged;

            RefreshPipelineList();
            if (_pipelinesListBox.Items.Count > 0)
                _pipelinesListBox.SelectedIndex = 0;
            else
                UpdateUiState();
        }

        private void PipelineBuilderForm_Shown(object sender, EventArgs e)
        {
            ApplyPreferredPipelinesPanelWidth();
        }

        private void PipelineBuilderForm_Resize(object sender, EventArgs e)
        {
            ApplyPreferredPipelinesPanelWidth();
        }

        public List<CustomPipelineDefinition> GetPipelines()
        {
            return _pipelines
                .Select(pipeline => pipeline.DeepClone())
                .ToList();
        }

        private TableLayoutPanel BuildPipelinesPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            panel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Pipelines"
            }, 0, 0);

            ListBox listBox = new ListBox
            {
                Name = "pipelinesListBox",
                Dock = DockStyle.Fill,
                IntegralHeight = false
            };
            panel.Controls.Add(listBox, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false
            };
            buttons.Controls.Add(CreateButton("New", "newPipelineButton", newPipelineButton_Click));
            buttons.Controls.Add(CreateButton("Rename", "renamePipelineButton", renamePipelineButton_Click));
            buttons.Controls.Add(CreateButton("Delete", "deletePipelineButton", deletePipelineButton_Click));
            panel.Controls.Add(buttons, 0, 2);

            return panel;
        }

        private TableLayoutPanel BuildStepsPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            panel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Steps"
            }, 0, 0);

            ListBox listBox = new ListBox
            {
                Name = "stepsListBox",
                Dock = DockStyle.Fill,
                IntegralHeight = false
            };
            panel.Controls.Add(listBox, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = true
            };
            buttons.Controls.Add(CreateButton("Add step", "addStepButton", addStepButton_Click));
            buttons.Controls.Add(CreateButton("Remove step", "removeStepButton", removeStepButton_Click));
            buttons.Controls.Add(CreateButton("Move up", "moveStepUpButton", moveStepUpButton_Click));
            buttons.Controls.Add(CreateButton("Move down", "moveStepDownButton", moveStepDownButton_Click));
            panel.Controls.Add(buttons, 0, 2);

            return panel;
        }

        private Panel BuildBottomPanel()
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                Padding = new Padding(8)
            };

            Button okButton = new Button
            {
                DialogResult = DialogResult.OK,
                Text = "OK",
                Width = 100,
                Dock = DockStyle.Right
            };
            Button cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Text = "Cancel",
                Width = 100,
                Dock = DockStyle.Right
            };

            panel.Controls.Add(cancelButton);
            panel.Controls.Add(okButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;

            return panel;
        }

        private ContextMenuStrip BuildAddStepMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            foreach (PipelineStepKind kind in Enum.GetValues(typeof(PipelineStepKind)).Cast<PipelineStepKind>())
            {
                ToolStripMenuItem item = new ToolStripMenuItem
                {
                    Text = PipelineDefinitionHelper.GetStepDisplayName(kind),
                    Tag = kind
                };
                item.Click += addStepMenuItem_Click;
                menu.Items.Add(item);
            }

            return menu;
        }

        private static T FindControl<T>(Control root, string name) where T : Control
        {
            foreach (Control control in root.Controls)
            {
                if (control is T matched && string.Equals(control.Name, name, StringComparison.Ordinal))
                    return matched;

                T nested = FindControl<T>(control, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static Button CreateButton(string text, string name, EventHandler handler)
        {
            Button button = new Button
            {
                Text = text,
                Name = name,
                AutoSize = true,
                Margin = new Padding(0, 0, 8, 0)
            };
            button.Click += handler;
            return button;
        }

        private void ApplyPreferredPipelinesPanelWidth()
        {
            if (_mainSplitContainer == null || _mainSplitContainer.IsDisposed)
                return;

            int availableWidth = _mainSplitContainer.ClientSize.Width;
            if (availableWidth <= 0)
                return;

            int requiredMinimumWidth = MinimumPipelinesPanelWidth + MinimumRightPanelWidth + _mainSplitContainer.SplitterWidth;
            if (availableWidth < requiredMinimumWidth)
            {
                if (_mainSplitContainer.Panel1MinSize != 0)
                    _mainSplitContainer.Panel1MinSize = 0;

                if (_mainSplitContainer.Panel2MinSize != 0)
                    _mainSplitContainer.Panel2MinSize = 0;

                return;
            }

            if (_mainSplitContainer.Panel1MinSize != MinimumPipelinesPanelWidth)
                _mainSplitContainer.Panel1MinSize = MinimumPipelinesPanelWidth;

            if (_mainSplitContainer.Panel2MinSize != MinimumRightPanelWidth)
                _mainSplitContainer.Panel2MinSize = MinimumRightPanelWidth;

            int maxAllowedWidth = availableWidth - MinimumRightPanelWidth - _mainSplitContainer.SplitterWidth;

            _mainSplitContainer.SplitterDistance = Math.Max(
                MinimumPipelinesPanelWidth,
                Math.Min(PreferredPipelinesPanelWidth, maxAllowedWidth));
        }

        private CustomPipelineDefinition SelectedPipeline =>
            _pipelinesListBox?.SelectedItem as CustomPipelineDefinition;

        private PipelineStepDefinition SelectedStep =>
            _stepsListBox?.SelectedItem as PipelineStepDefinition;

        private void RefreshPipelineList()
        {
            CustomPipelineDefinition selected = SelectedPipeline;
            _pipelinesListBox.BeginUpdate();
            _pipelinesListBox.Items.Clear();
            foreach (CustomPipelineDefinition pipeline in _pipelines)
                _pipelinesListBox.Items.Add(pipeline);
            _pipelinesListBox.EndUpdate();

            if (selected != null)
            {
                int selectedIndex = _pipelines.FindIndex(item => string.Equals(item.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
                if (selectedIndex >= 0)
                    _pipelinesListBox.SelectedIndex = selectedIndex;
            }

            UpdateUiState();
        }

        private void RefreshStepsList()
        {
            PipelineStepDefinition selectedStep = SelectedStep;
            CustomPipelineDefinition pipeline = SelectedPipeline;

            _stepsListBox.BeginUpdate();
            _stepsListBox.Items.Clear();
            if (pipeline != null)
            {
                foreach (PipelineStepDefinition step in pipeline.Steps)
                    _stepsListBox.Items.Add(step);
            }
            _stepsListBox.EndUpdate();

            if (pipeline != null && selectedStep != null)
            {
                int stepIndex = pipeline.Steps.IndexOf(selectedStep);
                if (stepIndex >= 0 && stepIndex < _stepsListBox.Items.Count)
                    _stepsListBox.SelectedIndex = stepIndex;
            }

            UpdateUiState();
        }

        private void UpdateUiState()
        {
            bool hasPipeline = SelectedPipeline != null;
            bool hasStep = SelectedStep != null;
            int selectedStepIndex = hasPipeline && hasStep ? SelectedPipeline.Steps.IndexOf(SelectedStep) : -1;

            _renamePipelineButton.Enabled = hasPipeline;
            _deletePipelineButton.Enabled = hasPipeline;
            _addStepButton.Enabled = hasPipeline;
            _removeStepButton.Enabled = hasStep;
            _moveStepUpButton.Enabled = selectedStepIndex > 0;
            _moveStepDownButton.Enabled = hasPipeline && hasStep && selectedStepIndex >= 0 && selectedStepIndex < SelectedPipeline.Steps.Count - 1;
            _stepPropertyGrid.SelectedObject = SelectedStep;
        }

        private void pipelinesListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshStepsList();
        }

        private void stepsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUiState();
        }

        private void stepPropertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            int selectedIndex = _stepsListBox.SelectedIndex;
            RefreshStepsList();
            if (selectedIndex >= 0 && selectedIndex < _stepsListBox.Items.Count)
                _stepsListBox.SelectedIndex = selectedIndex;
        }

        private void newPipelineButton_Click(object sender, EventArgs e)
        {
            string name = PromptForPipelineName("New Pipeline", "New pipeline");
            if (string.IsNullOrWhiteSpace(name))
                return;

            CustomPipelineDefinition pipeline = new CustomPipelineDefinition
            {
                Name = name,
                Steps = new List<PipelineStepDefinition>
                {
                    new PipelineStepDefinition
                    {
                        Kind = PipelineStepKind.NormalizeDoi
                    }
                }
            };

            _pipelines.Add(pipeline);
            RefreshPipelineList();
            _pipelinesListBox.SelectedItem = pipeline;
        }

        private void renamePipelineButton_Click(object sender, EventArgs e)
        {
            CustomPipelineDefinition pipeline = SelectedPipeline;
            if (pipeline == null)
                return;

            string name = PromptForPipelineName("Rename Pipeline", pipeline.Name, pipeline.Id);
            if (string.IsNullOrWhiteSpace(name))
                return;

            pipeline.Name = name;
            RefreshPipelineList();
            _pipelinesListBox.SelectedItem = pipeline;
        }

        private void deletePipelineButton_Click(object sender, EventArgs e)
        {
            CustomPipelineDefinition pipeline = SelectedPipeline;
            if (pipeline == null)
                return;

            DialogResult result = MessageBox.Show(
                this,
                $"Delete pipeline \"{pipeline.Name}\"?",
                "Delete Pipeline",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            int nextIndex = Math.Max(0, _pipelinesListBox.SelectedIndex - 1);
            _pipelines.Remove(pipeline);
            RefreshPipelineList();
            if (_pipelinesListBox.Items.Count > 0)
                _pipelinesListBox.SelectedIndex = Math.Min(nextIndex, _pipelinesListBox.Items.Count - 1);
        }

        private void addStepButton_Click(object sender, EventArgs e)
        {
            if (SelectedPipeline == null)
                return;

            _addStepMenu.Show(_addStepButton, new Point(0, _addStepButton.Height));
        }

        private void addStepMenuItem_Click(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem item) || !(item.Tag is PipelineStepKind kind))
                return;

            CustomPipelineDefinition pipeline = SelectedPipeline;
            if (pipeline == null)
                return;

            PipelineStepDefinition step = new PipelineStepDefinition
            {
                Kind = kind,
                MetadataMode = kind == PipelineStepKind.FetchMetadata
                    ? PipelineMetadataMode.UseCurrentSettings
                    : PipelineMetadataMode.UseCurrentSettings
            };

            pipeline.Steps.Add(step);
            RefreshStepsList();
            _stepsListBox.SelectedItem = step;
        }

        private void removeStepButton_Click(object sender, EventArgs e)
        {
            CustomPipelineDefinition pipeline = SelectedPipeline;
            PipelineStepDefinition step = SelectedStep;
            if (pipeline == null || step == null)
                return;

            int nextIndex = Math.Max(0, _stepsListBox.SelectedIndex - 1);
            pipeline.Steps.Remove(step);
            RefreshStepsList();
            if (_stepsListBox.Items.Count > 0)
                _stepsListBox.SelectedIndex = Math.Min(nextIndex, _stepsListBox.Items.Count - 1);
        }

        private void moveStepUpButton_Click(object sender, EventArgs e)
        {
            MoveSelectedStep(-1);
        }

        private void moveStepDownButton_Click(object sender, EventArgs e)
        {
            MoveSelectedStep(1);
        }

        private void MoveSelectedStep(int direction)
        {
            CustomPipelineDefinition pipeline = SelectedPipeline;
            PipelineStepDefinition step = SelectedStep;
            if (pipeline == null || step == null)
                return;

            int oldIndex = pipeline.Steps.IndexOf(step);
            int newIndex = oldIndex + direction;
            if (oldIndex < 0 || newIndex < 0 || newIndex >= pipeline.Steps.Count)
                return;

            pipeline.Steps.RemoveAt(oldIndex);
            pipeline.Steps.Insert(newIndex, step);
            RefreshStepsList();
            _stepsListBox.SelectedIndex = newIndex;
        }

        private string PromptForPipelineName(string title, string initialValue, string existingPipelineId = null)
        {
            using (InputBoxForm form = new InputBoxForm())
            {
                form.Text = title;
                form.SetText(initialValue ?? string.Empty);
                if (form.ShowDialog(this) != DialogResult.OK)
                    return null;

                string name = (form.GetText() ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show(this, "Pipeline name cannot be empty.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }

                bool nameExists = _pipelines.Any(pipeline =>
                    !string.Equals(pipeline.Id, existingPipelineId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(pipeline.Name, name, StringComparison.OrdinalIgnoreCase));

                if (nameExists)
                {
                    MessageBox.Show(this, "A pipeline with the same name already exists.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }

                return name;
            }
        }
    }
}
