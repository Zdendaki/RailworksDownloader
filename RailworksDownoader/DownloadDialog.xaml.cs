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

        public async Task UpdatePackages(Dictionary<int, int> update, List<Package> installedPackages, WebWrapper wrapper, SqLiteAdapter sqLiteAdapter)
        {
            for (int i = 0; i < update.Count; i++)
            {
                KeyValuePair<int, int> pair = update.ElementAt(i);
                Package p = installedPackages.FirstOrDefault(x => x.PackageId == pair.Key);
                p.Version = pair.Value;
                Dispatcher.Invoke(() =>
                {
                    Title = $"Updating packages {i + 1}/{update.Count}";
                    FileName.Content = p?.DisplayName ?? "#INVALID FILE NAME";
                });

                await Task.Run(async () =>
                {
                    int pkgId = pair.Key;
                    wrapper.OnDownloadProgressChanged += Wrapper_OnDownloadProgressChanged;
                    ObjectResult<object> dl_result = await wrapper.DownloadPackage(pkgId, App.Token);

                    if (dl_result.code == 1)
                    {
                        using (ZipArchive a = ZipFile.OpenRead((string)dl_result.content))
                        {
                            foreach (ZipArchiveEntry e in a.Entries)
                            {
                                if (e.Name == string.Empty)
                                    continue;

                                string path = Path.GetDirectoryName(Path.Combine(App.Railworks.AssetsPath, installedPackages.Where(x => x.PackageId == pkgId).Select(x => x.TargetPath).First(), e.FullName));

                                if (!Directory.Exists(path))
                                    Directory.CreateDirectory(path);

                                e.ExtractToFile(Path.Combine(path, e.Name), true);
                            }
                        }
                        installedPackages[installedPackages.FindIndex(x => x.PackageId == pkgId)] = p;
                        sqLiteAdapter.SaveInstalledPackage(p);
                        new Task(() => {
                            sqLiteAdapter.FlushToFile(true);
                        }).Start();
                    }
                    else
                    {
                        //FIXME: replace message box with better designed one
                        MessageBox.Show((string)dl_result.message, "Error occured while downloading", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    File.Delete((string)dl_result.content);
                });
            }

            App.Window.Dispatcher.Invoke(() => Hide());
        }

        public async Task DownloadPackage(Package download, List<Package> installedPackages, WebWrapper wrapper, SqLiteAdapter sqLiteAdapter)
        {
            Dispatcher.Invoke(() =>
            {
                Title = $"Downloading package";
                FileName.Content = download?.DisplayName ?? "#INVALID FILE NAME";
            });

            await Task.Run(async () =>
            {
                int pkgId = download.PackageId;
                wrapper.OnDownloadProgressChanged += Wrapper_OnDownloadProgressChanged;
                ObjectResult<object> dl_result = await wrapper.DownloadPackage(pkgId, App.Token);

                if (dl_result.code == 1)
                {
                    using (ZipArchive a = ZipFile.OpenRead((string)dl_result.content))
                    {
                        foreach (ZipArchiveEntry e in a.Entries)
                        {
                            if (e.Name == string.Empty)
                                continue;

                            string path = Path.GetDirectoryName(Path.Combine(App.Railworks.AssetsPath, download.TargetPath, e.FullName));

                            if (!Directory.Exists(path))
                                Directory.CreateDirectory(path);

                            e.ExtractToFile(Path.Combine(path, e.Name), true);
                        }
                    }
                    installedPackages.Add(download);
                    sqLiteAdapter.SaveInstalledPackage(download);
                    new Task(() => {
                        sqLiteAdapter.FlushToFile(true);
                    }).Start();
                }
                else
                {
                    //FIXME: replace message box with better designed one
                    MessageBox.Show((string)dl_result.message, "Error occured while downloading", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                File.Delete((string)dl_result.content);
            });

            App.Window.Dispatcher.Invoke(() => Hide());
        }

        public async Task DownloadPackages(HashSet<int> download, List<Package> cached, List<Package> installedPackages, WebWrapper wrapper, SqLiteAdapter sqLiteAdapter)
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
                    wrapper.OnDownloadProgressChanged += Wrapper_OnDownloadProgressChanged;
                    ObjectResult<object> dl_result = await wrapper.DownloadPackage(pkgId, App.Token);

                    if (dl_result.code == 1)
                    {
                        using (ZipArchive a = ZipFile.OpenRead((string)dl_result.content)) {
                            foreach (ZipArchiveEntry e in a.Entries)
                            {
                                if (e.Name == string.Empty)
                                    continue;
                                
                                string path = Path.GetDirectoryName(Path.Combine(App.Railworks.AssetsPath, cached.Where(x => x.PackageId == pkgId).Select(x => x.TargetPath).First(), e.FullName));

                                if (!Directory.Exists(path))
                                    Directory.CreateDirectory(path);

                                e.ExtractToFile(Path.Combine(path, e.Name), true);
                            }
                        }
                        installedPackages.Add(p);
                        sqLiteAdapter.SaveInstalledPackage(p);
                        new Task(() => {
                            sqLiteAdapter.FlushToFile(true);
                        }).Start();
                    } 
                    else
                    {
                        //FIXME: replace message box with better designed one
                        MessageBox.Show((string)dl_result.message, "Error occured while downloading", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    File.Delete((string)dl_result.content);
                });
            }

            App.Window.Dispatcher.Invoke(() => Hide());
        }

        private void Wrapper_OnDownloadProgressChanged(float progress)
        {
            App.Window.Dispatcher.Invoke(() =>
            {
                if (progress >= 100)
                {
                    DownloadProgress.IsIndeterminate = true;
                    Progress.Content = "Installing...";
                } else
                {
                    DownloadProgress.IsIndeterminate = false;
                    DownloadProgress.Value = progress;
                    Progress.Content = $"{progress} %";
                }
            });
        }

        private void Wc_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
                //FIXME: replace message box with better designed one
                MessageBox.Show(e.Error.Message, "Error occured while downloading", MessageBoxButton.OK, MessageBoxImage.Error);
            else if (!e.Cancelled)
                //FIXME: replace message box with better designed one
                MessageBox.Show("File downloaded!", "Download complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true; // TODO: Cancel downloading
        }
    }
}
