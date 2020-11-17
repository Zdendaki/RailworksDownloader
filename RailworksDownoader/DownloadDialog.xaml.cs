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
            Title = "Preparing download!";
            //FileName.Content = "";
        }

        public async Task DownloadFile(HashSet<int> download, HashSet<Package> cached, List<Package> installedPackages, WebWrapper wrapper, SqLiteAdapter sqLiteAdapter)
        {           
            for (int i = 0; i < download.Count; i++)
            {
                Package p = cached.FirstOrDefault(x => x.PackageId == download.ElementAt(i));
                Dispatcher.Invoke(() =>
                {
                    Title = $"Downloading packages {i + 1}/{download.Count}";
                    FileName.Content = p?.DisplayName ?? "#INVALID FILE NAME";
                });

                await Task.Run(async () =>
                {
                    int pkgId = download.ElementAt(i);
                    ObjectResult<object> dl_result = await wrapper.DownloadPackage(pkgId, App.Token);

                    if (dl_result.code == 1)
                    {
                        using (ZipArchive a = ZipFile.OpenRead((string)dl_result.content)) {
                            foreach (ZipArchiveEntry e in a.Entries)
                            {
                                e.ExtractToFile(Path.Combine(App.Railworks.AssetsPath, cached.Where(x => x.PackageId == pkgId).Select(x => x.TargetPath).First()), true);
                            }
                        }

                        installedPackages.Add(p);
                        sqLiteAdapter.SaveInstalledPackage(p);
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
