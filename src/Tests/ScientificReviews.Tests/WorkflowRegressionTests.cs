using ScientificReviews.Bibtex;
using ScientificReviews.Forms;
using ScientificReviews.Helpers;
using ScientificReviews.Logs;
using ScientificReviews.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews.Tests
{
    internal static class WorkflowRegressionTests
    {
        public static void ArchiveHistory_RestoresEditsDeletionAndSavedState()
        {
            List<BibtexEntry> records = new List<BibtexEntry> { Entry("first", "Original"), Entry("second", "Other") };
            ArchiveHistory history = new ArchiveHistory();
            history.Reset(records);
            records[0].Tags[0].Value = "Edited";
            history.Record(records);
            records.RemoveAt(1);
            history.Record(records);
            records = history.Undo();
            Equal(2, records.Count);
            Equal("Edited", records[0].GetTagValue("title"));
            records = history.Undo();
            Equal("Original", records[0].GetTagValue("title"));
            Equal(true, history.IsSaved);
            records[0].Tags[0].Value = "Must not corrupt stored state";
            records = history.Redo();
            Equal("Edited", records[0].GetTagValue("title"));
            history.MarkSaved();
            Equal(true, history.IsSaved);
            history.Undo();
            Equal(false, history.IsSaved);
            records = history.Redo();
            history.Record(records); // Refreshing unchanged state must preserve redo.
            Equal(1, history.Redo().Count);
            history.Undo();
            records[0].Tags[0].Value = "New branch";
            history.Record(records);
            Equal(false, history.CanRedo);
            history.Reset(new[] { Entry("another-file", "New archive") });
            Equal(false, history.CanUndo);
        }

        public static void Backups_KeepLatestAndRequestedNumberOfSnapshots()
        {
            using (TestDirectory directory = new TestDirectory())
            {
                string backupFolder = Path.Combine(directory.Path, "backup");
                for (int i = 0; i < 8; i++)
                    BibtexAutosaveManager.SaveSnapshot(backupFolder, 3, "snapshot " + i);
                Equal(4, Directory.GetFiles(backupFolder, "*.bib").Length);
                Equal("snapshot 7", File.ReadAllText(BibtexAutosaveManager.GetLatestBackupPath(backupFolder)));

                string latest = Path.Combine(backupFolder, "autosave_latest.bib");
                File.SetLastWriteTimeUtc(latest, DateTime.UtcNow.AddDays(-1));
                BibtexAutosaveManager.SaveSnapshot(backupFolder, 1, "last snapshot");
                Equal(2, Directory.GetFiles(backupFolder, "*.bib").Length);
                Equal("last snapshot", File.ReadAllText(latest));
                Equal(0, Directory.GetFiles(backupFolder, "*.tmp").Length);
            }
        }

        public static void CtrlBackspace_DeletesWordSelectionAndSupportsUndo()
        {
            using (TextBox editor = new TextBox())
            {
                IntPtr handle = editor.Handle;
                editor.Text = "žlutý kůň  ";
                editor.SelectionStart = editor.TextLength;
                TextEditingShortcuts.DeletePreviousWord(editor);
                Equal("žlutý ", editor.Text);
                Equal(true, editor.CanUndo);
                editor.Undo();
                Equal("žlutý kůň  ", editor.Text);

                editor.Text = "one two three";
                editor.Select(4, 3);
                TextEditingShortcuts.DeletePreviousWord(editor);
                Equal("one  three", editor.Text);
                editor.Undo();
                Equal("one two three", editor.Text);
                editor.Select(0, 0);
                TextEditingShortcuts.DeletePreviousWord(editor);
                Equal("one two three", editor.Text);
                editor.ReadOnly = true;
                editor.SelectAll();
                TextEditingShortcuts.DeletePreviousWord(editor);
                Equal("one two three", editor.Text);
            }
        }

        public static void BackupDefaults_NormalizeOlderSettings()
        {
            AppSettingsData settings = new AppSettingsData { BackupFolder = null };
            ScientificReviews.Program.PrepareSettingsData(settings);
            Equal(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup"), settings.BackupFolder);
            settings.BackupFolder = "custom-folder";
            ScientificReviews.Program.PrepareSettingsData(settings);
            Equal("custom-folder", settings.BackupFolder);
        }

        public static void MainForm_TagEditsUndoRedoAndUrlPreview()
        {
            using (TestDirectory directory = new TestDirectory())
            using (MainForm form = CreateForm(directory.Path))
            {
                List<BibtexEntry> records = new List<BibtexEntry> { Entry("CaseSensitiveKey", "Original") };
                records[0].Tags = records[0].Tags.Concat(new[] { new BibtexTag("url", "https://example.org/CaseSensitivePath?Token=ABC") }).ToArray();
                Field(form, "entries").SetValue(form, records);
                Invoke(form, "LoadData", records.ToArray(), "");
                Invoke(form, "Changed", false);

                DataGridView grid = (DataGridView)Field(form, "dataGridView1").GetValue(form);
                grid.Rows[0].Selected = true;
                ((ToolStripTextBox)Field(form, "txtKey").GetValue(form)).Text = "title";
                ((ToolStripTextBox)Field(form, "txtValue").GetValue(form)).Text = "Edited";
                Invoke(form, "AddOrEditTagToSelected");
                Equal(true, Dirty(form));
                Equal("Edited", Records(form)[0].GetTagValue("title"));
                Equal(true, form.TryHandleHistoryShortcut(Keys.Control | Keys.Z, null));
                Equal("Original", Records(form)[0].GetTagValue("title"));
                Equal(false, Dirty(form));
                form.TryHandleHistoryShortcut(Keys.Control | Keys.Y, null);
                Equal("Edited", Records(form)[0].GetTagValue("title"));
                Equal(true, Dirty(form));

                Invoke(form, "ShowEntry", Records(form)[0], "casesensitivepath");
                RichTextBox preview = (RichTextBox)Field(form, "richTextBox1").GetValue(form);
                Equal(true, preview.ReadOnly);
                Equal(true, preview.DetectUrls);
                Equal(true, preview.Text.Contains("https://example.org/CaseSensitivePath?Token=ABC"));
                Equal(true, preview.Text.Contains("CaseSensitiveKey"));

                grid.Rows[0].Selected = true;
                Invoke(form, "RemoveSelectedRecords");
                Equal(0, Records(form).Count);
                Equal(string.Empty, preview.Text);
                form.TryHandleHistoryShortcut(Keys.Control | Keys.Z, null);
                Equal(1, Records(form).Count);
                Equal("Edited", Records(form)[0].GetTagValue("title"));
            }
        }

        public static void MainForm_StartupFileOverridesRecoveryAndSaveApprovalResets()
        {
            using (TestDirectory directory = new TestDirectory())
            {
                string source = Path.Combine(directory.Path, "file with spaces.BIB");
                string saved = Path.Combine(directory.Path, "saved.bib");
                File.WriteAllText(source, "@article{StartupKey, title={Opened directly}}\n");
                string backups = Path.Combine(directory.Path, "backup");
                BibtexAutosaveManager.SaveSnapshot(backups, 2, "@article{WrongRecovery, title={Wrong file}}\n");
                using (MainForm form = CreateForm(directory.Path, new[] { source }))
                {
                    ScientificReviews.Program.AppSettings.Data.AllowBackup = true;
                    Invoke(form, "MainForm_Shown", form, EventArgs.Empty);
                    PumpUntil(() => Records(form).Count == 1);
                    Equal("startupkey", Records(form)[0].Key);
                    Equal(Path.GetFullPath(source), Field(form, "_currentBibTexPath").GetValue(form));
                    Equal(false, Dirty(form));

                    Task<bool> save = (Task<bool>)Invoke(form, "SaveBibtexToFileAsync", Records(form).ToArray(), saved, "Save test", "Saved", true);
                    Wait(save);
                    Equal(true, save.Result);
                    Equal(Path.GetFullPath(saved), Field(form, "_approvedSavePath").GetValue(form));
                    Records(form)[0].Tags[0].Value = "Subsequent save";
                    Invoke(form, "Changed", true);
                    Task<bool> saveAgain = (Task<bool>)Invoke(form, "SaveCurrentArchiveAsync", false);
                    Wait(saveAgain); // A regression would display an overwrite dialog and time out.
                    Equal(true, saveAgain.Result);
                    Equal(true, File.ReadAllText(saved).Contains("Subsequent save"));

                    Invoke(form, "SetCurrentBibTex", source);
                    Equal(null, Field(form, "_approvedSavePath").GetValue(form));
                    ScientificReviews.Program.AppSettings.Data.AllowBackup = false;
                    ((CancellationTokenSource)Field(form, "_changedCts").GetValue(form))?.Cancel();
                    Application.DoEvents();
                }
            }
        }

        public static void MainForm_RecoveryRetainsRecordsAndQuickCloseFlushesBackup()
        {
            using (TestDirectory directory = new TestDirectory())
            using (MainForm form = CreateForm(directory.Path))
            {
                AppSettingsData settings = ScientificReviews.Program.AppSettings.Data;
                settings.AllowBackup = true;
                settings.UnsafeClosing = true;
                BibtexAutosaveManager.SaveSnapshot(settings.BackupFolder, 2, "@article{RecoveredKey, title={Recovered}}\n");
                Invoke(form, "MainForm_Shown", form, EventArgs.Empty);
                Equal("recoveredkey", Records(form).Single().Key);
                Equal(true, Dirty(form));
                Records(form)[0].Tags[0].Value = "Changed just before closing";
                Invoke(form, "Changed", true);
                FormClosingEventArgs closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
                Invoke(form, "MainForm_FormClosing", form, closing);
                Equal(true, closing.Cancel);
                PumpUntil(() => (bool)Field(form, "_closeAfterBackup").GetValue(form));
                Equal(true, File.ReadAllText(BibtexAutosaveManager.GetLatestBackupPath(settings.BackupFolder)).Contains("Changed just before closing"));
            }
        }

        public static void MainForm_SearchKeepsLatestSmartAndClassicQuery()
        {
            using (TestDirectory directory = new TestDirectory())
            using (MainForm form = CreateForm(directory.Path))
            {
                List<BibtexEntry> records = new List<BibtexEntry>
                {
                    Entry("alpha", "Alpha result"),
                    Entry("beta", "Beta result"),
                    Entry("gamma", "Gamma result")
                };
                Field(form, "entries").SetValue(form, records);
                Invoke(form, "LoadData", records.ToArray(), "");

                ToolStripTextBox search = (ToolStripTextBox)Field(form, "txtSearch").GetValue(form);
                DataGridView grid = (DataGridView)Field(form, "dataGridView1").GetValue(form);
                search.Text = "title:alpha";
                search.Text = "title:beta";
                PumpUntil(() => grid.Rows.Count == 1 &&
                    ((BibtexEntry)((System.Data.DataRowView)grid.Rows[0].DataBoundItem).Row["Entry"]).Key == "beta");

                ScientificReviews.Program.AppSettings.Data.UseSmartSearch = false;
                search.Text = "alpha";
                search.Text = "gamma";
                PumpUntil(() => grid.Rows.Count == 1 &&
                    ((BibtexEntry)((System.Data.DataRowView)grid.Rows[0].DataBoundItem).Row["Entry"]).Key == "gamma");
            }
        }

        private static MainForm CreateForm(string directory, string[] startupPaths = null)
        {
            AppSettingsJson<AppSettingsData> settings = new AppSettingsJson<AppSettingsData>(Path.Combine(directory, "settings.json"));
            settings.Data.AllowBackup = false;
            settings.Data.FirstTimeUseCompleted = true;
            settings.Data.AutoPreprocessingMode = AutoPreprocessingMode.Off;
            settings.Data.BackupFolder = Path.Combine(directory, "backup");
            typeof(ScientificReviews.Program).GetProperty("AppSettings", BindingFlags.Static | BindingFlags.Public)
                .GetSetMethod(true).Invoke(null, new object[] { settings });
            AppLog.GlobalAppPath = directory;
            MainForm form = new MainForm(startupPaths);
            IntPtr handle = form.Handle;
            form.BindingContext = new BindingContext();
            return form;
        }

        private static BibtexEntry Entry(string key, string title) => new BibtexEntry
        {
            Key = key, Type = "article", Tags = new[] { new BibtexTag("title", title) }
        };

        private static FieldInfo Field(MainForm form, string name) => typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static List<BibtexEntry> Records(MainForm form) => (List<BibtexEntry>)Field(form, "entries").GetValue(form);
        private static bool Dirty(MainForm form) => (bool)typeof(MainForm).GetProperty("DatabaseChanged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
        private static object Invoke(MainForm form, string name, params object[] args) =>
            typeof(MainForm).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, args);

        private static void Wait(Task task)
        {
            PumpUntil(() => task.IsCompleted);
            task.GetAwaiter().GetResult();
        }

        private static void PumpUntil(Func<bool> complete)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!complete())
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("The UI operation did not finish.");
                Application.DoEvents();
                Thread.Sleep(5);
            }
            Application.DoEvents();
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }

        private sealed class TestDirectory : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ScientificReviewsTests-" + Guid.NewGuid().ToString("N"));
            public TestDirectory() => Directory.CreateDirectory(Path);
            public void Dispose()
            {
                // Only files in this newly created, uniquely named test directory are removed.
                foreach (string file in Directory.GetFiles(Path, "*", SearchOption.AllDirectories))
                    File.Delete(file);
                foreach (string subdirectory in Directory.GetDirectories(Path, "*", SearchOption.AllDirectories).OrderByDescending(value => value.Length))
                    Directory.Delete(subdirectory);
                Directory.Delete(Path);
            }
        }
    }
}
