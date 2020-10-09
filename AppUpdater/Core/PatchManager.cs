using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
namespace AppUpdater
{
    /// <summary>
    /// Handles all the patch update from downloading to replacing the file
    /// </summary>
    public class PatchManager
    {
        #region Private Members
        /// <summary>
        /// Progress var tracking value
        /// </summary>
        private int
            m_PatchFilesMax,
            m_PatchFilesCount,
            m_PatchFilesProgressValue;
        #endregion

        #region Public Properties
        /// <summary>
        /// Version handled to download patches
        /// </summary>
        public Version CurrentVersion { get; }
        /// <summary>
        /// All files required for the patch
        /// </summary>
        public Dictionary<string, PatchFile> PatchFiles { get; private set; }
        /// <summary>
        /// Check if the update progress is paused
        /// </summary>
        public bool IsUpdatePaused { get; private set; }
        /// <summary>
        /// Check if the update progress is paused
        /// </summary>
        public bool IsUpdating { get; private set; }

        public int DownloadProgressValue {
            get
            {
                return m_PatchFilesProgressValue;
            }
            private set
            {
                if (value == m_PatchFilesProgressValue)
                    return;
                // Set if is a new value
                m_PatchFilesProgressValue = value;
                // call event
                OnProgressValueChanged(value);
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates an updater manager to download and update files
        /// </summary>
        /// <param name="CurrentVersion">Current version with format like "1.0.0.0"</param>
        public PatchManager(Version CurrentVersion)
        {
            this.CurrentVersion = CurrentVersion;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Check for updates and returns the result.
        /// </summary>
        /// <param name="PatchVersionUrl">Url location of the version file</param>
        public async Task<PatchVersion> CheckForUpdates(string PatchVersionUrl)
        {
            // Set SSL/TLS is correctly being set
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Create web client to download the patch info
            using (WebClient web = new WebClient())
            {
                // Download, read and convert JSON to class
                PatchVersion patchVersion = JsonConvert.DeserializeObject<PatchVersion>(await web.DownloadStringTaskAsync(PatchVersionUrl));
                patchVersion.IsUpdateAvailable = CurrentVersion < Version.Parse(patchVersion.LastestVersion);
                return patchVersion;
            }
        }
        /// <summary>
        /// Prepare the files to be downloaded for the patch
        /// </summary>
        /// <param name="Patch">Patch to check</param>
        public async Task PrepareDownload(PatchVersion Patch)
        {
            // Set SSL/TLS is correctly being set
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Create web client to check patch files
            using (WebClient web = new WebClient())
            {
                // Patch to download
                var patchRequiredUrl = Patch.PatchUrl;
                // Collect all files that will be used for patching
                PatchFiles = new Dictionary<string, PatchFile>();

                PatchInfo patchInfo = null;
                do
                {
                    // Download, read and convert JSON to class
                    patchInfo = JsonConvert.DeserializeObject<PatchInfo>(await web.DownloadStringTaskAsync(patchRequiredUrl));

                    // If the version is the same as my current version, stop it
                    if (Version.Parse(patchInfo.Version) == CurrentVersion)
                        break;

                    // Add all files from patch
                    foreach (string file in patchInfo.Files)
                    {
                        // Collects non repeated files
                        if (!PatchFiles.ContainsKey(file))
                            PatchFiles.Add(file, new PatchFile(file, patchInfo.Host + file));
                    }

                    // Collect files from patch dependencies
                    patchRequiredUrl = patchInfo.PatchRequiredUrl;

                } while (!string.IsNullOrEmpty(patchRequiredUrl));

                // Check if the update version is matching with app
                if (Version.Parse(patchInfo.Version) > CurrentVersion)
                    throw new ApplicationException("The version of your application is too old to be updated.");

                m_PatchFilesMax = PatchFiles.Count;
                m_PatchFilesCount = 0;
                m_PatchFilesProgressValue = 0;
            }
        }

        /// <summary>
        /// Start/continue updating the application
        /// </summary>
        public async Task StartUpdate()
        {
            // Check Flags about pause/continue synchronization
            if (IsUpdating)
                return;
            IsUpdatePaused = false;

            // Check there is files to update
            if (PatchFiles == null || PatchFiles.Count == 0)
                return;

            // Create and use temporal path for downloaded files
            var tempPath = "Temp";
            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            // Check if the executable is going to be updated
            PatchFile updateExe = null;
            var executablePath = Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            if (PatchFiles.ContainsKey(executablePath))
            {
                updateExe = PatchFiles[executablePath];
                PatchFiles.Remove(executablePath);
            }

            // Start Downloading files from old to new
            List<string> keys = new List<string>(PatchFiles.Keys);
            keys.Reverse();

            // Download and update each file
            foreach(var key in keys)
            {
                var f = PatchFiles[key];
                // call event
                OnDownloadingFile(f.FullPath);
                // download it
                await f.Download(tempPath);
                // update it
                await f.Update();
                PatchFiles.Remove(key);

                // call event
                OnFileUpdated(f.FullPath);

                // Check if update has been paused
                if (IsUpdatePaused)
                    break;
            }

            // Delete the temporal path used
            Directory.Delete(tempPath,true);
            
            // Update executable as last one and restart application
            if (updateExe != null)
            {
                // call event
                OnDownloadingFile(updateExe.FullPath);
                // download it
                await updateExe.Download("");
                // update it slightly different (delete super old, move old and forget then move new)
                var thrashPath = Path.Combine(Path.GetTempPath(), executablePath + ".old");
                if (File.Exists(thrashPath))
                    File.Delete(thrashPath);
                await Task.Run(() => File.Move(executablePath, thrashPath));
                await Task.Run(() => File.Move(updateExe.TempPath, executablePath));
                // call event
                OnFileUpdated(updateExe.FullPath);
                // Restart application
                OnApplicationRestart();
                System.Diagnostics.Process.Start(executablePath);
                Environment.Exit(0);
            }
        }
        /// <summary>
        /// Pause updating the application
        /// </summary>
        public void PauseUpdate()
        {
            // Set flags about pause/continue synchronization
            if (!IsUpdating)
                return;
            IsUpdating = false;
            IsUpdatePaused = true;
        }
        #endregion

        #region Events
        /// <summary>
        /// Called before the file is downloaded
        /// </summary>
        public event DownloadingFileEventHandler DownloadingFile;
        public delegate void DownloadingFileEventHandler(string Path);
        private void OnDownloadingFile(string Path)
        {
            DownloadingFile?.Invoke(Path);
        }
        /// <summary>
        /// Called when the file has been updated
        /// </summary>
        public event FileUpdatedEventHandler FileUpdated;
        public delegate void FileUpdatedEventHandler(string Path);
        private void OnFileUpdated(string Path)
        {
            // TO DO:
            // Create backup to continue the update on restart
            
            FileUpdated?.Invoke(Path);

            // Count file updated
            m_PatchFilesCount += 1;
            DownloadProgressValue = m_PatchFilesCount * 100 / m_PatchFilesMax;
        }
        /// <summary>
        /// Called when the application is going to be replaced itself to be launched as updated
        /// </summary>
        public event ApplicationRestartEventHandler ApplicationRestart;
        public delegate void ApplicationRestartEventHandler();
        private void OnApplicationRestart()
        {
            ApplicationRestart?.Invoke();
        }
        /// <summary>
        /// Called when the progress value has changed
        /// </summary>
        public event ProgressValueChangedEventHandler ProgressValueChanged;
        public delegate void ProgressValueChangedEventHandler(int NewValue);
        private void OnProgressValueChanged(int NewValue)
        {
            ProgressValueChanged?.Invoke(NewValue);
        }
        #endregion
    }
}
