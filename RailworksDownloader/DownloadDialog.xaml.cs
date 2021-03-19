using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private float LastValue { get; set; }

        public DownloadDialog(bool cancel = true)
        {
            InitializeComponent();
            Title = Localization.Strings.PrepareTitle;
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
                    Title = string.Format(Localization.Strings.UpdateTitle, i + 1, update.Count);
                    FileName.Content = p?.DisplayName ?? Localization.Strings.InvalidFileName;
                });

                await Task.Run(async () =>
                {
                    int pkgId = pair.Key;
                    wrapper.OnDownloadProgressChanged += Wrapper_OnDownloadProgressChanged;
                    ObjectResult<object> dl_result = await wrapper.DownloadPackage(pkgId, App.Token);

                    if (Utils.IsSuccessStatusCode(dl_result.code))
                    {
                        Dispatcher.Invoke(() => CancelButton = false);

                        //cleanup before installig new version
                        List<string> removedFiles = Utils.RemoveFiles(sqLiteAdapter.LoadPackageFiles(pkgId));
                        sqLiteAdapter.RemovePackageFiles(removedFiles);

                        List<string> installedFiles = new List<string>();
                        List<string> failedFiles = new List<string>();
                        using (ZipArchive a = ZipFile.OpenRead((string)dl_result.content))
                        {
                            foreach (ZipArchiveEntry e in a.Entries)
                            {
                                if (e.Name == string.Empty) //is directory
                                    continue;

                                string rel_assets_path = Utils.NormalizePath(Path.Combine(installedPackages.Where(x => x.PackageId == pkgId).Select(x => x.TargetPath).First(), e.FullName));
                                string path = Path.GetDirectoryName(Path.Combine(App.Railworks.AssetsPath, rel_assets_path));

                                try
                                {
                                    if (!Directory.Exists(path))
                                        Directory.CreateDirectory(path);

                                    e.ExtractToFile(Path.Combine(path, e.Name), true);
                                    installedFiles.Add(rel_assets_path);
                                }
                                catch
                                {
                                    failedFiles.Add(e.FullName);
                                }
                            }
                        }

                        Trace.Assert(failedFiles.Count == 0, Localization.Strings.FailedCopyFiles, string.Join("\n", failedFiles));

                        sqLiteAdapter.SavePackageFiles(pkgId, installedFiles);
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
                        new Task(() =>
                        {

                            App.Window.Dispatcher.Invoke(() =>
                            {
                                MainWindow.ErrorDialog = new ContentDialog()
                                {
                                    Title = Localization.Strings.DownloadError,
                                    Content = dl_result.message,
                                    SecondaryButtonText = Localization.Strings.Ok,
                                    Owner = App.Window
                                };

                                MainWindow.ErrorDialog.ShowAsync();
                            });

                        }).Start();
                    }

                    File.Delete((string)dl_result.content);
                });
            }

            App.Window.Dispatcher.Invoke(() => Hide());
        }

        public async Task DownloadPackages(HashSet<int> download, List<Package> cached, List<Package> installedPackages, WebWrapper wrapper, SqLiteAdapter sqLiteAdapter)
        {
            download.RemoveWhere(x => cached.Any(y => y.PackageId == x && (y.IsPaid || installedPackages.Any(z => z.PackageId == x))));
            int count = download.Count;
            for (int i = 0; i < count; i++)
            {
                Package p = cached.FirstOrDefault(x => x.PackageId == download.First());

                if (p == null)
                {
                    App.Window.Dispatcher.Invoke(() =>
                    {
                        MainWindow.ErrorDialog = new ContentDialog()
                        {
                            Title = Localization.Strings.CriticalError,
                            Content = Localization.Strings.NonCached,
                            SecondaryButtonText = Localization.Strings.Ok,
                            Owner = App.Window
                        };

                        MainWindow.ErrorDialog.ShowAsync();
                    });
                    continue;
                }

                Dispatcher.Invoke(() =>
                {
                    Title = string.Format(Localization.Strings.DownloadTitle, i + 1, count);
                    string DisplayNameShort = null;

                    if (p?.DisplayName.Length > 50)
                    {
                        DisplayNameShort = (p?.DisplayName.Substring(0, 50) + "...") ?? Localization.Strings.InvalidFileName;
                    }
                    else
                    {
                        DisplayNameShort = p?.DisplayName ?? Localization.Strings.InvalidFileName;
                    }
                    FileName.Content = DisplayNameShort;
                });

                int pkgId = p.PackageId;
                wrapper.OnDownloadProgressChanged += Wrapper_OnDownloadProgressChanged;
                ObjectResult<object> dl_result = await wrapper.DownloadPackage(pkgId, App.Token);

                if (Utils.IsSuccessStatusCode(dl_result.code))
                {
                    Dispatcher.Invoke(() => CancelButton = false);

                    List<string> installedFiles = new List<string>();
                    List<string> failedFiles = new List<string>();
                    using (ZipArchive a = ZipFile.OpenRead((string)dl_result.content))
                    {
                        foreach (ZipArchiveEntry e in a.Entries)
                        {
                            if (e.Name == string.Empty)
                                continue;

                            string rel_assets_path = Path.Combine(cached.Where(x => x.PackageId == pkgId).Select(x => x.TargetPath).First(), e.FullName);
                            string path = Path.GetDirectoryName(Path.Combine(App.Railworks.AssetsPath, rel_assets_path));

                            try
                            {
                                if (!Directory.Exists(path))
                                    Directory.CreateDirectory(path);

                                e.ExtractToFile(Path.Combine(path, e.Name), true);
                                installedFiles.Add(rel_assets_path);
                            }
                            catch
                            {
                                failedFiles.Add(e.FullName);
                            }
                        }
                    }

                    Trace.Assert(failedFiles.Count == 0, Localization.Strings.FailedCopyFiles, string.Join("\n", failedFiles));

                    sqLiteAdapter.SavePackageFiles(pkgId, installedFiles);
                    installedPackages.Add(p);
                    sqLiteAdapter.SaveInstalledPackage(p);
                    sqLiteAdapter.FlushToFile(true);
                    download.Remove(pkgId);
                    Dispatcher.Invoke(() => CancelButton = true);
                }
                else
                {
                    new Task(() =>
                    {

                        App.Window.Dispatcher.Invoke(() =>
                        {
                            MainWindow.ErrorDialog = new ContentDialog()
                            {
                                Title = Localization.Strings.DownloadError,
                                Content = dl_result.message,
                                SecondaryButtonText = Localization.Strings.Ok,
                                Owner = App.Window
                            };

                            MainWindow.ErrorDialog.ShowAsync();
                        });

                    }).Start();
                }

                File.Delete((string)dl_result.content);
            }

            App.Window.Dispatcher.Invoke(() => Hide());
        }

        internal void DownloadUpdateAsync(Updater updater)
        {
            Dispatcher.Invoke(() =>
            {
                Title = Localization.Strings.DownloadingUpdate;
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
                    Title = Localization.Strings.InstallingUpdate;
                    FileName.Content = Localization.Strings.ApplicationRestart;
                    DownloadProgress.Value = 100;
                    DownloadProgress.IsIndeterminate = true;
                    Progress.Content = null;
                });
            };
        }

        private void Wrapper_OnDownloadProgressChanged(float progress)
        {
            if (LastValue != progress)
            {
                Dispatcher.Invoke(() =>
                {
                    if (progress >= 100)
                    {
                        DownloadProgress.IsIndeterminate = true;
                        Progress.Content = Localization.Strings.Installing;
                    }
                    else
                    {
                        DownloadProgress.IsIndeterminate = false;

                        DownloadProgress.Value = progress;
                        Progress.Content = $"{progress} %";
                        LastValue = progress;
                    }
                });
            }
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