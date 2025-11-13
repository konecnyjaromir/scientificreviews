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
    public partial class InputBoxForm : Form
    {
        public InputBoxForm()
        {
            InitializeComponent();
        }
        
        public static InputBoxForm Show(string text, Form parent)
        {
            var frm = new InputBoxForm();
            frm.Text = text;
            frm.ShowDialog(parent);
            return frm;
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

        public string GetText()
        {
            return textBox1.Text;
        }

        public void SetText(string value)
        {
            textBox1.Text = value;
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {

                if (textBox1.Text != string.Empty)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }

            }
        }

        private void InputBoxForm_Shown(object sender, EventArgs e)
        {
            textBox1.Select();
        }
    }
}
