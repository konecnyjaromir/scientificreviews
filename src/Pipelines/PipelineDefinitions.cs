using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ScientificReviews.Pipelines
{
    public enum PipelineStepKind
    {
        NormalizeDoi,
        FetchMetadata,
        RemoveDuplicatesByTitle,
        RemoveDuplicatesByDoi,
        NormalizePageTag,
        CreateEntryKeys,
        AutoPairPdfs,
        AutoupdateJcr
    }

    public enum PipelineMetadataMode
    {
        UseCurrentSettings,
        OnlyMissing,
        All,
        OnlyMissingAndArxivDois
    }

    public sealed class PipelineStepDefinition
    {
        [Browsable(true)]
        [DisplayName("Step")]
        [Description("Pipeline step to execute.")]
        public PipelineStepKind Kind { get; set; } = PipelineStepKind.NormalizeDoi;

        [Browsable(true)]
        [DisplayName("Metadata scope")]
        [Description("Used only by Fetch metadata steps. Other steps ignore this setting.")]
        public PipelineMetadataMode MetadataMode { get; set; } = PipelineMetadataMode.UseCurrentSettings;

        public PipelineStepDefinition DeepClone()
        {
            return new PipelineStepDefinition
            {
                Kind = Kind,
                MetadataMode = MetadataMode
            };
        }

        public override string ToString()
        {
            return PipelineDefinitionHelper.GetStepDisplayText(this);
        }
    }

    public sealed class CustomPipelineDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "New pipeline";
        public List<PipelineStepDefinition> Steps { get; set; } = new List<PipelineStepDefinition>();

        public CustomPipelineDefinition DeepClone()
        {
            return new CustomPipelineDefinition
            {
                Id = Id,
                Name = Name,
                Steps = (Steps ?? new List<PipelineStepDefinition>())
                    .Where(step => step != null)
                    .Select(step => step.DeepClone())
                    .ToList()
            };
        }

        public override string ToString()
        {
            return Name ?? "Unnamed pipeline";
        }
    }

    public static class PipelineDefinitionHelper
    {
        public static string GetStepDisplayName(PipelineStepKind kind)
        {
            switch (kind)
            {
                case PipelineStepKind.NormalizeDoi:
                    return "Normalize DOI";
                case PipelineStepKind.FetchMetadata:
                    return "Fetch metadata";
                case PipelineStepKind.RemoveDuplicatesByTitle:
                    return "Remove duplicates by title";
                case PipelineStepKind.RemoveDuplicatesByDoi:
                    return "Remove duplicates by DOI";
                case PipelineStepKind.NormalizePageTag:
                    return "Normalize page-tag";
                case PipelineStepKind.CreateEntryKeys:
                    return "Create entry keys";
                case PipelineStepKind.AutoPairPdfs:
                    return "Auto-pair PDFs";
                case PipelineStepKind.AutoupdateJcr:
                    return "Autoupdate JCR";
                default:
                    return kind.ToString();
            }
        }

        public static string GetMetadataModeDisplayName(PipelineMetadataMode mode)
        {
            switch (mode)
            {
                case PipelineMetadataMode.UseCurrentSettings:
                    return "Settings";
                case PipelineMetadataMode.OnlyMissing:
                    return "Only missing";
                case PipelineMetadataMode.All:
                    return "All";
                case PipelineMetadataMode.OnlyMissingAndArxivDois:
                    return "Only missing + arXiv DOIs";
                default:
                    return mode.ToString();
            }
        }

        public static string GetStepDisplayText(PipelineStepDefinition step)
        {
            if (step == null)
                return "Unnamed step";

            string stepName = GetStepDisplayName(step.Kind);
            if (step.Kind != PipelineStepKind.FetchMetadata)
                return stepName;

            return string.Format("{0} ({1})", stepName, GetMetadataModeDisplayName(step.MetadataMode));
        }

        public static string BuildPipelineSummary(IEnumerable<PipelineStepDefinition> steps)
        {
            List<string> parts = (steps ?? Enumerable.Empty<PipelineStepDefinition>())
                .Where(step => step != null)
                .Select(GetStepDisplayText)
                .ToList();

            return parts.Count == 0
                ? "No steps configured."
                : string.Join(" -> ", parts);
        }

        public static List<CustomPipelineDefinition> NormalizeCustomPipelines(IEnumerable<CustomPipelineDefinition> pipelines)
        {
            List<CustomPipelineDefinition> normalized = new List<CustomPipelineDefinition>();
            HashSet<string> usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CustomPipelineDefinition pipeline in pipelines ?? Enumerable.Empty<CustomPipelineDefinition>())
            {
                if (pipeline == null)
                    continue;

                string id = string.IsNullOrWhiteSpace(pipeline.Id)
                    ? Guid.NewGuid().ToString("N")
                    : pipeline.Id.Trim();
                if (!usedIds.Add(id))
                    id = Guid.NewGuid().ToString("N");

                string name = string.IsNullOrWhiteSpace(pipeline.Name)
                    ? "Unnamed pipeline"
                    : pipeline.Name.Trim();

                List<PipelineStepDefinition> steps = new List<PipelineStepDefinition>();
                foreach (PipelineStepDefinition step in pipeline.Steps ?? Enumerable.Empty<PipelineStepDefinition>())
                {
                    if (step == null)
                        continue;

                    PipelineStepKind kind = Enum.IsDefined(typeof(PipelineStepKind), step.Kind)
                        ? step.Kind
                        : PipelineStepKind.NormalizeDoi;
                    PipelineMetadataMode metadataMode = Enum.IsDefined(typeof(PipelineMetadataMode), step.MetadataMode)
                        ? step.MetadataMode
                        : PipelineMetadataMode.UseCurrentSettings;

                    steps.Add(new PipelineStepDefinition
                    {
                        Kind = kind,
                        MetadataMode = metadataMode
                    });
                }

                normalized.Add(new CustomPipelineDefinition
                {
                    Id = id,
                    Name = name,
                    Steps = steps
                });
            }

            return normalized;
        }
    }
}
