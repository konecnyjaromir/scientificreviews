using ScientificReviews.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
        }

        AppSettingsJson<AppSettingsData> set = new AppSettingsJson<AppSettingsData>(Program.SETTINGS_FILE_JSON);

        private void SettingsForm_Shown(object sender, EventArgs e)
        {
            set.LoadSettings();
            propertyGrid1.SelectedObject = set.Data;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            set.SaveSettings();
            DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
