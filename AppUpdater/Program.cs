using System;
namespace AppUpdater
{
    public class Program
    {
        /// <summary>
        /// Application entry point 
        /// </summary>
        static void Main(string[] args)
        {
            Console.Title = "AppUpdater";
            // Start update async
            UpdateAsync();
            // Exit only with escape key
            while (Console.ReadKey(false).Key != ConsoleKey.Escape);
        }
        /// <summary>
        /// Async method used as demo purpose
        /// </summary>
        public static async void UpdateAsync()
        {
            // Create a downloader with the patcher version
            var patcher = new PatchManager();
            Console.WriteLine("AppUpdater " + (patcher.CurrentVersion == new Version() ? "(Default)" : "v" + patcher.CurrentVersion));
            Console.WriteLine(" * Checking for updates...");
            var patchVersion = await patcher.CheckForUpdates("https://jellybitz.github.io/AppUpdater/standart-sample/patch_version.json");
            // Check if an update is available
            if (patchVersion.IsUpdateAvailable)
            {
                // Collect all files to be downloaded
                await patcher.PrepareDownload(patchVersion);

                // Supported events...
                patcher.FileReadyToDownload += (Path) => Console.WriteLine(" File Ready: " + Path);
                patcher.FileDownloadProgressValueChanged += (Path, e) => Console.WriteLine(" File Downloading: " + Path + " " + Math.Round(e.BytesReceived * 100f / e.TotalBytesToReceive, 2) + "%");
                patcher.FileUpdated += (Path) => Console.WriteLine(" File Updated: " + Path);
                patcher.DownloadProgressValueChanged += (FilesCount,FilesMax) => Console.WriteLine(" * Update on " + Math.Round(FilesCount * 100f / FilesMax, 2) + "%");
                patcher.PatchCompleted += (Version) => Console.WriteLine(" * Patch updated, your version now: "+Version);
                patcher.UpdateCompleted += () => Console.WriteLine(" * Update has been finished!");
                patcher.ApplicationRestart += () =>
                {
                    Console.WriteLine(" * The application is going to be restarted.\n > Press any key twice to Continue!");
                    // Twice since the void main is consuming one
                    Console.ReadKey();
                };

                // Start update all prepared files
                Console.WriteLine("Updating application...");
                await patcher.StartUpdate();
            }
            else
            {
                Console.WriteLine("Your application is up to date!");
            }
        }
    }
}
