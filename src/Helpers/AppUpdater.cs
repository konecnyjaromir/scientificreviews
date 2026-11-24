using System;
using System.Threading.Tasks;

using Velopack;
using Velopack.Sources;

namespace ScientificReviews.Helpers
{
    /// <summary>
    /// Automatic updates from GitHub Releases (packages are created by .github/workflows/release.yml).
    /// </summary>
    public sealed class AppUpdater
    {
        public const string RepositoryUrl = "https://github.com/konecnyjaromir/scientificreviews";

        private readonly UpdateManager _manager;

        public AppUpdater()
        {
            // Public repository: no access token, stable releases only
            _manager = new UpdateManager(new GithubSource(RepositoryUrl, null, false));
        }

        /// <summary>
        /// Update downloaded and confirmed by the user, applied by Program.Main after the main window closes.
        /// </summary>
        public static UpdateInfo PendingUpdate { get; set; }

        /// <summary>
        /// False when running from build output or from the standalone zip: updates work only in the installed app.
        /// </summary>
        public bool IsInstalled { get { return _manager.IsInstalled; } }

        public string CurrentVersion { get { return _manager.CurrentVersion?.ToString() ?? "-"; } }

        /// <summary>
        /// True when the application was installed by the Velopack Setup.exe (and therefore updates itself).
        /// </summary>
        public static bool IsInstalledApp()
        {
            try
            {
                return new AppUpdater().IsInstalled;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns null when the application is up to date.
        /// </summary>
        public Task<UpdateInfo> CheckForUpdatesAsync()
        {
            return _manager.CheckForUpdatesAsync();
        }

        public Task DownloadAsync(UpdateInfo update, Action<int> progress)
        {
            return _manager.DownloadUpdatesAsync(update, progress);
        }

        /// <summary>
        /// Exits the application, applies the downloaded update and starts the new version.
        /// </summary>
        public void ApplyAndRestart(UpdateInfo update)
        {
            _manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
        }

        /// <summary>
        /// Called after the main window has closed (unsaved changes and backups already handled).
        /// </summary>
        public static void ApplyPendingUpdate()
        {
            UpdateInfo update = PendingUpdate;
            if (update == null)
                return;

            PendingUpdate = null;
            new AppUpdater().ApplyAndRestart(update);
        }
    }
}
