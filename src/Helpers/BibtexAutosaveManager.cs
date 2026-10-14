using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScientificReviews.Helpers
{
    public static class BibtexAutosaveManager
    {
        private const string BaseName = "autosave";
        private const string LatestName = "autosave_latest.bib";

        /// <summary>
        /// Creates a new timestamped autosave backup and prunes old backups.
        /// Also updates autosave_latest.bib to always point to the newest content.
        /// </summary>
        public static void SaveSnapshot(string backupFolder, int keepBackups, string content)
        {
            if (string.IsNullOrWhiteSpace(backupFolder))
                throw new ArgumentException("Backup folder is empty.", nameof(backupFolder));

            if (keepBackups <= 0)
                return;

            Directory.CreateDirectory(backupFolder);

            // 1) write timestamped backup
            string stampName = $"{BaseName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.bib";
            string stampPath = Path.Combine(backupFolder, stampName);
            WriteSnapshot(stampPath, content);

            // 2) update "latest" (overwrite)
            string latestPath = Path.Combine(backupFolder, LatestName);
            WriteSnapshot(latestPath, content);

            // 3) prune old timestamped backups (keep newest N)
            PruneOldSnapshots(backupFolder, keepBackups);
        }

        /// <summary>
        /// Returns full path to the newest backup (prefers autosave_latest.bib if exists).
        /// </summary>
        public static string GetLatestBackupPath(string backupFolder)
        {
            if (string.IsNullOrWhiteSpace(backupFolder) || !Directory.Exists(backupFolder))
                return null;

            string latest = Path.Combine(backupFolder, LatestName);
            if (File.Exists(latest))
                return latest;

            // fallback: newest timestamped file
            return Directory.GetFiles(backupFolder, $"{BaseName}_*.bib")
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .FirstOrDefault()
                ?.FullName;
        }

        private static void PruneOldSnapshots(string backupFolder, int keepBackups)
        {
            var files = Directory.GetFiles(backupFolder, $"{BaseName}_*.bib")
                .Where(file => !string.Equals(Path.GetFileName(file), LatestName, StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .ToList();

            // keep newest keepBackups files; delete the rest
            for (int i = keepBackups; i < files.Count; i++)
            {
                try { files[i].Delete(); }
                catch { /* ignore */ }
            }
        }

        private static void WriteSnapshot(string path, string content)
        {
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, content ?? string.Empty);
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }
}
