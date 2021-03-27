using ModernWpf.Controls;
using Newtonsoft.Json;
using RailworksDownloader.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RailworksDownloader
{
    public class Package
    {
        public int PackageId { get; set; }

        public string FileName { get; set; }

        public string DisplayName { get; set; }

        public int Category { get; set; }

        [JsonIgnore]
        public string CategoryString
        {
            get
            {
                switch (Category)
                {
                    case 0:
                        return Localization.Strings.CatLoco;
                    case 1:
                        return Localization.Strings.CatWag;
                    case 2:
                        return Localization.Strings.CatRVDep;
                    case 3:
                        return Localization.Strings.CatScenery;
                    case 4:
                        return Localization.Strings.CatTrackObj;
                    case 5:
                        return Localization.Strings.CatEnv;
                    default:
                        return Localization.Strings.CatOther;
                }
            }
        }

        public int Era { get; set; }

        [JsonIgnore]
        public string EraString
        {
            get
            {
                switch (Era)
                {
                    case 1:
                        return Localization.Strings.Era1;
                    case 2:
                        return Localization.Strings.Era2;
                    case 3:
                        return Localization.Strings.Era3;
                    case 4:
                        return Localization.Strings.Era4;
                    case 5:
                        return Localization.Strings.Era5;
                    case 6:
                        return Localization.Strings.Era6;
                    default:
                        return Localization.Strings.EraNon;
                }
            }
        }

        public int Country { get; set; }

        [JsonIgnore]
        public string CountryString
        {
            get
            {
                switch (Country)
                {
                    default:
                        return Localization.Strings.CountryNon;
                }
            }
        }

        public int Version { get; set; }

        public int Owner { get; set; }

        public DateTime Datetime { get; set; }

        public string Description { get; set; }

        public string TargetPath { get; set; }

        public bool IsPaid { get; set; }

        public int SteamAppID { get; set; }

        public List<string> FilesContained { get; set; }

        public List<int> Dependencies { get; set; }

        public Package(int package_id, string display_name, int category, int era, int country, int owner, string date_time, string target_path, List<string> deps_contained, string file_name = "", string description = "", int version = 1, bool isPaid = false, int steamappid = -1, List<int> dependencies = null)
        {
            PackageId = package_id;
            FileName = file_name;
            DisplayName = display_name;
            Category = category;
            Era = era;
            Country = country;
            Version = version;
            Owner = owner;
            Datetime = Convert.ToDateTime(date_time);
            Description = description;
            TargetPath = target_path;
            IsPaid = isPaid;
            SteamAppID = steamappid;
            FilesContained = deps_contained;
            Dependencies = dependencies ?? new List<int>();
        }

        public Package(QueryContent packageJson)
        {
            PackageId = packageJson.id;
            FileName = packageJson.file_name;
            DisplayName = packageJson.display_name;
            Category = packageJson.category;
            Era = packageJson.era;
            Country = packageJson.country;
            Version = packageJson.version;
            Owner = packageJson.owner;
            Datetime = Convert.ToDateTime(packageJson.created);
            Description = packageJson.description;
            TargetPath = packageJson.target_path;
            IsPaid = packageJson.paid;
            SteamAppID = packageJson.steamappid ?? 0;
            FilesContained = new List<string>();
            if (packageJson.files != null)
                FilesContained = packageJson.files.Select(x => Utils.NormalizePath(x)).Distinct().ToList();
            if (packageJson.dependencies != null)
                Dependencies = packageJson.dependencies.ToList();
            else
                Dependencies = new List<int>();
        }
    }

    public class PackageManager
    {
        public List<Package> InstalledPackages { get; set; }

        public List<Package> CachedPackages { get; set; } = new List<Package>();

        internal SqLiteAdapter SqLiteAdapter { get; set; }

        public HashSet<string> DownloadableDeps { get; set; } = new HashSet<string>();
        public Dictionary<string, int> DownloadableDepsPackages { get; set; } = new Dictionary<string, int>();

        public HashSet<string> DownloadablePaidDeps { get; set; } = new HashSet<string>();
        public Dictionary<string, int> DownloadablePaidDepsPackages { get; set; } = new Dictionary<string, int>();

        public EventWaitHandle CacheInit = new EventWaitHandle(false, EventResetMode.ManualReset);

        internal HashSet<int> PkgsToDownload { get; set; } = new HashSet<int>();

        private Dictionary<int, int> ServerVersions { get; set; } = new Dictionary<int, int>();

        private Uri ApiUrl { get; set; }

        private WebWrapper WebWrapper { get; set; }

        private MainWindow MainWindow { get; set; }

        private bool MSMQRunning { get; set; } = false;

        public bool StopMSMQ { get; set; } = false;

        public PackageManager(Uri apiUrl, MainWindow mw, string RWPath)
        {
            ApiUrl = apiUrl;
            MainWindow = mw;
            WebWrapper = new WebWrapper(ApiUrl);

            SqLiteAdapter = new SqLiteAdapter(Path.Combine(RWPath, "main.dls"), true);
            InstalledPackages = SqLiteAdapter.LoadPackages();
            Task.Run(async () =>
            {
                await VerifyCache();
                //CachedPackages = CachedPackages.Union(InstalledPackages).ToList();
                CacheInit.Set();
            });
        }

        public async Task GetDependencies(HashSet<int> dependecies, HashSet<int> returnDependencies)
        {
            foreach (int depPackageId in dependecies)
            {
                if (!returnDependencies.Contains(depPackageId) && !InstalledPackages.Any(x => x.PackageId == depPackageId))
                {
                    Package dependencyPackage = CachedPackages.FirstOrDefault(x => x.PackageId == depPackageId);
                    if (dependencyPackage == default)
                    {
                        dependencyPackage = await WebWrapper.GetPackage(depPackageId);

                        lock (CachedPackages)
                        {
                            CachedPackages.Add(dependencyPackage);
                        }
                    }

                    if (!dependencyPackage.IsPaid)
                        returnDependencies.Add(depPackageId);

                    await GetDependencies(dependencyPackage.Dependencies.ToHashSet(), returnDependencies);
                }
            }
        }

        public async Task VerifyCache()
        {
            List<Package> localCache = SqLiteAdapter.LoadPackages(true);
            ServerVersions = new Dictionary<int, int>();
            localCache.ForEach(x => ServerVersions[x.PackageId] = x.Version);
            Tuple<IEnumerable<Package>, HashSet<int>> tRemoteCache = await WebWrapper.ValidateCache(ServerVersions);
            IEnumerable<Package> remoteCache = tRemoteCache.Item1;
            HashSet<int> remoteVersions = tRemoteCache.Item2;
            lock (CachedPackages)
            {
                CachedPackages = remoteCache.ToList();
                localCache.ForEach(x =>
                {
                    if (!remoteVersions.Contains(x.PackageId))
                    {
                        CachedPackages.Add(x);
                    }
                    else
                    {
                        ServerVersions[x.PackageId] = CachedPackages.First(y => y.PackageId == x.PackageId).Version;
                    }
                });
            }
            if (remoteVersions.Count > 0)
            {
                new Task(() =>
                {
                    foreach (Package pkg in CachedPackages)
                    {
                        SqLiteAdapter.SavePackage(pkg, true);
                    }
                    SqLiteAdapter.FlushToFile(true);
                }).Start();
            }
            CachedPackages.ForEach(x =>
            {
                if (x.IsPaid)
                {
                    x.FilesContained.ForEach(y =>
                    {
                        DownloadablePaidDepsPackages[y] = x.PackageId;
                        DownloadablePaidDeps.Add(y);
                    });
                }
                else
                {
                    x.FilesContained.ForEach(y =>
                    {
                        DownloadableDepsPackages[y] = x.PackageId;
                        DownloadableDeps.Add(y);
                    });
                }
            });
        }

        public async Task ResolveConflicts(MainWindow mw)
        {
            HashSet<string> conflictDeps = mw.RW.AllInstalledDeps.Intersect(DownloadableDeps).Except(InstalledPackages.SelectMany(x => x.FilesContained)).ToHashSet();

            HashSet<int> conflictPackages = new HashSet<int>();

            while (conflictDeps.Count > 0)
            {
                Package pkg = CachedPackages.First(x => x.FilesContained.Contains(conflictDeps.First()));
                conflictPackages.Add(pkg.PackageId);
                conflictDeps.ExceptWith(pkg.FilesContained);
            }

            //HashSet<int> conflictPackages = conflictPkgs.Select(x => x.PackageId).ToHashSet();

            bool rewriteAll = false;
            bool keepAll = false;

            for (int i = 0; i < conflictPackages.Count; i++)
            {
                int id = conflictPackages.ElementAt(i);

                if (Settings.Default.IgnoredPackages?.Contains(id) == true)
                    continue;

                Package p = CachedPackages.FirstOrDefault(x => x.PackageId == id);

                bool rewrite = false;
                if (!rewriteAll && !keepAll)
                {
                    Task<ContentDialogResult> t = null;
                    mw.Dispatcher.Invoke(() =>
                    {
                        MainWindow.ContentDialog = new ConflictPackageDialog(p.DisplayName);
                        t = MainWindow.ContentDialog.ShowAsync();
                    });

                    ContentDialogResult result = await t;

                    ConflictPackageDialog dlg = (ConflictPackageDialog)MainWindow.ContentDialog;

                    rewrite = dlg.RewriteLocal;
                    rewriteAll = dlg.RewriteAll;
                    keepAll = dlg.KeepAll;
                }

                if (rewrite || rewriteAll)
                {
                    PkgsToDownload.Add(id);
                    HashSet<int> depsPkgs = new HashSet<int>();
                    await GetDependencies(new HashSet<int>() { id }, depsPkgs);
                    PkgsToDownload.UnionWith(depsPkgs);
                }
                else
                {
                    if (Settings.Default.IgnoredPackages == null)
                        Settings.Default.IgnoredPackages = new List<int>();

                    Settings.Default.IgnoredPackages.Add(id);
                    Settings.Default.Save();
                }
            }
        }

        public void DownloadDependencies()
        {
            Task.Run(async () =>
            {
                if (!await Utils.CheckLogin(DownloadDependencies, MainWindow, ApiUrl) || App.IsDownloading)
                {
                    App.Window.Dispatcher.Invoke(() =>
                    {
                        MainWindow.ScanRailworks.IsEnabled = true;
                        MainWindow.SelectRailworksLocation.IsEnabled = true;
                        MainWindow.DownloadMissing.IsEnabled = true;
                    });
                    return;
                }

                if (PkgsToDownload.Count > 0)
                {
                    App.IsDownloading = true;
                    MainWindow.Dispatcher.Invoke(() => { MainWindow.DownloadDialog.ShowAsync(); });
                    MainWindow.DownloadDialog.DownloadPackages(PkgsToDownload, CachedPackages, InstalledPackages, WebWrapper, SqLiteAdapter).Wait();
                    App.IsDownloading = false;
                    MainWindow.RW_CrawlingComplete();
                }
                else
                {
                    new Task(() =>
                    {
                        App.Window.Dispatcher.Invoke(() =>
                        {
                            MainWindow.ErrorDialog = new ContentDialog()
                            {
                                Title = Localization.Strings.CantDownload,
                                Content = Localization.Strings.AllDownDesc,
                                SecondaryButtonText = Localization.Strings.Ok,
                                Owner = App.Window
                            };

                            MainWindow.ErrorDialog.ShowAsync();
                        });

                        MainWindow.Dispatcher.Invoke(() =>
                        {
                            MainWindow.ScanRailworks.IsEnabled = true;
                            MainWindow.SelectRailworksLocation.IsEnabled = true;
                        });
                    }).Start();
                }
            });
        }

        public void CheckUpdates()
        {
            Task.Run(async () =>
            {
                Dictionary<int, int> pkgsToUpdate = new Dictionary<int, int>();

                foreach (Package package in InstalledPackages)
                {
                    if (package.Version < ServerVersions[package.PackageId])
                    {
                        Task<ContentDialogResult> t = null;
                        MainWindow.Dispatcher.Invoke(() =>
                        {
                            MainWindow.ContentDialog.Title = Localization.Strings.NewerTitle;
                            MainWindow.ContentDialog.Content = string.Format(Localization.Strings.NewerDesc, package.DisplayName);
                            MainWindow.ContentDialog.PrimaryButtonText = Localization.Strings.NewerPrimary;
                            MainWindow.ContentDialog.SecondaryButtonText = Localization.Strings.NewerSecond;
                            MainWindow.ContentDialog.Owner = MainWindow;
                            t = MainWindow.ContentDialog.ShowAsync();
                        });

                        ContentDialogResult result = await t;
                        if (result == ContentDialogResult.Primary)
                        {
                            pkgsToUpdate[package.PackageId] = ServerVersions[package.PackageId];
                        }
                    }
                }

                if (!await Utils.CheckLogin(CheckUpdates, MainWindow, ApiUrl) || pkgsToUpdate.Count == 0 || App.IsDownloading)
                    return;

                App.IsDownloading = true;
                MainWindow.Dispatcher.Invoke(() => { MainWindow.DownloadDialog.ShowAsync(); });
                MainWindow.DownloadDialog.UpdatePackages(pkgsToUpdate, InstalledPackages, WebWrapper, SqLiteAdapter).Wait();
                App.IsDownloading = false;
                MainWindow.RW_CrawlingComplete();
            });
        }

        public void RunQueueWatcher()
        {
            new Task(async () =>
            {
                MSMQRunning = true;
                await ReceiveMSMQ();
                MSMQRunning = false;
            }).Start(); ;
            using (FileSystemWatcher watcher = new FileSystemWatcher())
            {
                watcher.Path = Path.GetTempPath();

                watcher.Filter = "DLS.queue";

                watcher.NotifyFilter = NotifyFilters.LastWrite;

                watcher.Changed += OnChanged;
                watcher.EnableRaisingEvents = true;

                while (!StopMSMQ) ;
            }
        }

        public void RemovePackage(int pkgId)
        {
            InstalledPackages.RemoveAll(x => x.PackageId == pkgId);
            List<string> removedFiles = Utils.RemoveFiles(SqLiteAdapter.LoadPackageFiles(pkgId));
            SqLiteAdapter.RemovePackageFiles(removedFiles);
            SqLiteAdapter.RemoveInstalledPackage(pkgId);
            SqLiteAdapter.FlushToFile(true);
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            System.Threading.Thread.Sleep(500);
            if (MSMQRunning)
                return;

            new Task(async () =>
            {
                MSMQRunning = true;
                await ReceiveMSMQ();
                MSMQRunning = false;
            }).Start();
        }

        public async Task ReceiveMSMQ()
        {
            string queueFile = Path.Combine(Path.GetTempPath(), "DLS.queue");
            HashSet<string> queuedPkgs = File.Exists(queueFile) ? File.ReadAllText(queueFile).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet() : new HashSet<string>();

            if (queuedPkgs.Count > 0)
            {
                MainWindow.Dispatcher.Invoke(() => { MainWindow.Activate(); });
                if (!await Utils.CheckLogin(async delegate
                {
                    await ReceiveMSMQ();
                    MSMQRunning = false;
                }, MainWindow, ApiUrl) || App.IsDownloading)
                {
                    //File.WriteAllText(queueFile, string.Empty);
                    return;
                }

                int idToDownload = Convert.ToInt32(queuedPkgs.PopOne());

                if (!InstalledPackages.Exists(x => x.PackageId == idToDownload))
                {
                    Task.Run(async () =>
                    {
                        Package packageToDownload = await WebWrapper.GetPackage(idToDownload);
                        lock (CachedPackages)
                        {
                            if (!CachedPackages.Any(x => x.PackageId == packageToDownload.PackageId))
                                CachedPackages.Add(packageToDownload);
                        }

                        if (packageToDownload.IsPaid)
                        {
                            App.Window.Dispatcher.Invoke(() =>
                            {
                                MainWindow.ErrorDialog = new ContentDialog()
                                {
                                    Title = Localization.Strings.CantDownload,
                                    Content = Localization.Strings.PaidPackageFail,
                                    SecondaryButtonText = Localization.Strings.Ok,
                                    Owner = App.Window
                                };

                                MainWindow.ErrorDialog.ShowAsync();
                            });

                            return;
                        }

                        HashSet<int> depsPkgs = new HashSet<int>();
                        await GetDependencies(new HashSet<int>() { packageToDownload.PackageId }, depsPkgs);
                        HashSet<int> packageIds = new HashSet<int>() { packageToDownload.PackageId }.Union(depsPkgs).ToHashSet();

                        if (packageIds.Count > 0)
                        {
                            App.IsDownloading = true;
                            MainWindow.Dispatcher.Invoke(() => { MainWindow.DownloadDialog.ShowAsync(); });
                            await MainWindow.DownloadDialog.DownloadPackages(packageIds, CachedPackages, InstalledPackages, WebWrapper, SqLiteAdapter);
                            App.IsDownloading = false;
                        }
                        else
                        {
                            new Task(() =>
                            {
                                App.Window.Dispatcher.Invoke(() =>
                                {
                                    MainWindow.ErrorDialog = new ContentDialog()
                                    {
                                        Title = Localization.Strings.CantDownload,
                                        Content = Localization.Strings.InstalFail,
                                        SecondaryButtonText = Localization.Strings.Ok,
                                        Owner = App.Window
                                    };

                                    MainWindow.ErrorDialog.ShowAsync();
                                });
                            }).Start();
                        }
                    }).Wait();
                }
                else
                {
                    App.Window.Dispatcher.Invoke(() =>
                    {
                        MainWindow.ErrorDialog = new ContentDialog()
                        {
                            Title = Localization.Strings.CantDownload,
                            Content = Localization.Strings.AlreadyInstalFail,
                            SecondaryButtonText = Localization.Strings.Ok,
                            Owner = App.Window
                        };

                        MainWindow.ErrorDialog.ShowAsync();
                    });

                }

                File.WriteAllText(queueFile, string.Join(",", queuedPkgs));
                await ReceiveMSMQ();
            }
        }
    }
}
