using System;
using System.Windows;
using AppUpdater;

namespace AppDemo
{
    /// <summary>
    /// Demo application
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Private Members
        /// <summary>
        /// Patch manager updating the whole application
        /// </summary>
        private PatchManager m_Patcher = null;
        #endregion

        #region Constructor
        /// <summary>
        /// Default constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }
        #endregion

        #region Raised Events
        private async void Window_Loaded(object sender, RoutedEventArgs re)
        {
            // Using version self handled from host
            m_Patcher = new PatchManager();
            this.Title = "AppUpdater " + (m_Patcher.CurrentVersion == new AppUpdater.Version() ? "(Demo)" : "v" + m_Patcher.CurrentVersion);

            // Start looking for updates
            tbkUpdateProgressText.Text = "Checking for updates...";
            var patch = await m_Patcher.CheckForUpdates("https://jellybitz.github.io/AppUpdater/AppDemo/web/patch_version.json");

            // Check update
            if (patch.IsUpdateAvailable)
            {
                tbkUpdateProgressText.Text = "Update available!";
                btnStart.IsEnabled = btnPause.IsEnabled = true;

                // Initialize files to be updated
                await m_Patcher.InitializeUpdate(patch);

                // Supported events
                m_Patcher.FileDownloadReady += (s, e) =>
                {
                    tbkFileProgressText.Text = ((PatchFile)s).FullPath;
                };
                m_Patcher.FileDownloadProgressChanged += (s, e) =>
                {
                    var percent = e.Percentage;
                    tbkFileProgressPercent.Text = Math.Round(percent, 2) + "%";
                    slrFileProgress.Value = percent;
                };
                m_Patcher.FileDownloadCompleted += (s, e) =>
                {
                    tbkFileProgressPercent.Text = "...";
                };
                m_Patcher.FileUpdateCompleted += (s, e) =>
                {
                    tbkFileProgressPercent.Text = "Ok!";
                };
                m_Patcher.UpdateProgressChanged += (s, e) =>
                {
                    var percent = e.Percentage;
                    tbkUpdateProgressPercent.Text = Math.Round(percent, 2) + "%";
                    slrUpdateProgress.Value = percent;
                };
                m_Patcher.PatchCompleted += (s, e) =>
                {
                    this.Title = "AppUpdater v" + m_Patcher.CurrentVersion;
                };
                m_Patcher.UpdateCompleted += (s, e) =>
                {
                    btnStart.IsEnabled = btnPause.IsEnabled = false;
                    tbkUpdateProgressText.Text = "Your application is up to date!";
                    tbkUpdateProgressPercent.Text = "";
                    tbkFileProgressText.Text = "";
                    tbkFileProgressPercent.Text = "";
                };
                m_Patcher.ApplicationRestart += (s, e) =>
                {
                    MessageBox.Show(this, "This application needs to be restarted.\r\nPress OK to continue.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }
            else
            {
                tbkUpdateProgressText.Text = "Your application is up to date!";
                slrUpdateProgress.Value = 100;
                slrFileProgress.Value = 100;
            }
        }
        private async void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            tbkUpdateProgressText.Text = "Updating...";
            await m_Patcher.StartUpdate();
        }

        private void ButtonPause_Click(object sender, RoutedEventArgs e)
        {
            tbkUpdateProgressText.Text = "Paused...";
            m_Patcher.PauseUpdate();
        }
        #endregion
    }
}
