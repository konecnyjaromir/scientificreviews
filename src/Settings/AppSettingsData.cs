using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public string JcrApiKey { get; set; }

        [Browsable(false)]
        public string[] SelectedTags { get; set; }

        [Browsable(false)]
        public string[] SelectedTypes { get; set; }

        [Browsable(false)]
        public string LastDirectory { get; set; }

        [Browsable(false)]
        public string LastFile { get; set; }

        public string[] Columns { get; set; }
    }
}
