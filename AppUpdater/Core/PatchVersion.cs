namespace AppUpdater
{
    /// <summary>
    /// A mirror class for "patch_version.json"
    /// </summary>
    public class PatchVersion
    {
        /// <summary>
        /// The lastest version available to update
        /// </summary>
        public string LastestVersion { get; set; }
        /// <summary>
        /// The url pointing to the "patchfiles.json" to be downloaded
        /// </summary>
        public string PatchUrl { get; set; }
        /// <summary>
        /// Check if an update is available to download
        /// </summary>
        public bool IsUpdateAvailable { get; set; }
    }
}
