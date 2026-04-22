using ScientificReviews.Bibtex;
using ScientificReviews.Logs;
using ScientificReviews.Settings;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScientificReviews
{
    internal static class Program
    {
        public const string SETTINGS_FILE_JSON = "settings.json";
        public const string JOURNALS_JSON = "journals.json";
        public const string APP_NAME = "Scientific reviews";
        public static string GlobalPath { get; private set; }
        public static string SettingsFilePath => string.IsNullOrWhiteSpace(GlobalPath)
            ? SETTINGS_FILE_JSON
            : Path.Combine(GlobalPath, SETTINGS_FILE_JSON);
        public static AppSettingsJson<AppSettingsData> AppSettings { get; private set; }
        public static JsonDatabase<JournalsDatabase> JournalsDatabase { get; private set; }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {



                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                               | SecurityProtocolType.Tls11
                               | SecurityProtocolType.Tls12
                               | SecurityProtocolType.Ssl3;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

                GlobalPath = Path.GetDirectoryName(Application.ExecutablePath);
                Thread.CurrentThread.Name = "Main Thread";
                LogsInitialize();
                LoadSettings();

                JournalsDatabase = new JsonDatabase<JournalsDatabase>(JOURNALS_JSON);
                JournalsDatabase.Load();
            }
#if !DEBUG
            catch (Exception ex)
            {
                ResolveException(ex);
            }
#endif
            finally { }

            Application.Run(new Forms.MainForm());
        }

        private static void LogsInitialize()
        {
            AppLog.GlobalAppPath = GlobalPath;
            AppLog.Extension = "log";
            AppLog.ErrPath = "logs";
            AppLog.AppName = "scientificreviews";

            ErrLog.GlobalAppPath = GlobalPath;
            ErrLog.Extension = AppLog.Extension;
            ErrLog.ErrPath = AppLog.ErrPath;
            ErrLog.AppName = AppLog.AppName;
        }

        private static void LoadSettings()
        {
            try
            {
                AppSettings = new AppSettingsJson<AppSettingsData>(SettingsFilePath);
                AppSettings.LoadSettings();
                bool settingsChanged = PrepareSettingsData(AppSettings.Data);

                if (settingsChanged)
                    AppSettings.SaveSettings("Settings migration/default normalization");
            }
            catch(Exception)
            {              
            }
        }

        internal static bool PrepareSettingsData(AppSettingsData settings)
        {
            if (settings == null)
                return false;

            bool changed = NormalizeSettingsData(settings);
            if (RunSettingsMigrations(settings))
                changed = true;

            return changed;
        }

        internal static bool TryImportSettings(string sourceFilePath, out SettingsImportResult result, out string errorMessage)
        {
            result = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                errorMessage = "No settings file was selected.";
                return false;
            }

            string fullSourcePath;
            try
            {
                fullSourcePath = Path.GetFullPath(sourceFilePath);
            }
            catch (Exception ex)
            {
                errorMessage = $"The selected settings path is invalid: {ex.Message}";
                return false;
            }

            if (File.Exists(fullSourcePath) == false)
            {
                errorMessage = "The selected settings file does not exist.";
                return false;
            }

            if (string.Equals(fullSourcePath, SettingsFilePath, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "The selected file is already the active settings file.";
                return false;
            }

            FileInfo sourceFileInfo = new FileInfo(fullSourcePath);
            if (sourceFileInfo.Length <= 0)
            {
                errorMessage = "The selected settings file is empty.";
                return false;
            }

            if (sourceFileInfo.Length > 1024 * 1024)
            {
                errorMessage = "The selected settings file is unexpectedly large and was rejected for safety.";
                return false;
            }

            try
            {
                AppSettingsData importedSettings = LoadSettingsDataFromFile(fullSourcePath);
                bool wasNormalizedOrMigrated = PrepareSettingsData(importedSettings);

                string verifiedJson = SerializeSettingsData(importedSettings);
                AppSettingsData verifiedSettings = LoadSettingsDataFromJson(verifiedJson);
                PrepareSettingsData(verifiedSettings);

                if (AreSettingsEqual(importedSettings, verifiedSettings) == false)
                    throw new InvalidOperationException("Imported settings verification failed after serialization.");

                string settingsDirectory = Path.GetDirectoryName(SettingsFilePath);
                if (string.IsNullOrWhiteSpace(settingsDirectory) == false)
                    Directory.CreateDirectory(settingsDirectory);

                string tempDirectory = string.IsNullOrWhiteSpace(settingsDirectory)
                    ? Application.StartupPath
                    : settingsDirectory;
                string tempFilePath = Path.Combine(tempDirectory, $"settings.import.{Guid.NewGuid():N}.tmp");

                File.WriteAllText(tempFilePath, verifiedJson, Encoding.UTF8);

                string backupFilePath = null;
                if (File.Exists(SettingsFilePath))
                {
                    backupFilePath = BuildSettingsImportBackupPath();
                    File.Replace(tempFilePath, SettingsFilePath, backupFilePath, true);
                }
                else
                {
                    File.Move(tempFilePath, SettingsFilePath);
                }

                if (AppSettings == null)
                    AppSettings = new AppSettingsJson<AppSettingsData>(SettingsFilePath);

                AppSettings.Data = importedSettings;
                result = new SettingsImportResult(fullSourcePath, backupFilePath, wasNormalizedOrMigrated);
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    AppSettings?.LoadSettings();
                }
                catch
                {
                }

                errorMessage = $"Settings import failed. Current settings were kept. {ex.Message}";
                return false;
            }
        }

        private static bool RunSettingsMigrations(AppSettingsData settings)
        {
            if (settings == null)
                return false;

            bool changed = false;

            if (settings.SettingsVersion < 1)
            {
                settings.RecursivePdfSearch = true;
                changed = true;
            }

            if (settings.SettingsVersion < 2)
            {
                settings.UseSmartSearch = true;
                changed = true;
            }

            if (settings.SettingsVersion < 3)
            {
                settings.OpenAddMode = OpenAddMode.Normal;
                changed = true;
            }

            if (settings.SettingsVersion < 4)
            {
                settings.AutofixMode = AutoPreprocessingMode.Normal;
                changed = true;
            }

            if (settings.SettingsVersion < 5)
            {
                settings.LowQuantileDeletingMode = LowQuantileDeletingMode.OnlyRecordsWithValidJifTags;
                changed = true;
            }

            if (settings.SettingsVersion < 6)
            {
                settings.PerformanceOptimizationMode = PerformanceOptimizationMode.OptimizeForQualityPerformanceRatio;
                changed = true;
            }

            if (settings.SettingsVersion != AppSettingsData.CURRENT_SETTINGS_VERSION)
            {
                settings.SettingsVersion = AppSettingsData.CURRENT_SETTINGS_VERSION;
                changed = true;
            }

            return changed;
        }

        private static bool NormalizeSettingsData(AppSettingsData settings)
        {
            bool changed = false;
            AppSettingsData defaults = new AppSettingsData();

            string[] normalizedColumns = NormalizeStringArray(settings.Columns);
            if (AreStringArraysEqual(settings.Columns, normalizedColumns) == false)
            {
                settings.Columns = normalizedColumns;
                changed = true;
            }

            string[] normalizedStandardColumns = NormalizeStringArray(settings.StandardColumns);
            if (normalizedStandardColumns.Length == 0)
            {
                normalizedStandardColumns = defaults.StandardColumns.ToArray();
                changed = true;
            }

            if (AreStringArraysEqual(settings.StandardColumns, normalizedStandardColumns) == false)
            {
                settings.StandardColumns = normalizedStandardColumns;
                changed = true;
            }

            string[] normalizedSelectedTags = NormalizeStringArray(settings.SelectedTags);
            if (AreStringArraysEqual(settings.SelectedTags, normalizedSelectedTags) == false)
            {
                settings.SelectedTags = normalizedSelectedTags;
                changed = true;
            }

            string[] normalizedSelectedTypes = NormalizeStringArray(settings.SelectedTypes);
            if (AreStringArraysEqual(settings.SelectedTypes, normalizedSelectedTypes) == false)
            {
                settings.SelectedTypes = normalizedSelectedTypes;
                changed = true;
            }

            if (settings.LastExportSettings == null)
            {
                settings.LastExportSettings = new LastExportSettingsData();
                changed = true;
            }
            else
            {
                changed |= NormalizeLastExportSettings(settings.LastExportSettings);
            }

            if (settings.Threads <= 0)
            {
                settings.Threads = defaults.Threads;
                changed = true;
            }

            if (settings.PdfAutoPairThresholdPercent < 0 || settings.PdfAutoPairThresholdPercent > 100)
            {
                settings.PdfAutoPairThresholdPercent = Math.Max(0, Math.Min(100, settings.PdfAutoPairThresholdPercent));
                changed = true;
            }

            if (settings.NumberOfBackups <= 0)
            {
                settings.NumberOfBackups = defaults.NumberOfBackups;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.DefaultCsvSeparator))
            {
                settings.DefaultCsvSeparator = defaults.DefaultCsvSeparator;
                changed = true;
            }

            if (Enum.IsDefined(typeof(MetadataScreenMode), settings.MetadataScreenMode) == false)
            {
                settings.MetadataScreenMode = defaults.MetadataScreenMode;
                changed = true;
            }

            if (Enum.IsDefined(typeof(AutoPreprocessingMode), settings.AutoPreprocessingMode) == false)
            {
                settings.AutoPreprocessingMode = defaults.AutoPreprocessingMode;
                changed = true;
            }

            if (Enum.IsDefined(typeof(AutoPreprocessingMode), settings.AutofixMode) == false)
            {
                settings.AutofixMode = defaults.AutofixMode;
                changed = true;
            }

            if (Enum.IsDefined(typeof(PdfSourceMatchMode), settings.PdfSourceMatchMode) == false)
            {
                settings.PdfSourceMatchMode = defaults.PdfSourceMatchMode;
                changed = true;
            }

            if (Enum.IsDefined(typeof(PasteAnythingMode), settings.PasteAnythingMode) == false)
            {
                settings.PasteAnythingMode = defaults.PasteAnythingMode;
                changed = true;
            }

            if (Enum.IsDefined(typeof(OpenAddMode), settings.OpenAddMode) == false)
            {
                settings.OpenAddMode = defaults.OpenAddMode;
                changed = true;
            }

            if (Enum.IsDefined(typeof(LowQuantileDeletingMode), settings.LowQuantileDeletingMode) == false)
            {
                settings.LowQuantileDeletingMode = defaults.LowQuantileDeletingMode;
                changed = true;
            }

            if (Enum.IsDefined(typeof(PerformanceOptimizationMode), settings.PerformanceOptimizationMode) == false)
            {
                settings.PerformanceOptimizationMode = defaults.PerformanceOptimizationMode;
                changed = true;
            }

            return changed;
        }

        private static bool NormalizeLastExportSettings(LastExportSettingsData settings)
        {
            if (settings == null)
                return false;

            bool changed = false;

            if (string.IsNullOrWhiteSpace(settings.Scope))
            {
                settings.Scope = "All";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.Format))
            {
                settings.Format = "Bib";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.Mode))
            {
                settings.Mode = "Normal";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.CsvSeparator))
            {
                settings.CsvSeparator = ",";
                changed = true;
            }

            return changed;
        }

        private static string[] NormalizeStringArray(string[] values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => string.IsNullOrWhiteSpace(value) == false)
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool AreStringArraysEqual(string[] left, string[] right)
        {
            return (left ?? Array.Empty<string>())
                .SequenceEqual(right ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        private static AppSettingsData LoadSettingsDataFromFile(string filePath)
        {
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            return LoadSettingsDataFromJson(json);
        }

        private static AppSettingsData LoadSettingsDataFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("The settings file is empty.");

            AppSettingsData settings = JsonConvert.DeserializeObject<AppSettingsData>(json);
            if (settings == null)
                throw new InvalidOperationException("The settings file does not contain a valid settings object.");

            return settings;
        }

        private static string SerializeSettingsData(AppSettingsData settings)
        {
            return JsonConvert.SerializeObject(settings, Formatting.Indented);
        }

        private static bool AreSettingsEqual(AppSettingsData left, AppSettingsData right)
        {
            return string.Equals(
                JsonConvert.SerializeObject(left),
                JsonConvert.SerializeObject(right),
                StringComparison.Ordinal);
        }

        private static string BuildSettingsImportBackupPath()
        {
            string directory = Path.GetDirectoryName(SettingsFilePath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"settings.import-backup.{timestamp}.json";
            return string.IsNullOrWhiteSpace(directory)
                ? fileName
                : Path.Combine(directory, fileName);
        }


        /// <summary>
        /// Zobrazi chybovou hlasku a ulozi log
        /// </summary>
        /// <param name="e"></param>
        public static void ResolveException(Exception e)
        {
            string errFile = ErrLog.Log(e);
            MessageBox.Show($"Critical error: see: {errFile}" , APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// Neodchycene vyjimky
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ResolveException((Exception)e.ExceptionObject);
        }
    }

    internal sealed class SettingsImportResult
    {
        public SettingsImportResult(string sourceFilePath, string backupFilePath, bool wasNormalizedOrMigrated)
        {
            SourceFilePath = sourceFilePath;
            BackupFilePath = backupFilePath;
            WasNormalizedOrMigrated = wasNormalizedOrMigrated;
        }

        public string SourceFilePath { get; }
        public string BackupFilePath { get; }
        public bool WasNormalizedOrMigrated { get; }
    }
}
