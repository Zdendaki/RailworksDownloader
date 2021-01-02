using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro DownloadDialog.xaml
    /// </summary>
    public partial class DownloadDialog : ContentDialog
    {
        public bool CancelButton
        {
            get => IsSecondaryButtonEnabled;
            set => IsSecondaryButtonEnabled = value;
        }

        public DownloadDialog(bool cancel = true)
        {
            InitializeComponent();
            Title = "Preparing download!";
            CancelButton = cancel;
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
                        Dispatcher.Invoke(() => CancelButton = false);

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
                        new Task(() =>
                        {
                            sqLiteAdapter.FlushToFile(true);
                        }).Start();

                        Dispatcher.Invoke(() => CancelButton = true);
                    }
                    else
                    {
                        //FIXED: replace message box with better designed one

                        new Task(() =>
                        {

                            App.Window.Dispatcher.Invoke(() =>
                            {
                                MainWindow.ErrorDialog = new ContentDialog()
                                {
                                    Title = "Error occured while downloading",
                                    Content = dl_result.message,
                                    SecondaryButtonText = "OK",
                                    Owner = App.Window
                                };

                                MainWindow.ErrorDialog.ShowAsync();
                            });

                        }).Start();

                        //MessageBox.Show((string)dl_result.message, "Error occured while downloading", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    File.Delete((string)dl_result.content);
                });
            }

            App.Window.Dispatcher.Invoke(() => Hide());
        }

        public async Task DownloadPackages(HashSet<int> download, List<Package> cached, List<Package> installedPackages, WebWrapper wrapper, SqLiteAdapter sqLiteAdapter)
        {
            download.RemoveWhere(x => cached.Any(y => y.PackageId == x && y.IsPaid));
            for (int i = 0; i < download.Count; i++)
            {
                Package p = cached.FirstOrDefault(x => x.PackageId == download.ElementAt(i));

                if (p == null)
                {
                    App.Window.Dispatcher.Invoke(() =>
                    {
                        MainWindow.ErrorDialog = new ContentDialog()
                        {
                            Title = "Critical error",
                            Content = "Attempted to download non cached package!",
                            SecondaryButtonText = "OK",
                            Owner = App.Window
                        };

                        MainWindow.ErrorDialog.ShowAsync();
                    });
                    continue;
                }

                Dispatcher.Invoke(() =>
                {
                    Title = $"Downloading packages {i + 1}/{download.Count}";
                    FileName.Content = p?.DisplayName ?? "#INVALID FILE NAME";
                });

                int pkgId = download.ElementAt(i);
                wrapper.OnDownloadProgressChanged += Wrapper_OnDownloadProgressChanged;
                ObjectResult<object> dl_result = await wrapper.DownloadPackage(pkgId, App.Token);

                if (dl_result.code == 1)
                {
                    Dispatcher.Invoke(() => CancelButton = false);

                    using (ZipArchive a = ZipFile.OpenRead((string)dl_result.content))
                    {
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
                    sqLiteAdapter.FlushToFile(true);

                    Dispatcher.Invoke(() => CancelButton = true);
                }
                else
                {
                    //FIXED: replace message box with better designed one

                    new Task(() =>
                    {

                        App.Window.Dispatcher.Invoke(() =>
                        {
                            MainWindow.ErrorDialog = new ContentDialog()
                            {
                                Title = "Error occured while downloading",
                                Content = dl_result.message,
                                SecondaryButtonText = "OK",
                                Owner = App.Window
                            };

                            MainWindow.ErrorDialog.ShowAsync();
                        });

                    }).Start();

                    //MessageBox.Show((string)dl_result.message, "Error occured while downloading", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                File.Delete((string)dl_result.content);
            }

            download = new HashSet<int>();
            App.Window.Dispatcher.Invoke(() => Hide());
        }

        internal void DownloadUpdateAsync(Updater updater)
        {
            Dispatcher.Invoke(() =>
            {
                Title = $"Downloading update of application...";
                FileName.Content = null;
                CancelButton = false;
                DownloadProgress.IsIndeterminate = false;
                SecondaryButtonText = null;
                DownloadProgress.Value = 0;
            });

            updater.OnDownloadProgressChanged += (progress) =>
            {
                Dispatcher.Invoke(() =>
                {
                    DownloadProgress.Value = progress;
                    Progress.Content = $"{progress} %";
                });
            };

            updater.OnDownloaded += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    Title = $"Installing update...";
                    FileName.Content = "Application will be restarted";
                    DownloadProgress.Value = 100;
                    DownloadProgress.IsIndeterminate = true;
                    Progress.Content = null;
                });
            };
        }

        private void Wrapper_OnDownloadProgressChanged(float progress)
        {
            App.Window.Dispatcher.Invoke(() =>
            {
                if (progress >= 100)
                {
                    DownloadProgress.IsIndeterminate = true;
                    Progress.Content = "Installing...";
                }
                else
                {
                    DownloadProgress.IsIndeterminate = false;
                    DownloadProgress.Value = progress;
                    Progress.Content = $"{progress} %";
                }
            });
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (CancelButton)
            {
                App.Window.Dispatcher.Invoke(() =>
                {
                    App.Window.ScanRailworks.IsEnabled = true;
                    App.Window.SelectRailworksLocation.IsEnabled = true;
                    App.Window.DownloadMissing.IsEnabled = true;
                });
            }
            else
                args.Cancel = true;
        }
    }
}
