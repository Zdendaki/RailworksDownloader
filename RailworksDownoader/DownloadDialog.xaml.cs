using ModernWpf.Controls;
using System.Net;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.IO.Compression;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro DownloadDialog.xaml
    /// </summary>
    public partial class DownloadDialog : ContentDialog
    {
        public DownloadDialog()
        {
            InitializeComponent();
        }

        public async Task DownloadFile(HashSet<int> download, HashSet<Package> cached, WebWrapper wrapper)
        {           
            for (int i = 0; i < download.Count; i++)
            {
                Dispatcher.Invoke(() =>
                {
                    Title = $"Downloading packages {i + 1}/{download.Count}";
                    FileName.Content = cached.FirstOrDefault(x => x.PackageId == download.ElementAt(i))?.DisplayName ?? "#INVALID FILE NAME";
                });

                await Task.Run(async () =>
                {
                    int pkgId = download.ElementAt(i);
                    ObjectResult<object> dl_result = await wrapper.DownloadPackage(pkgId, App.Token);

                    if (dl_result.code == 1)
                    {
                        ZipFile.ExtractToDirectory((string)dl_result.content, Path.Combine(App.Railworks.AssetsPath, cached.Where(x => x.PackageId == pkgId).Select(x => x.TargetPath).First())); // TODO: Overwrite,
                    }
                });
            }

            Hide();
        }

        private void Wc_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
                MessageBox.Show(e.Error.Message, "Error occured while downloading", MessageBoxButton.OK, MessageBoxImage.Error);
            else if (!e.Cancelled)
                MessageBox.Show("File downloaded!", "Download complete", MessageBoxButton.OK, MessageBoxImage.Information);

            
        }

        private void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true; // TODO: Cancel downloading
        }
    }
}
