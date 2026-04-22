using ScientificReviews.Helpers;
using ScientificReviews.Logs;
using ScientificReviews.Pipelines;
using ScientificReviews.Reports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private sealed class PipelineStepExecutionResult
        {
            public string Details { get; set; }
            public string SkippedReason { get; set; }
        }

        private ToolStripMenuItem _pipelinesToolStripMenuItem;
        private ToolStripSeparator _pipelinesTopSeparator;
        private ToolStripMenuItem _pipelineBuilderToolStripMenuItem;
        private ToolStripMenuItem _pipelineRunToolStripMenuItem;

        private void InitializePipelinesUi()
        {
            if (menuStrip1 == null || _pipelinesToolStripMenuItem != null)
                return;

            _pipelinesToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "pipelinesToolStripMenuItem",
                Text = "Pipelines"
            };

            _pipelineBuilderToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "pipelineBuilderToolStripMenuItem",
                Text = "Pipeline Builder"
            };
            _pipelineBuilderToolStripMenuItem.Click += pipelineBuilderToolStripMenuItem_Click;

            _pipelineRunToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "pipelineRunToolStripMenuItem",
                Text = "Run"
            };

            _pipelinesTopSeparator = new ToolStripSeparator
            {
                Name = "pipelinesTopSeparator"
            };

            if (databaseToolStripMenuItem != null)
            {
                databaseToolStripMenuItem.DropDownItems.Remove(autofixToolStripMenuItem);
                databaseToolStripMenuItem.DropDownItems.Remove(autofixModeToolStripMenuItem);
            }

            _pipelinesToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                autofixToolStripMenuItem,
                autofixModeToolStripMenuItem,
                _pipelinesTopSeparator,
                _pipelineBuilderToolStripMenuItem,
                _pipelineRunToolStripMenuItem
            });

            int databaseIndex = menuStrip1.Items.IndexOf(databaseToolStripMenuItem);
            int insertIndex = databaseIndex >= 0 ? databaseIndex + 1 : menuStrip1.Items.Count;
            menuStrip1.Items.Insert(insertIndex, _pipelinesToolStripMenuItem);

            RefreshPipelinesRunMenu();
        }

        private void RefreshPipelinesRunMenu()
        {
            if (_pipelineRunToolStripMenuItem == null)
                return;

            _pipelineRunToolStripMenuItem.DropDownItems.Clear();

            List<CustomPipelineDefinition> pipelines = PipelineDefinitionHelper.NormalizeCustomPipelines(
                Program.AppSettings?.Data?.CustomPipelines);

            if (pipelines.Count == 0)
            {
                _pipelineRunToolStripMenuItem.DropDownItems.Add(new ToolStripMenuItem
                {
                    Text = "(No custom pipelines)",
                    Enabled = false
                });
                return;
            }

            foreach (CustomPipelineDefinition pipeline in pipelines)
            {
                ToolStripMenuItem item = new ToolStripMenuItem
                {
                    Text = pipeline.Name,
                    Tag = pipeline.DeepClone(),
                    ToolTipText = PipelineDefinitionHelper.BuildPipelineSummary(pipeline.Steps)
                };
                item.Click += customPipelineRunToolStripMenuItem_Click;
                _pipelineRunToolStripMenuItem.DropDownItems.Add(item);
            }
        }

        private async void customPipelineRunToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem item) || !(item.Tag is CustomPipelineDefinition pipeline))
                return;

            if (!ConfirmCustomPipelineRun(pipeline))
                return;

            await StartPipelineOperationAsync(
                pipeline.DeepClone(),
                startedAutomatically: false,
                operationKey: "custom-pipeline-" + pipeline.Id,
                operationName: pipeline.Name);
        }

        private void pipelineBuilderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<CustomPipelineDefinition> currentPipelines = PipelineDefinitionHelper.NormalizeCustomPipelines(
                Program.AppSettings?.Data?.CustomPipelines);

            using (PipelineBuilderForm form = new PipelineBuilderForm(currentPipelines))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                    return;

                Program.AppSettings.Data.CustomPipelines = PipelineDefinitionHelper.NormalizeCustomPipelines(form.GetPipelines());
                Program.AppSettings.SaveSettings();
                RefreshPipelinesRunMenu();
                lblStatus.Text = "Pipelines updated.";
            }
        }

        private bool ConfirmCustomPipelineRun(CustomPipelineDefinition pipeline)
        {
            if (pipeline == null)
                return false;

            string summary = BuildPipelineSummaryLines(pipeline.Steps);
            DialogResult response = MessageBox.Show(
                this,
                $"Pipeline \"{pipeline.Name}\" will run these steps:{Environment.NewLine}{Environment.NewLine}{summary}{Environment.NewLine}{Environment.NewLine}This operation may modify records irreversibly. Do you want to continue?",
                "Run Pipeline",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return response == DialogResult.Yes;
        }

        private static string BuildPipelineSummaryLines(IEnumerable<PipelineStepDefinition> steps)
        {
            string[] lines = (steps ?? Enumerable.Empty<PipelineStepDefinition>())
                .Where(step => step != null)
                .Select(step => "- " + PipelineDefinitionHelper.GetStepDisplayText(step))
                .ToArray();

            return lines.Length == 0
                ? "- No steps configured"
                : string.Join(Environment.NewLine, lines);
        }

        private static CustomPipelineDefinition CreateBuiltInPreprocessingPipeline(AutoPreprocessingMode mode)
        {
            switch (mode)
            {
                case AutoPreprocessingMode.Fast:
                    return new CustomPipelineDefinition
                    {
                        Id = "builtin-fast",
                        Name = "Fast",
                        Steps = new List<PipelineStepDefinition>
                        {
                            new PipelineStepDefinition { Kind = PipelineStepKind.NormalizeDoi },
                            new PipelineStepDefinition { Kind = PipelineStepKind.NormalizePageTag },
                            new PipelineStepDefinition { Kind = PipelineStepKind.CreateEntryKeys },
                            new PipelineStepDefinition { Kind = PipelineStepKind.AutoPairPdfs }
                        }
                    };
                case AutoPreprocessingMode.Normal:
                    return new CustomPipelineDefinition
                    {
                        Id = "builtin-normal",
                        Name = "Normal",
                        Steps = new List<PipelineStepDefinition>
                        {
                            new PipelineStepDefinition { Kind = PipelineStepKind.NormalizeDoi },
                            new PipelineStepDefinition { Kind = PipelineStepKind.FetchMetadata, MetadataMode = PipelineMetadataMode.UseCurrentSettings },
                            new PipelineStepDefinition { Kind = PipelineStepKind.RemoveDuplicatesByTitle },
                            new PipelineStepDefinition { Kind = PipelineStepKind.RemoveDuplicatesByDoi },
                            new PipelineStepDefinition { Kind = PipelineStepKind.NormalizePageTag },
                            new PipelineStepDefinition { Kind = PipelineStepKind.CreateEntryKeys },
                            new PipelineStepDefinition { Kind = PipelineStepKind.AutoPairPdfs },
                            new PipelineStepDefinition { Kind = PipelineStepKind.AutoupdateJcr }
                        }
                    };
                case AutoPreprocessingMode.Deep:
                    return new CustomPipelineDefinition
                    {
                        Id = "builtin-deep",
                        Name = "Deep",
                        Steps = new List<PipelineStepDefinition>
                        {
                            new PipelineStepDefinition { Kind = PipelineStepKind.NormalizeDoi },
                            new PipelineStepDefinition { Kind = PipelineStepKind.FetchMetadata, MetadataMode = PipelineMetadataMode.All },
                            new PipelineStepDefinition { Kind = PipelineStepKind.RemoveDuplicatesByTitle },
                            new PipelineStepDefinition { Kind = PipelineStepKind.RemoveDuplicatesByDoi },
                            new PipelineStepDefinition { Kind = PipelineStepKind.NormalizePageTag },
                            new PipelineStepDefinition { Kind = PipelineStepKind.CreateEntryKeys },
                            new PipelineStepDefinition { Kind = PipelineStepKind.AutoPairPdfs },
                            new PipelineStepDefinition { Kind = PipelineStepKind.AutoupdateJcr }
                        }
                    };
                default:
                    return null;
            }
        }

        private async Task StartPipelineOperationAsync(
            CustomPipelineDefinition pipeline,
            bool startedAutomatically,
            string operationKey,
            string operationName)
        {
            pipeline = pipeline?.DeepClone();
            if (pipeline == null || pipeline.Steps == null || pipeline.Steps.Count == 0)
            {
                if (!startedAutomatically)
                    lblStatus.Text = "Selected pipeline does not contain any steps.";
                return;
            }

            if (entries.Count == 0)
            {
                if (!startedAutomatically)
                    lblStatus.Text = $"No records available for {operationName.ToLowerInvariant()}.";
                return;
            }

            string operationDetails = PipelineDefinitionHelper.BuildPipelineSummary(pipeline.Steps);
            StatusStripOperationHandle operation = StartTrackedOperation(
                operationKey,
                operationName,
                operationDetails,
                startedAutomatically);
            if (operation == null)
                return;

            ProcessLogScope log = BeginProcessLog(operationName, $"Records: {entries.Count}, pipeline: {pipeline.Name}");
            List<string> skippedSteps = new List<string>();
            EntryChangeSnapshot overallChangeSnapshot = CaptureEntryChanges(entries.ToArray());

            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            using (ReportScopeContext reportScope = BeginReportScope(
                operationName,
                $"{operationName} started.",
                $"Pipeline: {pipeline.Name}{Environment.NewLine}Records: {entries.Count}{Environment.NewLine}Steps: {pipeline.Steps.Count}"))
            {
                operation.RegisterCancellation(cancellation.Cancel);

                try
                {
                    for (int i = 0; i < pipeline.Steps.Count; i++)
                    {
                        PipelineStepDefinition step = pipeline.Steps[i];
                        string stepDisplayText = PipelineDefinitionHelper.GetStepDisplayText(step);

                        operation.Report(stepDisplayText, "Running...", i + 1, pipeline.Steps.Count, false);
                        LogProcessProgress(log, "Pipeline step started.", stepDisplayText, i + 1, pipeline.Steps.Count);
                        cancellation.Token.ThrowIfCancellationRequested();

                        PipelineStepExecutionResult stepResult = await ExecutePipelineStepAsync(
                            step,
                            operationKey,
                            i + 1,
                            startedAutomatically,
                            cancellation.Token);

                        if (!string.IsNullOrWhiteSpace(stepResult?.SkippedReason))
                            skippedSteps.Add($"{PipelineDefinitionHelper.GetStepDisplayName(step.Kind)} ({stepResult.SkippedReason})");

                        operation.Report(
                            stepDisplayText,
                            stepResult?.Details ?? "Completed.",
                            i + 1,
                            pipeline.Steps.Count,
                            false);
                        LogProcessProgress(log, "Pipeline step completed.", stepResult?.Details ?? stepDisplayText, i + 1, pipeline.Steps.Count);
                    }

                    EntryChangeReport overallChangeReport = BuildEntryChangeReport(overallChangeSnapshot);
                    string details = skippedSteps.Count == 0
                        ? $"{operationName} completed all steps."
                        : "Skipped: " + string.Join(", ", skippedSteps) + ".";

                    operation.Complete($"{operationName} finished.", details);
                    log.Complete(details);
                    lblStatus.Text = skippedSteps.Count == 0
                        ? $"{operationName} finished."
                        : $"{operationName} finished. Skipped: {string.Join(", ", skippedSteps)}.";
                    reportScope.Complete(
                        $"{operationName} finished.",
                        details,
                        skippedSteps.Count == 0 ? OperationReportSeverity.Info : OperationReportSeverity.Warning,
                        overallChangeReport);
                }
                catch (OperationCanceledException)
                {
                    operation.Cancel("Cancelled", $"{operationName} was stopped by user.");
                    lblStatus.Text = $"{operationName} cancelled.";
                    log.Complete($"{operationName} cancelled.");
                    reportScope.Complete($"{operationName} cancelled.", null, OperationReportSeverity.Warning);
                }
                catch (Exception ex)
                {
                    operation.Fail(ex, "Failed");
                    lblStatus.Text = ex.Message;
                    log.Fail(ex, $"{operationName} failed.");
                    reportScope.Complete($"{operationName} failed.", ex.Message, OperationReportSeverity.Error);
                }
                finally
                {
                    log.Dispose();
                }
            }
        }

        private async Task<PipelineStepExecutionResult> ExecutePipelineStepAsync(
            PipelineStepDefinition step,
            string parentOperationKey,
            int stepIndex,
            bool startedAutomatically,
            CancellationToken cancellationToken)
        {
            string stepName = PipelineDefinitionHelper.GetStepDisplayName(step.Kind);
            switch (step.Kind)
            {
                case PipelineStepKind.NormalizeDoi:
                    RunNormalizeDoiOperation(entries.ToArray());
                    return new PipelineStepExecutionResult { Details = "DOI normalization completed." };
                case PipelineStepKind.FetchMetadata:
                    await StartFetchMetadataOperationAsync(
                        entries.ToArray(),
                        false,
                        $"{parentOperationKey}-fetch-metadata-{stepIndex}",
                        stepName,
                        ResolvePipelineMetadataMode(step.MetadataMode),
                        null,
                        cancellationToken);
                    return new PipelineStepExecutionResult
                    {
                        Details = $"Metadata scope: {PipelineDefinitionHelper.GetMetadataModeDisplayName(step.MetadataMode)}."
                    };
                case PipelineStepKind.RemoveDuplicatesByTitle:
                    RunRemoveDuplicateEntriesByTagOperation("title");
                    return new PipelineStepExecutionResult { Details = "Duplicate removal by title completed." };
                case PipelineStepKind.RemoveDuplicatesByDoi:
                    RunRemoveDuplicateEntriesByTagOperation("doi");
                    return new PipelineStepExecutionResult { Details = "Duplicate removal by DOI completed." };
                case PipelineStepKind.NormalizePageTag:
                    RunNormalizePageTagOperation(entries.ToArray());
                    return new PipelineStepExecutionResult { Details = "Page-tag normalization completed." };
                case PipelineStepKind.CreateEntryKeys:
                    RunCreateEntryKeysOperation(entries.ToArray());
                    return new PipelineStepExecutionResult { Details = "Entry key generation completed." };
                case PipelineStepKind.AutoPairPdfs:
                    if (string.IsNullOrWhiteSpace(Program.AppSettings.Data.PdfFolder))
                    {
                        return new PipelineStepExecutionResult
                        {
                            Details = "Skipped: PDF folder is not set.",
                            SkippedReason = "PDF folder is not set"
                        };
                    }

                    await StartAutoPairOperationAsync(
                        entries.ToArray(),
                        $"{parentOperationKey}-auto-pair-pdfs-{stepIndex}",
                        stepName,
                        Program.AppSettings.Data.PdfFolder,
                        startedAutomatically,
                        cancellationToken);
                    return new PipelineStepExecutionResult { Details = "PDF matching completed." };
                case PipelineStepKind.AutoupdateJcr:
                    if (string.IsNullOrWhiteSpace(Program.AppSettings.Data.JcrApiKey))
                    {
                        return new PipelineStepExecutionResult
                        {
                            Details = "Skipped: JCR API key is not set.",
                            SkippedReason = "JCR API key is not set"
                        };
                    }

                    await StartAutoupdateJcrOperationAsync(cancellationToken);
                    return new PipelineStepExecutionResult { Details = "JCR update completed." };
                default:
                    throw new InvalidOperationException("Unsupported pipeline step: " + step.Kind);
            }
        }

        private static MetadataScreenMode? ResolvePipelineMetadataMode(PipelineMetadataMode mode)
        {
            switch (mode)
            {
                case PipelineMetadataMode.UseCurrentSettings:
                    return null;
                case PipelineMetadataMode.OnlyMissing:
                    return MetadataScreenMode.OnlyMissing;
                case PipelineMetadataMode.All:
                    return MetadataScreenMode.All;
                case PipelineMetadataMode.OnlyMissingAndArxivDois:
                    return MetadataScreenMode.OnlyMissingAndArxivDois;
                default:
                    return null;
            }
        }
    }
}
