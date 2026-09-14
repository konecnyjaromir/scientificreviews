using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ScientificReviews.Forms
{
    public class AboutForm : Form
    {
        private const string ProjectUrl = "https://github.com/konecnyjaromir/scientificreviews";

        public AboutForm()
        {
            InitializeAboutForm();
        }

        private void InitializeAboutForm()
        {
            Text = "About";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 320);

            TabControl tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            tabControl.TabPages.Add(CreateProgramTab());
            tabControl.TabPages.Add(CreateProjectTab());

            Controls.Add(tabControl);
        }

        private TabPage CreateProgramTab()
        {
            Assembly assembly = typeof(Program).Assembly;
            AssemblyName assemblyName = assembly.GetName();
            string description = GetAssemblyAttribute<AssemblyDescriptionAttribute>(assembly)?.Description
                ?? "ScientificReviews";
            string company = GetAssemblyAttribute<AssemblyCompanyAttribute>(assembly)?.Company
                ?? string.Empty;
            string copyright = GetAssemblyAttribute<AssemblyCopyrightAttribute>(assembly)?.Copyright
                ?? string.Empty;

            TabPage page = new TabPage("Program");

            TableLayoutPanel layout = CreateBaseLayout();
            layout.Controls.Add(CreateTitleLabel(Application.ProductName), 0, 0);
            layout.Controls.Add(CreateTextLabel(description), 0, 1);
            layout.Controls.Add(CreateTextLabel($"Version: {Application.ProductVersion}"), 0, 2);
            layout.Controls.Add(CreateTextLabel($"Assembly: {assemblyName.Version}"), 0, 3);

            if (!string.IsNullOrWhiteSpace(company))
                layout.Controls.Add(CreateTextLabel($"Company: {company}"), 0, 4);

            if (!string.IsNullOrWhiteSpace(copyright))
                layout.Controls.Add(CreateTextLabel(copyright), 0, 5);

            page.Controls.Add(layout);
            return page;
        }

        private TabPage CreateProjectTab()
        {
            TabPage page = new TabPage("Project");
            TableLayoutPanel layout = CreateBaseLayout();

            layout.Controls.Add(CreateTitleLabel("Project"), 0, 0);
            layout.Controls.Add(CreateTextLabel("Source code repository:"), 0, 1);

            LinkLabel link = new LinkLabel
            {
                AutoSize = true,
                Text = ProjectUrl,
                LinkBehavior = LinkBehavior.HoverUnderline,
                Margin = new Padding(0, 0, 0, 16)
            };
            link.Links.Add(0, ProjectUrl.Length, ProjectUrl);
            link.LinkClicked += ProjectLink_LinkClicked;
            layout.Controls.Add(link, 0, 2);

            Label acknowledgement = CreateTextLabel("Thank you to arXiv for use of its open access interoperability.");
            acknowledgement.MaximumSize = new Size(500, 0);
            layout.Controls.Add(acknowledgement, 0, 3);

            Label itextAcknowledgement = CreateTextLabel("This software uses the open-source iText library (AGPL licensed). Scientific Reviews is distributed as open-source freeware.");
            itextAcknowledgement.MaximumSize = new Size(500, 0);
            layout.Controls.Add(itextAcknowledgement, 0, 4);

            page.Controls.Add(layout);
            return page;
        }

        private static TableLayoutPanel CreateBaseLayout()
        {
            return new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                AutoScroll = true
            };
        }

        private static Label CreateTitleLabel(string text)
        {
            return new Label
            {
                AutoSize = true,
                Text = text,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12)
            };
        }

        private static Label CreateTextLabel(string text)
        {
            return new Label
            {
                AutoSize = true,
                Text = text,
                Margin = new Padding(0, 0, 0, 8)
            };
        }

        private static T GetAssemblyAttribute<T>(Assembly assembly) where T : Attribute
        {
            object[] attributes = assembly.GetCustomAttributes(typeof(T), false);
            return attributes.Length > 0 ? (T)attributes[0] : null;
        }

        private void ProjectLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ProjectUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Unable to open link", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
