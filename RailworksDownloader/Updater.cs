using RailworksDownloader.Properties;
using Sentry;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace RailworksDownloader
{
    internal class Updater
    {
        internal delegate void OnDownloadProgressChangedEventHandler(float progress);
        internal event OnDownloadProgressChangedEventHandler OnDownloadProgressChanged;
        internal delegate void OnDownloadedEventHandler();
        internal event OnDownloadedEventHandler OnDownloaded;

        private Uri UpdateUrl { get; set; }

        internal bool CheckUpdates(Uri apiUrl)
        {
            bool isThereNewer = false;
            Task.Run(async () =>
            {
                ObjectResult<AppVersionContent> jsonResult = await WebWrapper.GetAppVersion(apiUrl);
                if (jsonResult != null && Utils.IsSuccessStatusCode(jsonResult.code)) {
                    App.ReportErrors = jsonResult.content.report_errors;

                    if (jsonResult.content.version_name != App.Version)
                    {
                        isThereNewer = true;
                        UpdateUrl = new Uri(jsonResult.content.file_path);
                    }
                }
            }).Wait();

            return isThereNewer;
        }

        internal async Task UpdateAsync()
        {
            App.Window.Dispatcher.Invoke(() =>
            {
                MainWindow.DownloadDialog.ShowAsync();
                MainWindow.DownloadDialog.DownloadUpdateAsync(this);
            });

            OnDownloadProgressChanged?.Invoke(0);

            WebClient webClient = new WebClient();
            webClient.DownloadProgressChanged += (sender, e) =>
            {
                OnDownloadProgressChanged?.Invoke(e.ProgressPercentage);
            };

            try
            {
                string tempFname = Path.GetTempFileName();
                await webClient.DownloadFileTaskAsync(UpdateUrl, tempFname);
                OnDownloaded?.Invoke();

                Thread.Sleep(3000);

                string oldFilename = Assembly.GetExecutingAssembly().Location;

                string ps = Resources.UpdateScript.Replace("##01", tempFname).Replace("##02", oldFilename);
                ExecuteCommand(ps);
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                MessageBox.Show(Localization.Strings.UpdaterAdminDesc, Localization.Strings.ClientUpdateError, MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void ExecuteCommand(string command)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo("PowerShell", $"-Command \"{command}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            try
            {
                Process.Start(processInfo);
            }
            catch
            {
                MessageBox.Show(Localization.Strings.UpdaterAdminDesc, Localization.Strings.UpdaterAdminTitle, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                ExecuteCommand(command);
            }
        }
    }
}
