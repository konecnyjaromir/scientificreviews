using ScientificReviews.Forms;
using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;

namespace ScientificReviews.Settings.Editors
{
    public class StringArrayEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            using (EditColumnsForm form = new EditColumnsForm())
            {
                form.Text = context?.PropertyDescriptor?.DisplayName ?? "Edit items";
                form.SetColumns(value as string[] ?? Array.Empty<string>());

                return form.ShowDialog() == DialogResult.OK
                    ? form.GetColumns()
                    : value;
            }
        }
    }
}
