using ModernWpf.Controls;
using Newtonsoft.Json;
using RailworksDownloader.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public Package(int package_id, string display_name, int category, int era, int country, int owner, string date_time, string target_path, List<string> deps_contained, string file_name = "", string description = "", int version = 1)
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
            IsPaid = false;
            SteamAppID = -1;
            FilesContained = deps_contained;
            Dependencies = new List<int>();
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
                FilesContained = packageJson.files.ToList();
            Dependencies = new List<int>();
            if (packageJson.dependencies != null)
                Dependencies = packageJson.dependencies.ToList();
        }
    }

    public class PackageManager
    {
        public List<Package> InstalledPackages { get; set; }

        public List<Package> CachedPackages { get; set; } = new List<Package>();

        public HashSet<string> MissingDeps { get; set; }

        private SqLiteAdapter SqLiteAdapter { get; set; }

        public HashSet<string> ServerDeps { get; set; } = new HashSet<string>();

        public HashSet<string> ServerPaidDeps { get; set; } = new HashSet<string>();

        private HashSet<string> DownloadableDeps { get; set; } = new HashSet<string>();

        internal HashSet<int> PkgsToDownload { get; set; } = new HashSet<int>();

        private Uri ApiUrl { get; set; }

        private WebWrapper WebWrapper { get; set; }

        private MainWindow MainWindow { get; set; }

        private bool MSMQRunning { get; set; } = false;

        public PackageManager(Uri apiUrl, MainWindow mw, string RWPath)
        {
            ApiUrl = apiUrl;
            MainWindow = mw;

            SqLiteAdapter = new SqLiteAdapter(Path.Combine(RWPath, "main.dls"), true);
            InstalledPackages = SqLiteAdapter.LoadInstalledPackages();
            CachedPackages = CachedPackages.Union(InstalledPackages).ToList();
            WebWrapper = new WebWrapper(ApiUrl);
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

        public async Task<List<int>> FindFile(string file_name, bool withDeps = true)
        {
            Package package = InstalledPackages.FirstOrDefault(x => x.FilesContained.Contains(file_name));

            if (package != default)
                return new List<int>() { package.PackageId };

            lock (CachedPackages)
                package = CachedPackages.FirstOrDefault(x => x.FilesContained.Contains(file_name));

            if (package != default)
                return new List<int>() { package.PackageId };

            if (!ServerDeps.Contains(file_name) && !ServerPaidDeps.Contains(file_name))
                return new List<int>();

            Package onlinePackage = await WebWrapper.SearchForFile(file_name);
            if (onlinePackage != null && onlinePackage.PackageId > 0)
            {
                lock (CachedPackages)
                {
                    if (!CachedPackages.Any(x => x.PackageId == onlinePackage.PackageId))
                        CachedPackages.Add(onlinePackage);
                }

                HashSet<int> dependencyPkgIds = new HashSet<int>();
                if (withDeps)
                {
                    await GetDependencies(new HashSet<int>() { onlinePackage.PackageId }, dependencyPkgIds);
                }

                return new List<int>() { onlinePackage.PackageId }.Union(dependencyPkgIds).ToList();
            }

            return new List<int>();
        }

        public async Task<HashSet<string>> GetDownloadableDependencies(HashSet<string> globalDependencies, HashSet<string> existing, MainWindow mw)
        {
            InstalledPackages = SqLiteAdapter.LoadInstalledPackages();
            ServerDeps = await WebWrapper.QueryArray("listFiles");

            if (ServerDeps == null)
                return DownloadableDeps;

            HashSet<string> conflictDeps = existing.Intersect(ServerDeps).Except(InstalledPackages.SelectMany(x => x.FilesContained)).ToHashSet();

            HashSet<int> conflictPackages = new HashSet<int>();

            int maxThreads = Math.Min(Environment.ProcessorCount, conflictDeps.Count);
            Parallel.For(0, maxThreads, workerId =>
            {
                Task.Run(async () =>
                {
                    int max = conflictDeps.Count * (workerId + 1) / maxThreads;
                    for (int i = conflictDeps.Count * workerId / maxThreads; i < max; i++)
                    {
                        List<int> packages = await FindFile(conflictDeps.ElementAt(i), false);

                        Trace.Assert(packages.Count > 0, string.Format(Localization.Strings.FindFileFail, conflictDeps.ElementAt(i)));

                        if (packages.Count > 0)
                        {
                            int id = packages.First();
                            lock (conflictPackages)
                            {
                                if (conflictPackages.Contains(id))
                                    continue;

                                conflictPackages.Add(id);
                            }
                        }
                    }
                }).Wait();
            });

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

            CheckUpdates();

            DownloadableDeps = ServerDeps.Intersect(globalDependencies).ToHashSet();
            return DownloadableDeps;
        }

        public async Task<HashSet<string>> GetPaidDependencies(HashSet<string> globalDependencies)
        {
            ServerPaidDeps = await WebWrapper.QueryArray("listPaid");
            if (ServerPaidDeps == null)
                return new HashSet<string>();
            return ServerPaidDeps.Intersect(globalDependencies).ToHashSet();
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

                int maxThreads = Math.Min(Environment.ProcessorCount, DownloadableDeps.Count);
                Parallel.For(0, maxThreads, workerId =>
                {
                    int max = DownloadableDeps.Count * (workerId + 1) / maxThreads;
                    for (int i = DownloadableDeps.Count * workerId / maxThreads; i < max; i++)
                    {
                        Task.Run(async () =>
                        {
                            string dependency = DownloadableDeps.ElementAt(i);
                            List<int> pkgId = await FindFile(dependency);

                            if (pkgId.Count >= 0)
                            {
                                lock (PkgsToDownload)
                                {
                                    PkgsToDownload.UnionWith(pkgId);
                                }
                            }
                        }).Wait();
                    }
                });

                if (PkgsToDownload.Count > 0)
                {
                    App.IsDownloading = true;
                    MainWindow.Dispatcher.Invoke(() => { MainWindow.DownloadDialog.ShowAsync(); }); // TODO: Check if works
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
                List<int> packagesId = InstalledPackages.Select(x => x.PackageId).ToList();
                Dictionary<int, int> serverVersions = await WebWrapper.GetVersions(packagesId);
                if (serverVersions.Count == 0)
                    return;

                foreach (Package package in InstalledPackages)
                {
                    if (serverVersions.ContainsKey(package.PackageId) && package.Version < serverVersions[package.PackageId])
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
                            pkgsToUpdate[package.PackageId] = serverVersions[package.PackageId];
                        }
                    }
                }

                if (!await Utils.CheckLogin(CheckUpdates, MainWindow, ApiUrl) || pkgsToUpdate.Count == 0 || App.IsDownloading)
                    return;

                App.IsDownloading = true;
                MainWindow.Dispatcher.Invoke(() => { MainWindow.DownloadDialog.ShowAsync(); }); // TODO: Check if works
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

                while (true) ;
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
                if (!await Utils.CheckLogin(async delegate {
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
