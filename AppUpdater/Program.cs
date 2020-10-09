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
            Console.Title = "AppUpdater v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
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
            // Create a downloader with the application version
            var patcher = new PatchManager(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Checking for updates...");
            var patchVersion = await patcher.CheckForUpdates("http://127.0.0.1/hosting-sample/patch_version.json");
            // Check if an update is available
            if (patchVersion.IsUpdateAvailable)
            {
                await patcher.PrepareDownload(patchVersion);
                patcher.DownloadingFile += (Path) =>
                {
                    Console.WriteLine("Downloading: " + Path);
                };
                patcher.FileUpdated += (Path) =>
                {
                    Console.WriteLine("Updating: " + Path);
                };
                patcher.ProgressValueChanged += (Value) =>
                {
                    Console.WriteLine("Percentage Now: " + Value + "%");
                };
                patcher.ApplicationRestart += () =>
                {
                    Console.WriteLine("The application is going to be restarted.\n > Press any key twice to Continue!");
                    // Twice since the void main is consuming one
                    Console.ReadKey();
                };
                Console.WriteLine("Updating application...");
                // Start update all prepared files
                await patcher.StartUpdate();
            }
            else
            {
                Console.WriteLine("Your application is up to date!");
            }
        }
    }
}
