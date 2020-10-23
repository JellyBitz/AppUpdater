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
        #region Private Members
        /// <summary>
        /// Bytes chunks for downloading (1kb as default)
        /// </summary>
        private int m_ChunkByteSize = 1024 * 1000;
        /// <summary>
        /// Bytes received from downloading
        /// </summary>
        private int m_BytesReceived;
        /// <summary>
        /// Flag indicating if the download has been made successfully
        /// </summary>
    	private bool m_IsDownloadCompleted;
        /// <summary>
        /// Check if the download is paused
        /// </summary>
        private volatile bool m_IsPaused;
        #endregion

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
        /// File location while downloading it
        /// </summary>
        public string DownloadPath { get; }
        /// <summary>
        /// Check if the download is paused
        /// </summary>
        public bool IsPaused
        {
            get { return m_IsPaused; }
            private set { m_IsPaused = value; }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates an application file that will be extracted from internet and updated on application
        /// </summary>
        /// <param name="FullPath">Path where is going to be located this file</param>
        /// <param name="Url">Url where this file is located</param>
        /// <param name="DownloadPath">Path where the file will be located on download</param>
        public PatchFile(string FullPath, string Url, string DownloadPath)
        {
            this.FullPath = FullPath;
            this.Url = Url;
            this.DownloadPath = DownloadPath;

            if (File.Exists(DownloadPath))
            {
                // Load length previously downloded
                m_BytesReceived = (int)new FileInfo(DownloadPath).Length;
                // Check if has been paused before
                if(m_BytesReceived > 0)
                   m_IsPaused = true;
            }
            else
            {
                // Create/override file as empty
                using (File.Create(DownloadPath)) { }
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Starts or continue the download of the file
        /// </summary>
        public async Task StartDownload()
        {
            IsPaused = false;

            // Check if file has been downloaded already 
            int BytesToReceive = (int) await GetContentLength(Url);
            if (m_BytesReceived == BytesToReceive)
            {
                m_IsDownloadCompleted = true;
                return;
            }

            // Create file request
            var request = (HttpWebRequest)WebRequest.Create(Url);
            request.Method = "GET";
            request.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705)";
            if (m_BytesReceived > 0)
                request.AddRange(m_BytesReceived);

            // Send request and read stream response
            using (var response = await request.GetResponseAsync())
            {
                using (var responseStream = response.GetResponseStream())
                {
                    using (var fs = new FileStream(DownloadPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        while (!IsPaused)
                        {
                            // Reading stream
                            var buffer = new byte[m_ChunkByteSize];
                            var bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length);

                            // Check if file has been fully downloaded
                            if (bytesRead == 0)
                            {
                                m_IsDownloadCompleted = true;
                                // call event
                                OnDownloadCompleted();
                                break;
                            }

                            // Write to file and avance position
                            await fs.WriteAsync(buffer, 0, bytesRead);
                            m_BytesReceived += bytesRead;

                            // call event
                            OnDownloadProgressChanged(m_BytesReceived, BytesToReceive);
                        }
                        // Clear file stream cache
                        await fs.FlushAsync();
                    }
                }
            }
        }
        /// <summary>
        /// Stops downloading the file to continue it later
        /// </summary>
        public void PauseDownload()
        {
            IsPaused = true;
        }
        /// <summary>
        /// Update the file to the physical location
        /// </summary>
        public async Task Update()
        {
            // Check if the file has been downloaded previously
            if (m_IsDownloadCompleted)
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
                await Task.Run(() => File.Move(DownloadPath, FullPath)); // This action can take a while!
                if (alreadyExist)
                    await Task.Run(() => File.Delete(fileBackup));

                // call event
                OnUpdateCompleted();

                // Successfully updated, avoid doing it twice
                m_IsDownloadCompleted = false;
            }
        }
        #endregion

        #region Private Helpers
        /// <summary>
        /// Obtain the file size from url
        /// </summary>
        private async Task<long> GetContentLength(string Url)
        {
            var request = (HttpWebRequest)WebRequest.Create(Url);
            request.Method = "HEAD";
            // send request
            using (var response = await request.GetResponseAsync()) { 
                return response.ContentLength;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// Called when the download progress has changed
        /// </summary>
        public event DownloadProgressChangedEventHandler DownloadProgressChanged;
        public delegate void DownloadProgressChangedEventHandler(object sender, DownloadProgressChangedEventArgs e);
        private void OnDownloadProgressChanged(int BytesReceived, int BytesToRead)
        {
            DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(BytesReceived, BytesToRead));
        }
        public class DownloadProgressChangedEventArgs : EventArgs
        {
            public int BytesReceived { get; }
            public int BytesToRead { get; }
            public double Percentage { get { return BytesReceived * 100d / BytesToRead; } }
            internal DownloadProgressChangedEventArgs(int BytesReceived, int BytesToRead)
            {
                this.BytesReceived = BytesReceived;
                this.BytesToRead = BytesToRead;
            }
        }
        /// <summary>
        /// Called when the file has been downloaded
        /// </summary>
        public event DownloadCompletedEventHandler DownloadCompleted;
        public delegate void DownloadCompletedEventHandler(object sender, EventArgs e);
        private void OnDownloadCompleted()
        {
            DownloadCompleted?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>
        /// Called when the file has been updated
        /// </summary>
        public event UpdateCompletedEventHandler UpdateCompleted;
        public delegate void UpdateCompletedEventHandler(object sender, EventArgs e);
        private void OnUpdateCompleted()
        {
            UpdateCompleted?.Invoke(this, EventArgs.Empty);
        }
        #endregion
    }
}