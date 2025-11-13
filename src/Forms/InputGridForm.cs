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
    public partial class InputGridForm : Form
    {
        public InputGridForm()
        {
            InitializeComponent();
        }

        public object Object
        {
            set
            {
                propertyGrid1.SelectedObject = value;   
            }
            get
            {
                return propertyGrid1.SelectedObject;
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
