using System;
using System.ComponentModel;
using System.Globalization;

namespace ScientificReviews.Settings.Editors
{
    public class PerformanceOptimizationModeConverter : EnumConverter
    {
        private static readonly PerformanceOptimizationMode[] OrderedModes = new[]
        {
            PerformanceOptimizationMode.OptimizeForPerformance,
            PerformanceOptimizationMode.OptimizeForQualityPerformanceRatio,
            PerformanceOptimizationMode.OptimizeForQuality,
            PerformanceOptimizationMode.NoOptimization
        };

        public PerformanceOptimizationModeConverter() : base(typeof(PerformanceOptimizationMode))
        {
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(OrderedModes);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is PerformanceOptimizationMode mode)
            {
                switch (mode)
                {
                    case PerformanceOptimizationMode.OptimizeForPerformance:
                        return "Optimize For Performance";
                    case PerformanceOptimizationMode.OptimizeForQuality:
                        return "Optimize For Quality (!)";
                    case PerformanceOptimizationMode.NoOptimization:
                        return "No optimization (not recommended)";
                    case PerformanceOptimizationMode.OptimizeForQualityPerformanceRatio:
                    default:
                        return "Optimize For Quality / Performance ratio";
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string stringValue = value as string;
            if (stringValue != null)
            {
                switch (stringValue.Trim())
                {
                    case "Optimize For Performance":
                        return PerformanceOptimizationMode.OptimizeForPerformance;
                    case "Optimize For Quality / Performance ratio":
                    case "Balanced":
                    case "Default":
                        return PerformanceOptimizationMode.OptimizeForQualityPerformanceRatio;
                    case "Optimize For Quality (!)":
                    case "Optimize For Quality":
                        return PerformanceOptimizationMode.OptimizeForQuality;
                    case "No optimization (not recommended)":
                    case "No optimalization (not reccomended)":
                    case "No optimization":
                        return PerformanceOptimizationMode.NoOptimization;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
