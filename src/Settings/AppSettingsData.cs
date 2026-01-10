using ScientificReviews.Settings.Editors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScientificReviews
{
    public class AppSettingsData
    {
        private const string GENERAL_CAT = "0: General";

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("JCR Api key")]
        [Description("Type your JCR Api key to get JCR functions")]
        public string JcrApiKey { get; set; }

        [Browsable(true)]
        [Category(GENERAL_CAT)]
        [DisplayName("Pdf Folder")]
        [Description("Set the path, where you have PDFs with [Title].pdf or [Key].pdf")]
        [Editor(typeof(PathEditor), typeof(UITypeEditor))]
        public string PdfFolder { get; set; }

        [Browsable(false)]
        public string[] Columns { get; set; }


        [Browsable(false)]
        public string[] SelectedTags { get; set; }

        [Browsable(false)]
        public string[] SelectedTypes { get; set; }

        [Browsable(false)]
        public string LastDirectory { get; set; }

        [Browsable(false)]
        public string LastFile { get; set; }

    }
}
