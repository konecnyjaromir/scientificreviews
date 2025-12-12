namespace ScientificReviews.Forms
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.projectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadBibTexFolderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadBibTexFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clearToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripSeparator();
            this.exportDatabaseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportVisibleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportDOIsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportAsTableToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.databaseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createEntryKeysToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeDuplicitiesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeDuplicitiesByDOIToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeTagsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeTypesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeWithoutDOIToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.updatePageTagFormatToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.excludeEntriesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.excludeEntriesByTitleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.recordToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addTagToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addTagToSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeTagsToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.removeRecordToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.allowEditToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.journalCitationReportsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.updateJournalsDatabaseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createExtraJCRTagsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeQ3Q4ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.columnsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblSelected = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblInfo = new System.Windows.Forms.ToolStripStatusLabel();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.propertyGrid1 = new System.Windows.Forms.PropertyGrid();
            this.panel1 = new System.Windows.Forms.Panel();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.splitter2 = new System.Windows.Forms.Splitter();
            this.toolStrip2 = new System.Windows.Forms.ToolStrip();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.txtSearch = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripLabel2 = new System.Windows.Forms.ToolStripLabel();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.btnAddTag = new System.Windows.Forms.ToolStripButton();
            this.btnRemoveTag = new System.Windows.Forms.ToolStripButton();
            this.btnGoogle = new System.Windows.Forms.ToolStripButton();
            this.btnPdf = new System.Windows.Forms.ToolStripButton();
            this.bindingSource1 = new System.Windows.Forms.BindingSource(this.components);
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.btnRemoveTags = new System.Windows.Forms.ToolStripButton();
            this.removeTagToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.panel1.SuspendLayout();
            this.toolStrip2.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.projectToolStripMenuItem,
            this.databaseToolStripMenuItem,
            this.recordToolStripMenuItem,
            this.journalCitationReportsToolStripMenuItem,
            this.viewToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1164, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // projectToolStripMenuItem
            // 
            this.projectToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loadBibTexFolderToolStripMenuItem,
            this.loadBibTexFileToolStripMenuItem,
            this.clearToolStripMenuItem,
            this.toolStripMenuItem2,
            this.exportDatabaseToolStripMenuItem,
            this.exportVisibleToolStripMenuItem,
            this.exportSelectedToolStripMenuItem,
            this.exportDOIsToolStripMenuItem,
            this.exportAsTableToolStripMenuItem});
            this.projectToolStripMenuItem.Name = "projectToolStripMenuItem";
            this.projectToolStripMenuItem.Size = new System.Drawing.Size(56, 20);
            this.projectToolStripMenuItem.Text = "Project";
            // 
            // loadBibTexFolderToolStripMenuItem
            // 
            this.loadBibTexFolderToolStripMenuItem.Name = "loadBibTexFolderToolStripMenuItem";
            this.loadBibTexFolderToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.loadBibTexFolderToolStripMenuItem.Text = "Add or load BibTex folder";
            this.loadBibTexFolderToolStripMenuItem.Click += new System.EventHandler(this.loadBibTexFolderToolStripMenuItem_Click);
            // 
            // loadBibTexFileToolStripMenuItem
            // 
            this.loadBibTexFileToolStripMenuItem.Name = "loadBibTexFileToolStripMenuItem";
            this.loadBibTexFileToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.loadBibTexFileToolStripMenuItem.Text = "Add or load BibTex file";
            this.loadBibTexFileToolStripMenuItem.Click += new System.EventHandler(this.loadBibTexFileToolStripMenuItem_Click);
            // 
            // clearToolStripMenuItem
            // 
            this.clearToolStripMenuItem.Name = "clearToolStripMenuItem";
            this.clearToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.clearToolStripMenuItem.Text = "Clear";
            this.clearToolStripMenuItem.Click += new System.EventHandler(this.clearToolStripMenuItem_Click);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(204, 6);
            // 
            // exportDatabaseToolStripMenuItem
            // 
            this.exportDatabaseToolStripMenuItem.Name = "exportDatabaseToolStripMenuItem";
            this.exportDatabaseToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.exportDatabaseToolStripMenuItem.Text = "Export database";
            this.exportDatabaseToolStripMenuItem.Click += new System.EventHandler(this.exportDatabaseToolStripMenuItem_Click);
            // 
            // exportVisibleToolStripMenuItem
            // 
            this.exportVisibleToolStripMenuItem.Name = "exportVisibleToolStripMenuItem";
            this.exportVisibleToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.exportVisibleToolStripMenuItem.Text = "Export visible";
            this.exportVisibleToolStripMenuItem.Click += new System.EventHandler(this.exportVisibleToolStripMenuItem_Click);
            // 
            // exportDOIsToolStripMenuItem
            // 
            this.exportDOIsToolStripMenuItem.Name = "exportDOIsToolStripMenuItem";
            this.exportDOIsToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.exportDOIsToolStripMenuItem.Text = "Export DOIs";
            this.exportDOIsToolStripMenuItem.Click += new System.EventHandler(this.exportDOIsToolStripMenuItem_Click);
            // 
            // exportAsTableToolStripMenuItem
            // 
            this.exportAsTableToolStripMenuItem.Name = "exportAsTableToolStripMenuItem";
            this.exportAsTableToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.exportAsTableToolStripMenuItem.Text = "Export CSV from table";
            this.exportAsTableToolStripMenuItem.Click += new System.EventHandler(this.exportAsTableToolStripMenuItem_Click);
            // 
            // databaseToolStripMenuItem
            // 
            this.databaseToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.createEntryKeysToolStripMenuItem,
            this.removeDuplicitiesToolStripMenuItem,
            this.removeDuplicitiesByDOIToolStripMenuItem,
            this.removeTagsToolStripMenuItem,
            this.removeTypesToolStripMenuItem,
            this.removeWithoutDOIToolStripMenuItem,
            this.updatePageTagFormatToolStripMenuItem,
            this.excludeEntriesToolStripMenuItem,
            this.excludeEntriesByTitleToolStripMenuItem});
            this.databaseToolStripMenuItem.Name = "databaseToolStripMenuItem";
            this.databaseToolStripMenuItem.Size = new System.Drawing.Size(67, 20);
            this.databaseToolStripMenuItem.Text = "Database";
            this.databaseToolStripMenuItem.Click += new System.EventHandler(this.databaseToolStripMenuItem_Click);
            // 
            // createEntryKeysToolStripMenuItem
            // 
            this.createEntryKeysToolStripMenuItem.Name = "createEntryKeysToolStripMenuItem";
            this.createEntryKeysToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.createEntryKeysToolStripMenuItem.Text = "Create entry keys";
            this.createEntryKeysToolStripMenuItem.Click += new System.EventHandler(this.createEntryKeysToolStripMenuItem_Click);
            // 
            // removeDuplicitiesToolStripMenuItem
            // 
            this.removeDuplicitiesToolStripMenuItem.Name = "removeDuplicitiesToolStripMenuItem";
            this.removeDuplicitiesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.removeDuplicitiesToolStripMenuItem.Text = "Remove duplicities by title";
            this.removeDuplicitiesToolStripMenuItem.Click += new System.EventHandler(this.removeDuplicitiesToolStripMenuItem_Click);
            // 
            // removeDuplicitiesByDOIToolStripMenuItem
            // 
            this.removeDuplicitiesByDOIToolStripMenuItem.Name = "removeDuplicitiesByDOIToolStripMenuItem";
            this.removeDuplicitiesByDOIToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.removeDuplicitiesByDOIToolStripMenuItem.Text = "Remove duplicities by DOI";
            this.removeDuplicitiesByDOIToolStripMenuItem.Click += new System.EventHandler(this.removeDuplicitiesByDOIToolStripMenuItem_Click);
            // 
            // removeTagsToolStripMenuItem
            // 
            this.removeTagsToolStripMenuItem.Name = "removeTagsToolStripMenuItem";
            this.removeTagsToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.removeTagsToolStripMenuItem.Text = "Remove tags";
            this.removeTagsToolStripMenuItem.Click += new System.EventHandler(this.removeTagsToolStripMenuItem_Click);
            // 
            // removeTypesToolStripMenuItem
            // 
            this.removeTypesToolStripMenuItem.Name = "removeTypesToolStripMenuItem";
            this.removeTypesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.removeTypesToolStripMenuItem.Text = "Remove types";
            this.removeTypesToolStripMenuItem.Click += new System.EventHandler(this.removeTypesToolStripMenuItem_Click);
            // 
            // removeWithoutDOIToolStripMenuItem
            // 
            this.removeWithoutDOIToolStripMenuItem.Name = "removeWithoutDOIToolStripMenuItem";
            this.removeWithoutDOIToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.removeWithoutDOIToolStripMenuItem.Text = "Remove without DOI";
            this.removeWithoutDOIToolStripMenuItem.Click += new System.EventHandler(this.removeWithoutDOIToolStripMenuItem_Click);
            // 
            // updatePageTagFormatToolStripMenuItem
            // 
            this.updatePageTagFormatToolStripMenuItem.Name = "updatePageTagFormatToolStripMenuItem";
            this.updatePageTagFormatToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.updatePageTagFormatToolStripMenuItem.Text = "Update page tag format";
            this.updatePageTagFormatToolStripMenuItem.Click += new System.EventHandler(this.updatePageTagFormatToolStripMenuItem_Click);
            // 
            // excludeEntriesToolStripMenuItem
            // 
            this.excludeEntriesToolStripMenuItem.Name = "excludeEntriesToolStripMenuItem";
            this.excludeEntriesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.excludeEntriesToolStripMenuItem.Text = "Exclude entries";
            this.excludeEntriesToolStripMenuItem.Click += new System.EventHandler(this.excludeEntriesToolStripMenuItem_Click);
            // 
            // excludeEntriesByTitleToolStripMenuItem
            // 
            this.excludeEntriesByTitleToolStripMenuItem.Name = "excludeEntriesByTitleToolStripMenuItem";
            this.excludeEntriesByTitleToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.excludeEntriesByTitleToolStripMenuItem.Text = "Exclude entries by title";
            this.excludeEntriesByTitleToolStripMenuItem.Click += new System.EventHandler(this.excludeEntriesByTitleToolStripMenuItem_Click);
            // 
            // recordToolStripMenuItem
            // 
            this.recordToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allowEditToolStripMenuItem,
            this.toolStripMenuItem1,
            this.addTagToolStripMenuItem,
            this.addTagToSelectedToolStripMenuItem,
            this.removeTagToolStripMenuItem,
            this.removeTagsToolStripMenuItem1,
            this.removeRecordToolStripMenuItem,
            this.deleteSelectedToolStripMenuItem});
            this.recordToolStripMenuItem.Name = "recordToolStripMenuItem";
            this.recordToolStripMenuItem.Size = new System.Drawing.Size(56, 20);
            this.recordToolStripMenuItem.Text = "Record";
            this.recordToolStripMenuItem.Click += new System.EventHandler(this.recordToolStripMenuItem_Click);
            // 
            // addTagToolStripMenuItem
            // 
            this.addTagToolStripMenuItem.Name = "addTagToolStripMenuItem";
            this.addTagToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.addTagToolStripMenuItem.Text = "Add tag";
            this.addTagToolStripMenuItem.Click += new System.EventHandler(this.addTagToolStripMenuItem_Click);
            // 
            // addTagToSelectedToolStripMenuItem
            // 
            this.addTagToSelectedToolStripMenuItem.Name = "addTagToSelectedToolStripMenuItem";
            this.addTagToSelectedToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.addTagToSelectedToolStripMenuItem.Text = "Add tag to selected";
            this.addTagToSelectedToolStripMenuItem.Click += new System.EventHandler(this.addTagToSelectedToolStripMenuItem_Click);
            // 
            // removeTagsToolStripMenuItem1
            // 
            this.removeTagsToolStripMenuItem1.Name = "removeTagsToolStripMenuItem1";
            this.removeTagsToolStripMenuItem1.Size = new System.Drawing.Size(195, 22);
            this.removeTagsToolStripMenuItem1.Text = "Remove tags";
            this.removeTagsToolStripMenuItem1.Click += new System.EventHandler(this.removeTagsToolStripMenuItem1_Click);
            // 
            // removeRecordToolStripMenuItem
            // 
            this.removeRecordToolStripMenuItem.Name = "removeRecordToolStripMenuItem";
            this.removeRecordToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.removeRecordToolStripMenuItem.Text = "Remove current record";
            this.removeRecordToolStripMenuItem.Click += new System.EventHandler(this.removeRecordToolStripMenuItem_Click);
            // 
            // deleteSelectedToolStripMenuItem
            // 
            this.deleteSelectedToolStripMenuItem.Name = "deleteSelectedToolStripMenuItem";
            this.deleteSelectedToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.deleteSelectedToolStripMenuItem.Text = "Remove selected";
            this.deleteSelectedToolStripMenuItem.Click += new System.EventHandler(this.deleteSelectedToolStripMenuItem_Click);
            // 
            // allowEditToolStripMenuItem
            // 
            this.allowEditToolStripMenuItem.Name = "allowEditToolStripMenuItem";
            this.allowEditToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.allowEditToolStripMenuItem.Text = "Allow edit";
            this.allowEditToolStripMenuItem.Click += new System.EventHandler(this.allowEditToolStripMenuItem_Click);
            // 
            // journalCitationReportsToolStripMenuItem
            // 
            this.journalCitationReportsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.updateJournalsDatabaseToolStripMenuItem,
            this.createExtraJCRTagsToolStripMenuItem,
            this.removeQ3Q4ToolStripMenuItem});
            this.journalCitationReportsToolStripMenuItem.Name = "journalCitationReportsToolStripMenuItem";
            this.journalCitationReportsToolStripMenuItem.Size = new System.Drawing.Size(145, 20);
            this.journalCitationReportsToolStripMenuItem.Text = "Journal Citation Reports";
            // 
            // updateJournalsDatabaseToolStripMenuItem
            // 
            this.updateJournalsDatabaseToolStripMenuItem.Name = "updateJournalsDatabaseToolStripMenuItem";
            this.updateJournalsDatabaseToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.updateJournalsDatabaseToolStripMenuItem.Text = "Update Journals Database";
            this.updateJournalsDatabaseToolStripMenuItem.Click += new System.EventHandler(this.updateJournalsDatabaseToolStripMenuItem_Click);
            // 
            // createExtraJCRTagsToolStripMenuItem
            // 
            this.createExtraJCRTagsToolStripMenuItem.Name = "createExtraJCRTagsToolStripMenuItem";
            this.createExtraJCRTagsToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.createExtraJCRTagsToolStripMenuItem.Text = "Create extra JCR tags";
            this.createExtraJCRTagsToolStripMenuItem.Click += new System.EventHandler(this.createExtraJCRTagsToolStripMenuItem_Click);
            // 
            // removeQ3Q4ToolStripMenuItem
            // 
            this.removeQ3Q4ToolStripMenuItem.Name = "removeQ3Q4ToolStripMenuItem";
            this.removeQ3Q4ToolStripMenuItem.Size = new System.Drawing.Size(209, 22);
            this.removeQ3Q4ToolStripMenuItem.Text = "Remove Q3 Q4";
            this.removeQ3Q4ToolStripMenuItem.Click += new System.EventHandler(this.removeQ3Q4ToolStripMenuItem_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.columnsToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(63, 20);
            this.viewToolStripMenuItem.Text = "Window";
            // 
            // columnsToolStripMenuItem
            // 
            this.columnsToolStripMenuItem.Name = "columnsToolStripMenuItem";
            this.columnsToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
            this.columnsToolStripMenuItem.Text = "Columns";
            this.columnsToolStripMenuItem.Click += new System.EventHandler(this.columnsToolStripMenuItem_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus,
            this.toolStripStatusLabel1,
            this.lblSelected,
            this.lblInfo});
            this.statusStrip1.Location = new System.Drawing.Point(0, 909);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1164, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(22, 17);
            this.lblStatus.Text = "---";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(1095, 17);
            this.toolStripStatusLabel1.Spring = true;
            // 
            // lblSelected
            // 
            this.lblSelected.Name = "lblSelected";
            this.lblSelected.Size = new System.Drawing.Size(20, 17);
            this.lblSelected.Text = "(-)";
            // 
            // lblInfo
            // 
            this.lblInfo.Name = "lblInfo";
            this.lblInfo.Size = new System.Drawing.Size(12, 17);
            this.lblInfo.Text = "-";
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            this.dataGridView1.AllowUserToResizeRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Left;
            this.dataGridView1.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dataGridView1.Location = new System.Drawing.Point(0, 49);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView1.Size = new System.Drawing.Size(797, 860);
            this.dataGridView1.TabIndex = 2;
            this.dataGridView1.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellContentClick);
            this.dataGridView1.SelectionChanged += new System.EventHandler(this.dataGridView1_SelectionChanged);
            this.dataGridView1.KeyDown += new System.Windows.Forms.KeyEventHandler(this.dataGridView1_KeyDown);
            // 
            // splitter1
            // 
            this.splitter1.Location = new System.Drawing.Point(797, 49);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(10, 860);
            this.splitter1.TabIndex = 5;
            this.splitter1.TabStop = false;
            this.splitter1.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.splitter1_SplitterMoved);
            // 
            // propertyGrid1
            // 
            this.propertyGrid1.Dock = System.Windows.Forms.DockStyle.Top;
            this.propertyGrid1.HelpVisible = false;
            this.propertyGrid1.Location = new System.Drawing.Point(0, 35);
            this.propertyGrid1.Name = "propertyGrid1";
            this.propertyGrid1.Size = new System.Drawing.Size(357, 501);
            this.propertyGrid1.TabIndex = 6;
            this.propertyGrid1.ToolbarVisible = false;
            this.propertyGrid1.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGrid1_PropertyValueChanged);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.propertyGrid1);
            this.panel1.Controls.Add(this.richTextBox1);
            this.panel1.Controls.Add(this.splitter2);
            this.panel1.Controls.Add(this.toolStrip2);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(807, 49);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(357, 860);
            this.panel1.TabIndex = 7;
            // 
            // richTextBox1
            // 
            this.richTextBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.richTextBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBox1.Location = new System.Drawing.Point(0, 35);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(357, 825);
            this.richTextBox1.TabIndex = 7;
            this.richTextBox1.Text = "";
            // 
            // splitter2
            // 
            this.splitter2.Dock = System.Windows.Forms.DockStyle.Top;
            this.splitter2.Location = new System.Drawing.Point(0, 25);
            this.splitter2.Name = "splitter2";
            this.splitter2.Size = new System.Drawing.Size(357, 10);
            this.splitter2.TabIndex = 6;
            this.splitter2.TabStop = false;
            // 
            // toolStrip2
            // 
            this.toolStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnAddTag,
            this.btnRemoveTag,
            this.btnRemoveTags});
            this.toolStrip2.Location = new System.Drawing.Point(0, 0);
            this.toolStrip2.Name = "toolStrip2";
            this.toolStrip2.Size = new System.Drawing.Size(357, 25);
            this.toolStrip2.TabIndex = 8;
            this.toolStrip2.Text = "toolStrip2";
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel1,
            this.txtSearch,
            this.toolStripLabel2,
            this.toolStripSeparator1,
            this.btnGoogle,
            this.btnPdf});
            this.toolStrip1.Location = new System.Drawing.Point(0, 24);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1164, 25);
            this.toolStrip1.TabIndex = 8;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Name = "toolStripLabel1";
            this.toolStripLabel1.Size = new System.Drawing.Size(42, 22);
            this.toolStripLabel1.Text = "Search";
            // 
            // txtSearch
            // 
            this.txtSearch.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(300, 25);
            this.txtSearch.Click += new System.EventHandler(this.txtSearch_Click);
            this.txtSearch.TextChanged += new System.EventHandler(this.txtSearch_TextChanged);
            // 
            // toolStripLabel2
            // 
            this.toolStripLabel2.Name = "toolStripLabel2";
            this.toolStripLabel2.Size = new System.Drawing.Size(120, 22);
            this.toolStripLabel2.Text = "Separated by \',\' as OR";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // btnAddTag
            // 
            this.btnAddTag.Image = global::ScientificReviews.Properties.Resources.add;
            this.btnAddTag.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnAddTag.Name = "btnAddTag";
            this.btnAddTag.Size = new System.Drawing.Size(69, 22);
            this.btnAddTag.Text = "Add tag";
            this.btnAddTag.Click += new System.EventHandler(this.addTag_Click);
            // 
            // btnRemoveTag
            // 
            this.btnRemoveTag.Image = global::ScientificReviews.Properties.Resources.remove;
            this.btnRemoveTag.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRemoveTag.Name = "btnRemoveTag";
            this.btnRemoveTag.Size = new System.Drawing.Size(90, 22);
            this.btnRemoveTag.Text = "Remove tag";
            this.btnRemoveTag.Click += new System.EventHandler(this.btnDeleteTag_Click);
            // 
            // btnGoogle
            // 
            this.btnGoogle.Image = global::ScientificReviews.Properties.Resources.google;
            this.btnGoogle.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnGoogle.Name = "btnGoogle";
            this.btnGoogle.Size = new System.Drawing.Size(65, 22);
            this.btnGoogle.Text = "Google";
            this.btnGoogle.Click += new System.EventHandler(this.btnGoogle_Click);
            // 
            // btnPdf
            // 
            this.btnPdf.Image = global::ScientificReviews.Properties.Resources.pdf2;
            this.btnPdf.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnPdf.Name = "btnPdf";
            this.btnPdf.Size = new System.Drawing.Size(80, 22);
            this.btnPdf.Text = "Open PDF";
            this.btnPdf.Click += new System.EventHandler(this.btnPdf_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(192, 6);
            // 
            // btnRemoveTags
            // 
            this.btnRemoveTags.Image = global::ScientificReviews.Properties.Resources.remove;
            this.btnRemoveTags.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRemoveTags.Name = "btnRemoveTags";
            this.btnRemoveTags.Size = new System.Drawing.Size(95, 22);
            this.btnRemoveTags.Text = "Remove tags";
            this.btnRemoveTags.Click += new System.EventHandler(this.btnRemoveTags_Click);
            // 
            // removeTagToolStripMenuItem
            // 
            this.removeTagToolStripMenuItem.Name = "removeTagToolStripMenuItem";
            this.removeTagToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.removeTagToolStripMenuItem.Text = "Remove tag";
            this.removeTagToolStripMenuItem.Click += new System.EventHandler(this.removeTagToolStripMenuItem_Click);
            // 
            // exportSelectedToolStripMenuItem
            // 
            this.exportSelectedToolStripMenuItem.Name = "exportSelectedToolStripMenuItem";
            this.exportSelectedToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.exportSelectedToolStripMenuItem.Text = "Export selected";
            this.exportSelectedToolStripMenuItem.Click += new System.EventHandler(this.exportSelectedToolStripMenuItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1164, 931);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.splitter1);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainForm";
            this.Text = "Scientific Reviews";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.toolStrip2.ResumeLayout(false);
            this.toolStrip2.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem projectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadBibTexFolderToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.ToolStripMenuItem databaseToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createEntryKeysToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeTagsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeTypesToolStripMenuItem;
        private System.Windows.Forms.Splitter splitter1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripStatusLabel lblInfo;
        private System.Windows.Forms.ToolStripMenuItem removeDuplicitiesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeWithoutDOIToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeDuplicitiesByDOIToolStripMenuItem;
        private System.Windows.Forms.PropertyGrid propertyGrid1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Splitter splitter2;
        private System.Windows.Forms.ToolStripMenuItem updatePageTagFormatToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem journalCitationReportsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem updateJournalsDatabaseToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createExtraJCRTagsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeQ3Q4ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadBibTexFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem excludeEntriesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem excludeEntriesByTitleToolStripMenuItem;
        private System.Windows.Forms.BindingSource bindingSource1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripTextBox txtSearch;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem columnsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem recordToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addTagToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeRecordToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeTagsToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem allowEditToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addTagToSelectedToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel lblSelected;
        private System.Windows.Forms.ToolStripButton btnGoogle;
        private System.Windows.Forms.ToolStripButton btnPdf;
        private System.Windows.Forms.ToolStripLabel toolStripLabel2;
        private System.Windows.Forms.ToolStripMenuItem clearToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem2;
        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.ToolStripMenuItem exportDatabaseToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportVisibleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportDOIsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAsTableToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteSelectedToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStrip2;
        private System.Windows.Forms.ToolStripButton btnAddTag;
        private System.Windows.Forms.ToolStripButton btnRemoveTag;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripButton btnRemoveTags;
        private System.Windows.Forms.ToolStripMenuItem removeTagToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedToolStripMenuItem;
    }
}