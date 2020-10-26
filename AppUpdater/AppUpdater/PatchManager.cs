using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using System.Text;
using static AppUpdater.PatchFile;

namespace AppUpdater
{
    /// <summary>
    /// Handles all the patch update from downloading to replacing the file
    /// </summary>
    public class PatchManager
    {
        #region Private Members
        /// <summary>
        /// Progress bar tracking value
        /// </summary>
        private int
            m_PatchFilesMax,
            m_PatchFilesCount;
        /// <summary>
        /// Current file being downloaded
        /// </summary>
        private PatchFile m_CurrentFileUpdating;
        /// <summary>
        /// Check if the version is being handled by the patcher
        /// </summary>
        private bool m_IsHandlingVersion;
        /// <summary>
        /// Quick check to indicate if the application is being updated
        /// </summary>
        private volatile bool m_IsUpdating;
        /// <summary>
        /// Quick check to indicate pause
        /// </summary>
        private volatile bool m_IsUpdatePaused;
        #endregion

        #region Public Properties
        /// <summary>
        /// Version handled to download patches
        /// </summary>
        public Version CurrentVersion { get; private set; }
        /// <summary>
        /// List of all files that will be required to update the application
        /// </summary>
        public Dictionary<string, PatchFile> PatchFiles { get; private set; }
        /// <summary>
        /// All patches required to successfully update the application
        /// </summary>
        public Dictionary<Version, Dictionary<string, PatchFile>> Patches { get; private set; }
        /// <summary>
        /// Path used temporally to download the files
        /// </summary>
        public string DownloadingPath { get; set; } = ".dl";
        /// <summary>
        /// Check if the update process is paused
        /// </summary>
        public bool IsUpdatePaused
        {
            get { return m_IsUpdatePaused; }
            private set { m_IsUpdatePaused = value; }
        }
        /// <summary>
        /// Check if is on updating process
        /// </summary>
        public bool IsUpdating {
            get { return m_IsUpdating; }
            private set { m_IsUpdating = value; }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates an updater manager to download and update files
        /// </summary>
        /// <param name="CurrentVersion">Current version from application</param>
        public PatchManager(Version CurrentVersion)
        {
            this.CurrentVersion = CurrentVersion;
        }
        /// <summary>
        /// Creates an updater manager to download and update files by using his own version control
        /// </summary>
        public PatchManager()
        {
            // Loads version control
            CurrentVersion = LoadVersion();
            m_IsHandlingVersion = true;
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
                PatchVersion patchVersion = JsonDeserializer<PatchVersion>(await web.DownloadStringTaskAsync(PatchVersionUrl));
                patchVersion.IsUpdateAvailable = CurrentVersion < new Version(patchVersion.LastestVersion);
                return patchVersion;
            }
        }

        /// <summary>
        /// Initialize the pachting files to be downloaded and updated
        /// </summary>
        /// <param name="Patch">Patch to prepare</param>
        /// <param name="SupportOldVersion">Check if your application supports versions that are not found on patch</param>
        public async Task InitializeUpdate(PatchVersion Patch, bool SupportOldVersion = false)
        {
            // Set SSL/TLS is correctly being set
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Create web client to check patch files
            using (WebClient web = new WebClient())
            {
                // Patch to download
                var patchRequiredUrl = Patch.PatchUrl;
                // Avoid repeating files
                PatchFiles = new Dictionary<string, PatchFile>();
                // Collect all files that will be used for patching
                Patches = new Dictionary<Version, Dictionary<string, PatchFile>>();

                // Create downloading folder and hide it from user
                if (!Directory.Exists(DownloadingPath))
                {
                    DirectoryInfo dirInfo = Directory.CreateDirectory(DownloadingPath);
                    dirInfo.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
                }

                // Start reading web patches
                Version patchVersion = null;
                do
                {
                    // Download, read and convert JSON to class
                    var patchInfo = JsonDeserializer<PatchInfo>(await web.DownloadStringTaskAsync(patchRequiredUrl));

                    // If the version is the same as my current version, stop it
                    patchVersion = new Version(patchInfo.Version);
                    if (patchVersion == CurrentVersion)
                        break;

                    // Create version patch if doesn't exist
                    if (!Patches.TryGetValue(patchVersion, out Dictionary<string, PatchFile> patch))
                    {
                        patch = new Dictionary<string, PatchFile>();
                        Patches[patchVersion] = patch;
                    }
                    // Collects non repeated files
                    foreach (string file in patchInfo.Files)
                    {
                        if (!PatchFiles.ContainsKey(file))
                        {
                            // Add file to this patch
                            var patchFile = new PatchFile(file, patchInfo.Host + file, Path.Combine(DownloadingPath, PatchFiles.Count.ToString().PadLeft(8, '0')));
                            patch.Add(file, patchFile);
                            PatchFiles.Add(file, patchFile);
                        }
                    }
                    // Remove it if this patch doesn't have files
                    if (patch.Count == 0)
                        Patches.Remove(patchVersion);

                    // Collect files from patch dependencies
                    patchRequiredUrl = patchInfo.PatchRequiredUrl;

                } while (!string.IsNullOrEmpty(patchRequiredUrl));

                // Check if the update version is matching with app
                if (!SupportOldVersion && patchVersion > CurrentVersion)
                    throw new ApplicationException("The version of your application is too old to be updated.");

                // Check if there is cache to synchronize
                LoadCache();

                m_PatchFilesMax = PatchFiles.Count;
                m_PatchFilesCount = 0;
            }
        }

        /// <summary>
        /// Start or continue updating the application
        /// </summary>
        public async Task StartUpdate()
        {
            try
            {
                // Check Flags about pause/continue synchronization
                if (IsUpdating)
                    return;
                IsUpdating = true;
                // Execute update
                await StartUpdating();
            }
            catch(Exception ex)
            {
                // Just throw it
                throw ex;
            }
            finally
            {
                // Stop execution
                IsUpdating = false;
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
            IsUpdatePaused = true;
            // Try to pause it
            m_CurrentFileUpdating?.PauseDownload();
        }
        #endregion

        #region Private Helpers
        /// <summary>
        /// Start/continue updating the application
        /// </summary>
        private async Task StartUpdating()
        {
            IsUpdatePaused = false;

            // Check there is files to update
            if (PatchFiles == null || PatchFiles.Count == 0)
                return;

            // Path to check if the executable needs to be updated
            var exePath = Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);

            // Start Downloading files from old patches to new ones
            var versions = new List<Version>(Patches.Keys);
            versions.Sort((a, b) => { return a.CompareTo(b); });
            foreach (var version in versions)
            {
                var patch = Patches[version];
                // Create a different instance of keys to remove it without issues
                var paths = new List<string>(patch.Keys);
                // Download and update each file from this version
                for (int i = 0; i < paths.Count; i++)
                {
                    // track updating file
                    var f = m_CurrentFileUpdating = patch[paths[i]];
                    // Check if the file is going to replace this executable
                    bool isExecutable = f.FullPath == exePath;
                    if (isExecutable)
                    {
                        // Check if is not last file to update
                        if (i < (paths.Count - 1))
                        {
                            // Add it again as last one
                            paths.Add(f.FullPath);
                            continue;
                        }
                    }

                    // call event
                    OnFileDownloadReady(f);

                    // track event
                    f.DownloadProgressChanged += OnFileDownloadProgressChanged;

                    // download it
                    await f.StartDownload();

                    // untrack event
                    f.DownloadProgressChanged -= OnFileDownloadProgressChanged;

                    // Check if download has been paused
                    if (f.IsPaused)
                        break;
                    
                    // call event
                    var completedEventArgs = OnFileDownloadCompleted(f);

                    // Check if the updating process is going to be handled by manager
                    if (completedEventArgs.CancelUpdate)
                    {
                        await completedEventArgs.ExecuteAction();
                    }
                    else
                    {
                        // Check if is executable
                        if (isExecutable)
                        {
                            // update it slightly different (delete super old, move old and forget then move new)
                            var thrashPath = Path.Combine(DownloadingPath, exePath + ".old");
                            await Task.Run(() =>
                            {
                                if (File.Exists(thrashPath))
                                    File.Delete(thrashPath);
                                File.Move(exePath, thrashPath);
                                File.Move(f.DownloadPath, exePath);
                            });
                        }
                        else
                        {
                            // update it
                            await f.Update();
                        }
                    }

                    // Check if is the executable being updated
                    if (isExecutable)
                        // delete cache
                        DeleteCache();
                    else
                        // Create backup to continue the update on restart
                        SaveCache(version, patch);

                    // remove it from tracking
                    patch.Remove(f.FullPath);
                    PatchFiles.Remove(f.FullPath);

                    // call event
                    OnFileUpdateCompleted(f);

                    // Start calling a events to restart the application
                    if (isExecutable)
                    {
                        // call event
                        OnPatchCompleted(version);

                        // Check if this is the last file from everything for update
                        if (PatchFiles.Count == 0)
                            // call event
                            OnUpdateCompleted();

                        // Restart application
                        OnApplicationRestart();

                        // Execute new executable and exit from this one
                        System.Diagnostics.Process.Start(exePath);
                        Environment.Exit(0);
                    }
                }

                // Stop tracking as updating
                m_CurrentFileUpdating = null;

                // Check if update has been paused
                if (IsUpdatePaused)
                    break;
                
                // call event
                OnPatchCompleted(version);
            }

            // Patch finished
            if (PatchFiles.Count == 0)
            {
                // delete cache
                DeleteCache();
                // call event
                OnUpdateCompleted();
            }
        }
        /// <summary>
        /// Loads the current patch version
        /// </summary>
        private Version LoadVersion()
        {
            // Load the current app data if exists
            var patchVersionPath = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + ".patch";
            if (File.Exists(patchVersionPath))
            {
                // Read patch file structure
                using (var reader = new BinaryReader(new FileStream(patchVersionPath, FileMode.Open)))
                {
                    int versionNumbersLength = reader.ReadInt32();
                    uint[] versionNumbers = new uint[versionNumbersLength];
                    for (int i = 0; i < versionNumbersLength; i++)
                        versionNumbers[i] = reader.ReadUInt32();
                    return new Version(versionNumbers);
                }
            }
            else
            {
                return new Version();
            }
        }
        /// <summary>
        /// Saves the patch version to file
        /// </summary>
        private void SaveVersion()
        {
            var patchVersionPath = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + ".patch";
            // Write patch file structure
            using (var writer = new BinaryWriter(new FileStream(patchVersionPath, FileMode.Create)))
            {
                writer.Write(CurrentVersion.VersionNumbers.Length);
                for (int i = 0; i < CurrentVersion.VersionNumbers.Length; i++)
                    writer.Write(CurrentVersion.VersionNumbers[i]);
            }
        }
        /// <summary>
        /// Check cache from update and synchronize files from patches
        /// </summary>
        private void LoadCache()
        {
            var patchCachePath = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + ".patch.cache";
            if (File.Exists(patchCachePath))
            {
                Version cacheVersion = null;
                Dictionary<string, PatchFile> cachePatch = null;
                // Read patch file structure
                using (var reader = new BinaryReader(new FileStream(patchCachePath, FileMode.Open)))
                {
                    cacheVersion = new Version(reader.ReadString());
                    // for each patch file
                    var filesCount = reader.ReadInt32();
                    cachePatch = new Dictionary<string, PatchFile>(filesCount);
                    for (int j = 0; j < filesCount; j++)
                    {
                        var file = new PatchFile(reader.ReadString(), reader.ReadString(), reader.ReadString());
                        cachePatch.Add(file.FullPath, file);
                    }
                }
                // Merge current patches with cache patch to avoid download all the patch again
                foreach (var version in Patches.Keys)
                {
                    if (version == cacheVersion)
                    {
                        var patch = Patches[version];
                        // Create a keys copy to remove from dictionary without issues
                        var paths = new List<string>(patch.Keys);
                        // If file doesn't exist on cache patch, then is already updated
                        foreach (var path in paths)
                        {
                            // Avoid update it again
                            if (!cachePatch.ContainsKey(path))
                            {
                                patch.Remove(path);
                                PatchFiles.Remove(path);
                            }
                        }
                        // Indicate update has been paused
                        IsUpdatePaused = true;
                    }
                }
            }
        }
        /// <summary>
        /// Saves the patch being updated to avoid lossing update progress
        /// </summary>
        private void SaveCache(Version Version, Dictionary<string, PatchFile> Patch)
        {
            var patchCachePath = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + ".patch.cache";
            // Write patch file structure
            using (var writer = new BinaryWriter(new FileStream(patchCachePath, FileMode.Create)))
            {
                writer.Write(Version.ToString());
                // for each patch file
                writer.Write(Patch.Count);
                foreach (var file in Patch.Values)
                {
                    writer.Write(file.FullPath);
                    writer.Write(file.Url);
                    writer.Write(file.DownloadPath);
                }
            }
        }
        /// <summary>
        /// Delete patch cache
        /// </summary>
        private void DeleteCache()
        {
            var patchCachePath = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + ".patch.cache";
            if (File.Exists(patchCachePath))
                File.Delete(patchCachePath);
        }
        /// <summary>
        /// Serialize an object to JSON string
        /// </summary>
        private static string JsonSerializer<T>(T obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
                ser.WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
        /// <summary>
        /// Deserialize a JSON string to an object
        /// </summary>
        private static T JsonDeserializer<T>(string jsonString)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonString)))
            {
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
                return (T)ser.ReadObject(ms);
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// Called before the file starts to be downloaded
        /// </summary>
        public event DownloadReadyEventHandler FileDownloadReady;
        public delegate void DownloadReadyEventHandler(object sender, EventArgs e);
        private void OnFileDownloadReady(PatchFile File)
        {
            FileDownloadReady?.Invoke(File, EventArgs.Empty);
        }
        /// <summary>
        /// Called when the downloaded progress value from file has been changed
        /// </summary>
        public event PatchFile.DownloadProgressChangedEventHandler FileDownloadProgressChanged;
        private void OnFileDownloadProgressChanged(object sender, PatchFile.DownloadProgressChangedEventArgs e)
        {
            FileDownloadProgressChanged?.Invoke(sender, e);
        }
        /// <summary>
        /// Called when the file has been downloaded
        /// </summary>
        public event DownloadCompletedEventHandler FileDownloadCompleted;
        public delegate void DownloadCompletedEventHandler(object sender, DownloadCompletedEventArgs e);
        private DownloadCompletedEventArgs OnFileDownloadCompleted(object sender)
        {
            var e = new DownloadCompletedEventArgs();
            FileDownloadCompleted?.Invoke(sender, e);
            return e;
        }
        public class DownloadCompletedEventArgs : EventArgs
        {
            public bool CancelUpdate { get; set; }
            public Func<Task> UpdateAction { get; set; }
            internal DownloadCompletedEventArgs() {
                
            }
            public async Task ExecuteAction()
            {
                await UpdateAction?.Invoke();
            }
        }
        /// <summary>
        /// Called when the file has been updated
        /// </summary>
        public event PatchFile.UpdateCompletedEventHandler FileUpdateCompleted;
        private void OnFileUpdateCompleted(PatchFile File)
        {
            FileUpdateCompleted?.Invoke(File, EventArgs.Empty);

            // Count file updated
            m_PatchFilesCount += 1;
            OnUpdateProgressChanged(m_PatchFilesCount, m_PatchFilesMax);
        }
        /// <summary>
        /// Called when the updater completed successfully a patch version
        /// </summary>
        public event PatchCompletedEventHandler PatchCompleted;
        public delegate void PatchCompletedEventHandler(object sender, EventArgs e);
        private void OnPatchCompleted(Version Version)
        {
            // Update version if is being automatically handled
            this.CurrentVersion = Version;
            if (m_IsHandlingVersion)
                SaveVersion();

            // call attached event
            PatchCompleted?.Invoke(this,EventArgs.Empty);
        }

        /// <summary>
        /// Called when the download progress value has changed
        /// </summary>
        public event UpdateProgressChangedEventHandler UpdateProgressChanged;
        public delegate void UpdateProgressChangedEventHandler(object sender, UpdateProgressChangedEventArgs e);
        private void OnUpdateProgressChanged(int FilesUpdated, int FilesToUpdate)
        {
            UpdateProgressChanged?.Invoke(this, new UpdateProgressChangedEventArgs(FilesUpdated, FilesToUpdate));
        }
        public class UpdateProgressChangedEventArgs : EventArgs
        {
            public int FilesUpdated { get; }
            public int FilesToUpdate { get; }
            public double Percentage { get { return FilesUpdated * 100 / FilesToUpdate; } }
            internal UpdateProgressChangedEventArgs(int FilesUpdated, int FilesToUpdate)
            {
                this.FilesUpdated = FilesUpdated;
                this.FilesToUpdate = FilesToUpdate;
            }
        }
        /// <summary>
        /// Called when the update has been finished
        /// </summary>
        public event UpdateCompletedEventHandler UpdateCompleted;
        private void OnUpdateCompleted()
        {
            UpdateCompleted?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>
        /// Called when the application needs to be restarted to replace itself correctly
        /// </summary>
        public event ApplicationRestartEventHandler ApplicationRestart;
        public delegate void ApplicationRestartEventHandler(object sender, EventArgs e);
        private void OnApplicationRestart()
        {
            ApplicationRestart?.Invoke(this, EventArgs.Empty);
        }
        #endregion
    }
}
