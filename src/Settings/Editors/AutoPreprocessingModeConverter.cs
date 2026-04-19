using System.ComponentModel;

namespace ScientificReviews.Settings.Editors
{
    public class AutoPreprocessingModeConverter : EnumConverter
    {
        private static readonly AutoPreprocessingMode[] OrderedModes = new[]
        {
            AutoPreprocessingMode.Off,
            AutoPreprocessingMode.Fast,
            AutoPreprocessingMode.Normal,
            AutoPreprocessingMode.Deep
        };

        public AutoPreprocessingModeConverter() : base(typeof(AutoPreprocessingMode))
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
    }
}
