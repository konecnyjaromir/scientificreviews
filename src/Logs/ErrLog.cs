using System;
namespace ScientificReviews.Logs
{
    public static class ErrLog
    {
        private static string _extension = "log";
        /// <summary>
        /// Přípona souborů ErrorLogu
        /// </summary>
        public static string Extension
        {
            get { return ErrLog._extension; }
            set { ErrLog._extension = value; }
        }

        private static string _errPath = "err\\";
        /// <summary>
        /// Nastaví relativní cestu začíná bez lomítka končí lomítkem
        /// </summary>
        public static string ErrPath
        {
            get { return ErrLog._errPath; }
            set { ErrLog._errPath = value; }
        }

        private static string _appName = "";
        /// <summary>
        /// Jménmo aplikace pro rozlišení názvu Logu
        /// </summary>
        public static string AppName
        {
            get { return ErrLog._appName; }
            set { ErrLog._appName = value; }
        }

        private static string _globalAppPath = "";
        /// <summary>
        /// Nastaví globání cestu k aplikaci Updater
        /// </summary>
        public static string GlobalAppPath
        {
            get { return ErrLog._globalAppPath; }
            set { ErrLog._globalAppPath = value; }
        }


        /// <summary>
        /// Zaloguje chybovou hlášku e do souboru.
        /// </summary>
        /// <param name="e">Chybová proměná Exception</param>
        public static string Log(Exception e)
        {
            AppLog.GlobalAppPath = _globalAppPath;
            AppLog.Extension = _extension;
            AppLog.ErrPath = _errPath;
            AppLog.AppName = _appName;
            return AppLog.Log(e);
        }

    }
}
