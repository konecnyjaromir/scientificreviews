using System;
using System.ComponentModel;
using System.Globalization;

namespace ScientificReviews.Settings.Editors
{
    public class OpenAddModeConverter : EnumConverter
    {
        public OpenAddModeConverter() : base(typeof(OpenAddMode))
        {
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is OpenAddMode mode)
            {
                switch (mode)
                {
                    case OpenAddMode.Raw:
                        return "Raw (origin data)";
                    case OpenAddMode.Normal:
                    default:
                        return "Normal (classic)";
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
                    case "Raw (origin data)":
                    case "Origin data":
                    case "Raw (clear data)":
                        return OpenAddMode.Raw;
                    case "Normal (classic)":
                    case "Normal":
                        return OpenAddMode.Normal;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
