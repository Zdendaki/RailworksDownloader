using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RailworksDownloader
{
    class Updater
    {
        internal delegate void OnDownloadProgressChangedEventHandler(float progress);
        internal event OnDownloadProgressChangedEventHandler OnDownloadProgressChanged;

        private Uri UpdateUrl { get; set; }

        internal bool CheckUpdates(Uri apiUrl)
        {
            bool isThereNewer = false;
            Task.Run(async () =>
            {
                ObjectResult<AppVersionContent> jsonResult = await WebWrapper.GetAppVersion(apiUrl);
                if (jsonResult != null && jsonResult.code > 0 && jsonResult.content.version_name != App.Version)
                {
                    isThereNewer = true;
                    UpdateUrl = new Uri(jsonResult.content.file_path);
                }
            }).Wait();

            return isThereNewer;
        }

        internal async Task UpdateAsync()
        {
            OnDownloadProgressChanged?.Invoke(0);

            WebClient webClient = new WebClient();
            webClient.DownloadProgressChanged += (sender, e) =>
            {
                OnDownloadProgressChanged?.Invoke(e.ProgressPercentage);
            };

            string tempFname = Path.GetTempFileName();
            await webClient.DownloadFileTaskAsync(UpdateUrl, tempFname);

            string oldFilename = System.Reflection.Assembly.GetExecutingAssembly().Location;

            ExecuteCommand($"ping 127.0.0.1 -n 4 > nul & move \"{tempFname}\" \"{oldFilename}\" > nul & start \"\" \"{oldFilename}\" > nul");
            Environment.Exit(0);
        }

        static void ExecuteCommand(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.Verb = "runas";

            Process.Start(processInfo);
        }
    }
}
