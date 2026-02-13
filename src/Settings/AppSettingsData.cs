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
        private const string BACKUP_CAT = "1: Backup";

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


        [Browsable(true)]
        [Category(BACKUP_CAT)]
        [DisplayName("Allow backup")]
        [Description("If you want to create backups")]
        [Editor(typeof(PathEditor), typeof(UITypeEditor))]
        public bool AllowBackup { get; set; } = true;
        
        
        [Browsable(true)]
        [Category(BACKUP_CAT)]
        [DisplayName("Number of backups")]
        [Description("Number of backups to keep")]
        public int NumberOfBackups { get; set; } = 10;

        [Browsable(true)]
        [Category(BACKUP_CAT)]
        [DisplayName("Backup folder")]
        [Description("Set the path, where you will have backup bibtexes")]
        [Editor(typeof(PathEditor), typeof(UITypeEditor))]
        public string BackupFolder { get; set; }



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
