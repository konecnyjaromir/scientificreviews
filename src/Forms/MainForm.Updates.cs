using ScientificReviews.Helpers;
using ScientificReviews.Logs;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velopack;

namespace ScientificReviews.Forms
{
    public partial class MainForm
    {
        private bool _updateCheckRunning;

        private void StartUpdateCheckOnStartup()
        {
            if (Program.AppSettings?.Data?.CheckForUpdates != true)
                return;

            _ = CheckForUpdatesAsync(interactive: false);
        }

        /// <summary>
        /// Checks GitHub Releases and lets the user decide about installation.
        /// Interactive = started by the user (reports "up to date", "not installed" and errors in dialogs).
        /// </summary>
        internal async Task CheckForUpdatesAsync(bool interactive)
        {
            if (_updateCheckRunning)
            {
                if (interactive)
                    lblStatus.Text = "Update check is already running.";
                return;
            }

            if (AppUpdater.PendingUpdate != null)
            {
                lblStatus.Text = "Update is downloaded and will be installed when the application closes.";
                return;
            }

            _updateCheckRunning = true;
            try
            {
                AppUpdater updater = new AppUpdater();
                if (!updater.IsInstalled)
                {
                    if (interactive)
                    {
                        MessageBox.Show(this,
                            "Automatic updates are available only for the installed application (Setup.exe from GitHub Releases).\r\n\r\n" +
                            "This copy runs from the standalone zip or from build output. The latest version can be downloaded from:\r\n" +
                            AppUpdater.RepositoryUrl + "/releases",
                            Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                if (interactive)
                    lblStatus.Text = "Checking for updates...";

                UpdateInfo update = await updater.CheckForUpdatesAsync();
                if (update == null)
                {
                    if (interactive)
                    {
                        lblStatus.Text = $"Application is up to date ({updater.CurrentVersion}).";
                        MessageBox.Show(this, $"You are using the latest version ({updater.CurrentVersion}).",
                            Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                string newVersion = update.TargetFullRelease.Version.ToString();
                lblStatus.Text = $"Update {newVersion} is available.";

                DialogResult answer = MessageBox.Show(this,
                    $"A new version {newVersion} is available (current {updater.CurrentVersion}).\r\n\r\n" +
                    "Download and install it now? The application will close (with the usual check for unsaved changes) and restart.",
                    Program.APP_NAME + " - update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer != DialogResult.Yes)
                {
                    lblStatus.Text = $"Update {newVersion} postponed.";
                    return;
                }

                int reported = -1;
                await updater.DownloadAsync(update, progress =>
                {
                    // Progress comes from a background thread, report every 10 %
                    int step = progress / 10;
                    if (step == reported)
                        return;
                    reported = step;
                    if (IsHandleCreated && !IsDisposed)
                        BeginInvoke((MethodInvoker)(() => lblStatus.Text = $"Downloading update {newVersion}: {progress} %"));
                });

                AppLog.Log($"Update {newVersion} downloaded, installing after close.", AppLog.MessageType.Info);
                AppUpdater.PendingUpdate = update;
                lblStatus.Text = $"Update {newVersion} downloaded - it will be installed when the application closes.";

                // Normal closing path: unsaved-changes prompt, autosave backup and settings save run first,
                // Program.Main then applies the update and restarts the application.
                Close();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Update check failed: {ex.Message}";
                AppLog.Log(ex, "Update check failed");
                if (interactive)
                {
                    MessageBox.Show(this, $"Update check failed:\r\n{ex.Message}",
                        Program.APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                _updateCheckRunning = false;
            }
        }
    }
}
