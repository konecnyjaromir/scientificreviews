using System;
using System.ComponentModel;
using System.Globalization;

namespace ScientificReviews.Settings.Editors
{
    public class LowQuantileDeletingModeConverter : EnumConverter
    {
        public LowQuantileDeletingModeConverter() : base(typeof(LowQuantileDeletingMode))
        {
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is LowQuantileDeletingMode mode)
            {
                switch (mode)
                {
                    case LowQuantileDeletingMode.AllRecords:
                        return "All records";
                    case LowQuantileDeletingMode.OnlyRecordsWithValidJifTags:
                    default:
                        return "Only Records With Valid Jif Tags";
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
                    case "All records":
                    case "All":
                        return LowQuantileDeletingMode.AllRecords;
                    case "Only Records With Valid Jif Tags":
                    case "Only valid Jif tags":
                    case "Only valid JCR tags":
                        return LowQuantileDeletingMode.OnlyRecordsWithValidJifTags;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
