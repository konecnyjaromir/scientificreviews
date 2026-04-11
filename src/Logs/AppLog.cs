using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScientificReviews.Logs
{
    public static class AppLog
    {
        private static readonly object Sync = new object();
        private static string _extension = "log";
        /// <summary>
        /// Přípona souborů ErrorLogu
        /// </summary>
        public static string Extension
        {
            get { return _extension; }
            set { _extension = value; }
        }

        private static string _errPath = "log\\";
        /// <summary>
        /// Nastaví relativní cestu začíná bez lomítka končí lomítkem
        /// </summary>
        public static string ErrPath
        {
            get { return _errPath; }
            set { _errPath = value; }
        }

        private static string _appName = "";
        /// <summary>
        /// Jménmo aplikace pro rozlišení názvu Logu
        /// </summary>
        public static string AppName
        {
            get { return _appName; }
            set { _appName = value; }
        }

        private static string _globalAppPath = "";
        /// <summary>
        /// Nastaví globání cestu k aplikaci Updater
        /// </summary>
        public static string GlobalAppPath
        {
            get { return _globalAppPath; }
            set { _globalAppPath = value; }
        }

        public enum MessageType
        {
            Error, Exclamation, Info
        }

        public static string GetCurrentFile()
        {
            string path = Path.Combine(_globalAppPath, _errPath);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            String fileName;
            if (_appName == "")
            {
                fileName = Path.Combine(path, DateTime.Now.Year.ToString() + "_" + DateTime.Now.Month.ToString("00") + "_" +
                  DateTime.Now.Day.ToString("00") + "." + _extension);
            }
            else
            {
                fileName = Path.Combine(path, _appName + "_" + DateTime.Now.Year.ToString() + "_" + DateTime.Now.Month.ToString("00") + "_" +
               DateTime.Now.Day.ToString("00") + "." + _extension);
            }
            return fileName;
        }

        /// <summary>
        /// Zaloguje chybovou hlášku e do souboru.
        /// </summary>
        /// <param name="e">Chybová proměná Exception</param>
        public static string Log(string message, MessageType type)
        {
            try
            {
                String fileName = GetCurrentFile();

                bool append = File.Exists(fileName);
                string typeString;
                switch (type)
                {
                    case MessageType.Error:
                        typeString = "ERROR";
                        break;
                    case MessageType.Exclamation:
                        typeString = "EXCLAMATION";
                        break;
                    default:
                        typeString = "INFO";
                        break;
                }

                lock (Sync)
                {
                    using (StreamWriter sw = new StreamWriter(fileName,
                               append, new UnicodeEncoding(true, true)))
                    {
                        sw.WriteLine(string.Format("{0} {1}: {2}", DateTime.Now.ToString(), typeString, message));
                    }
                }
                return fileName;
            }
            catch
            { }
            return null;
        }
    }
}
