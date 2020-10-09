using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace AppUpdater
{
    /// <summary>
    /// Represents a file that can be downloaded from internet
    /// </summary>
    public class PatchFile
    {

        #region Public Properties
        /// <summary>
        /// Relative path where the file will be located after downloading
        /// </summary>
        public string FullPath { get; }
        /// <summary>
        /// Website location from this file
        /// </summary>
        public string Url { get; }
        /// <summary>
        /// Temporal location of this file after being downloaded
        /// </summary>
        public string TempPath { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates an application file that will be extracted from internet and updated on application
        /// </summary>
        /// <param name="FullPath">Path where is going to be located this file</param>
        /// <param name="Url">Url where this file is located</param>
        public PatchFile(string FullPath, string Url)
        {
            this.FullPath = FullPath;
            this.Url = Url;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Download the file from website
        /// </summary>
        /// <param name="TempPath">Temporal path where the file will be located on download</param>
        public async Task Download(string TempPath)
        {
            // Set SSL/TLS is correctly being set
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Creates a client to download the resource
            using (WebClient web = new WebClient())
            {
                // Create temporal file location
                do
                {
                    this.TempPath = Path.Combine(TempPath, Path.GetRandomFileName());
                } while (File.Exists(this.TempPath));
                
                // Put the resource on temporal location while downloading
                await web.DownloadFileTaskAsync(new Uri(Url), this.TempPath);
            }
        }
        /// <summary>
        /// Update the file to the physical location
        /// </summary>
        public async Task Update()
        {
            // Check if the file has been downloaded previously
            if (!string.IsNullOrEmpty(TempPath))
            {
                // Create directory if doesn't exists
                var dir = Path.GetDirectoryName(FullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Force moving file (backup the old, deletes the old backup, moves the new, deletes the old)
                bool alreadyExist = File.Exists(FullPath);
                var fileBackup = FullPath + ".temp.bkp";
                if (alreadyExist)
                {
                    if (File.Exists(fileBackup))
                        await Task.Run(() => File.Delete(fileBackup));
                    await Task.Run(() => File.Move(FullPath, fileBackup));
                }
                await Task.Run(() => File.Move(TempPath, FullPath)); // This action can take a while!
                if (alreadyExist)
                    await Task.Run(() => File.Delete(fileBackup));

                // Successfully updated, avoid doing it twice
                TempPath = null;
            }
        }
        #endregion
    }
}
