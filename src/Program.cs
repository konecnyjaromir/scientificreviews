using ScientificReviews.Bibtex;
using ScientificReviews.Logs;
using ScientificReviews.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
            ErrLog.GlobalAppPath = Application.UserAppDataPath;
            ErrLog.Extension = "err";
            ErrLog.ErrPath = "err";

            AppLog.GlobalAppPath = Application.UserAppDataPath;
            AppLog.Extension = "log";
            AppLog.ErrPath = "log";
        }

        private static void LoadSettings()
        {
            string settingsFile = Path.Combine(GlobalPath, SETTINGS_FILE_JSON);
            try
            {
                AppSettings = new AppSettingsJson<AppSettingsData>(settingsFile);
                AppSettings.LoadSettings();
                if (AppSettings.Data.Columns == null)
                    AppSettings.Data.Columns = new string[0];
            }
            catch(Exception)
            {              
            }
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
}
