namespace AppUpdater
{
    /// <summary>
    /// A mirror class for "patch_info.json"
    /// </summary>
    public class PatchInfo
    {
        /// <summary>
        /// Version of this patch
        /// </summary>
        public string Version { get; set; }
        /// <summary>
        /// Hosted location of this patch
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Files used for this patch
        /// </summary>
        public string[] Files { get; set; }
        /// <summary>
        /// Patch url dependency
        /// </summary>
        public string PatchRequiredUrl { get; set; }
    }
}
