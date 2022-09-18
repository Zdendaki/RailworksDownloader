using Microsoft.AppCenter.Crashes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public Dictionary<string, Dictionary<string, Dictionary<string, int>>> DownloadableDepsPackages { get; set; } = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();

        public HashSet<string> DownloadablePaidDeps { get; set; } = new HashSet<string>();

        public Dictionary<string, Dictionary<string, Dictionary<string, int>>> DownloadablePaidDepsPackages { get; set; } = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();

        public EventWaitHandle CacheInit = new EventWaitHandle(false, EventResetMode.ManualReset);

        public FileSystemWatcher msmqWatcher { get; set; }

        internal HashSet<int> PkgsToDownload { get; set; } = new HashSet<int>();

        private Dictionary<int, int> ServerVersions { get; set; } = new Dictionary<int, int>();

        private Uri ApiUrl { get; set; }

        private WebWrapper WebWrapper { get; set; }

        private MainWindow MainWindow { get; set; }

        private bool MSMQRunning { get; set; } = false;

        public PackageManager(Uri apiUrl, MainWindow mw, string RWPath)
        {
            ApiUrl = apiUrl;
            MainWindow = mw;
            WebWrapper = new WebWrapper(ApiUrl);

            SqLiteAdapter = new SqLiteAdapter(Path.Combine(RWPath, "main.dls"), true);
            InstalledPackages = SqLiteAdapter.LoadPackages();
            Task.Run(async () =>
            {
                try
                {
                    await VerifyCache();
                }
                catch (Exception e)
                {
                    Crashes.TrackError(e, new Dictionary<string, string>() { { "Type", "Exception" } });
                    Trace.Assert(false, Localization.Strings.VerifyCacheFailed);
                }
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
                foreach (Package pkg in remoteCache)
                {
                    ServerVersions[pkg.PackageId] = pkg.Version;
                }
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
                HashSet<string> filesList = x.IsPaid ? DownloadablePaidDeps : DownloadableDeps;
                Dictionary<string, Dictionary<string, Dictionary<string, int>>> packagesList = x.IsPaid ? DownloadablePaidDepsPackages : DownloadableDepsPackages;

                x.FilesContained.ForEach(y =>
                {
                    string[] parts = y.Split(new string[] { "\\" }, 3, StringSplitOptions.None);
                    if (parts.Length == 3)
                    {
                        string provider = parts[0];
                        string product = parts[1];
                        string file = parts[2];

                        if (!packagesList.ContainsKey(provider))
                            packagesList[provider] = new Dictionary<string, Dictionary<string, int>>();

                        if (!packagesList[provider].ContainsKey(product))
                            packagesList[provider][product] = new Dictionary<string, int>();

                        packagesList[provider][product][file] = x.PackageId;
                        filesList.Add(y);
                    }
                });
            });
        }

        public async Task ResolveConflicts()
        {
            HashSet<string> conflictDeps = MainWindow.RW.AllInstalledDeps.Intersect(DownloadableDeps).Except(InstalledPackages.SelectMany(x => x.FilesContained)).ToHashSet();

            HashSet<int> conflictPackages = new HashSet<int>();

            while (conflictDeps.Count > 0)
            {
                Package pkg = CachedPackages.First(x => x.FilesContained.Contains(conflictDeps.First()));
                conflictPackages.Add(pkg.PackageId);
                conflictDeps.ExceptWith(pkg.FilesContained);
            }

            bool keepAll = false;
            bool rewriteAll = false;
            EventWaitHandle dialogConfirm = new EventWaitHandle(false, EventResetMode.AutoReset);

            for (int i = 0; i < conflictPackages.Count; i++)
            {
                int id = conflictPackages.ElementAt(i);

                if (App.Settings.IgnoredPackages?.Contains(id) == true)
                    continue;

                bool rewrite = false;
                if (!rewriteAll && !keepAll)
                {
                    Package p = CachedPackages.FirstOrDefault(x => x.PackageId == id);

                    MainWindow.Dispatcher.Invoke(() =>
                    {
                        ConflictPackageDialog conflictPackageDialog = new ConflictPackageDialog(p.DisplayName);
                        App.DialogQueue.AddDialog(Environment.TickCount, 2, conflictPackageDialog, (_) =>
                        {
                            rewrite = conflictPackageDialog?.RewriteLocal ?? false;
                            rewriteAll = conflictPackageDialog?.RewriteAll ?? false;
                            keepAll = conflictPackageDialog?.KeepAll ?? false;
                            dialogConfirm.Set();
                        });
                    });
                    dialogConfirm.WaitOne();
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
                    App.Settings.IgnoredPackages.Add(id);
                    App.Settings.Save();
                }
            }
        }

        public void GetPackagesToDownload(IEnumerable<string> allMissing)
        {
            IEnumerable<string> depsToDownload = allMissing.Intersect(DownloadableDeps);
            while (depsToDownload.Count() > 0)
            {
                string[] parts = depsToDownload.First().Split(new string[] { "\\" }, 3, StringSplitOptions.None);
                string provider = parts[0];
                string product = parts[1];
                string file = parts[2];

                Package pkg = CachedPackages.First(x => x.PackageId == DownloadableDepsPackages[provider][product][file]);
                PkgsToDownload.Add(pkg.PackageId);
                depsToDownload = depsToDownload.Except(pkg.FilesContained);
            }
        }

        public void DownloadDependencies()
        {
            if (App.IsDownloading)
                return;

            Task.Run(async () =>
            {
                if (!await Utils.CheckLogin(DownloadDependencies, MainWindow, ApiUrl))
                {
                    MainWindow.Dispatcher.Invoke(() =>
                    {
                        MainWindow.ScanRailworks.IsEnabled = true;
                        MainWindow.SelectRailworksLocation.IsEnabled = true;
                        MainWindow.DownloadMissing.IsEnabled = true;
                    });
                    return;
                }

                PkgsToDownload.RemoveWhere(x => CachedPackages.First(y => y.PackageId == x).IsPaid);

                if (PkgsToDownload.Count > 0)
                {
                    App.IsDownloading = true;
                    MainWindow.Dispatcher.Invoke(() =>
                    {
                        DownloadDialog downloadDialog = new DownloadDialog();
                        App.DialogQueue.AddDialog(Environment.TickCount, 1, downloadDialog);
                        Task.Run(() =>
                        {
                            downloadDialog.DownloadPackages(PkgsToDownload, CachedPackages, InstalledPackages, WebWrapper, SqLiteAdapter);
                            MainWindow.Dispatcher.Invoke(() =>
                            {
                                App.IsDownloading = false;
                            });
                            MainWindow.RW_CrawlingComplete();
                        });
                    });
                }
                else
                {
                    Utils.DisplayError(Localization.Strings.CantDownload, Localization.Strings.AllDownDesc);
                    MainWindow.Dispatcher.Invoke(() =>
                    {
                        MainWindow.DownloadMissing.IsEnabled = false;
                    });
                }

                MainWindow.Dispatcher.Invoke(() =>
                {
                    MainWindow.ScanRailworks.IsEnabled = true;
                    MainWindow.SelectRailworksLocation.IsEnabled = true;
                    MainWindow.DownloadMissing.IsEnabled = true;
                });
            });
        }

        public void CheckUpdates()
        {
            Task.Run(async () =>
            {
                Dictionary<int, int> pkgsToUpdate = new Dictionary<int, int>();

                foreach (Package package in InstalledPackages)
                {
                    if (package.IsPaid || !ServerVersions.ContainsKey(package.PackageId))
                        continue;

                    if (package.Version < ServerVersions[package.PackageId])
                    {
                        Utils.DisplayYesNo(Localization.Strings.NewerTitle, string.Format(Localization.Strings.NewerDesc, package.DisplayName), Localization.Strings.NewerPrimary, Localization.Strings.NewerSecond, (res) =>
                        {
                            if (res)
                                pkgsToUpdate[package.PackageId] = ServerVersions[package.PackageId];
                        });
                    }
                }

                if (!await Utils.CheckLogin(CheckUpdates, MainWindow, ApiUrl) || pkgsToUpdate.Count == 0 || App.IsDownloading)
                    return;

                App.IsDownloading = true;
                MainWindow.Dispatcher.Invoke(() =>
                {
                    DownloadDialog downloadDialog = new DownloadDialog();
                    App.DialogQueue.AddDialog(Environment.TickCount, 1, downloadDialog);
                    Task.Run(() =>
                    {
                        downloadDialog.UpdatePackages(pkgsToUpdate, InstalledPackages, WebWrapper, SqLiteAdapter);
                        MainWindow.Dispatcher.Invoke(() =>
                        {
                            App.IsDownloading = false;
                        });
                        MainWindow.RW_CrawlingComplete();
                    });
                });
            });
        }

        public FileSystemWatcher RunQueueWatcher()
        {
            new Task(async () =>
            {
                MSMQRunning = true;
                await ReceiveMSMQ();
                MSMQRunning = false;
            }).Start();

            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = Path.GetTempPath(),

                Filter = "DLS.queue",

                NotifyFilter = NotifyFilters.LastWrite
            };

            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.EnableRaisingEvents = true;

            return watcher;
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
            System.Threading.Thread.Sleep(50);
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

                if (int.TryParse(queuedPkgs.PopOne(), out int idToDownload))
                {
                    if (!InstalledPackages.Exists(x => x.PackageId == idToDownload))
                    {
                        Task.Run(async () =>
                        {
                            Package packageToDownload = await WebWrapper.GetPackage(idToDownload);
                            if (packageToDownload == null)
                            {
                                Utils.DisplayError(Localization.Strings.CantDownload, Localization.Strings.NoSuchPackageFail);
                                return;
                            }
                            lock (CachedPackages)
                            {
                                if (!CachedPackages.Any(x => x.PackageId == packageToDownload.PackageId))
                                    CachedPackages.Add(packageToDownload);
                            }

                            if (packageToDownload.IsPaid)
                            {
                                Utils.DisplayError(Localization.Strings.CantDownload, Localization.Strings.PaidPackageFail);
                                return;
                            }

                            HashSet<int> depsPkgs = new HashSet<int>();
                            await GetDependencies(new HashSet<int>() { packageToDownload.PackageId }, depsPkgs);
                            HashSet<int> packageIds = new HashSet<int>() { packageToDownload.PackageId }.Union(depsPkgs).ToHashSet();

                            if (packageIds.Count > 0)
                            {
                                MainWindow.Dispatcher.Invoke(() =>
                                {
                                    App.IsDownloading = true;
                                    DownloadDialog downloadDialog = new DownloadDialog();
                                    App.DialogQueue.AddDialog(Environment.TickCount, 1, downloadDialog);
                                    Task.Run(() =>
                                    {
                                        downloadDialog.DownloadPackages(packageIds, CachedPackages, InstalledPackages, WebWrapper, SqLiteAdapter);
                                        MainWindow.Dispatcher.Invoke(() =>
                                        {
                                            App.IsDownloading = false;
                                        });
                                    });
                                });
                            }
                            else
                            {
                                Utils.DisplayError(Localization.Strings.CantDownload, Localization.Strings.InstalFail);
                            }
                        }).Wait();
                    }
                    else
                    {
                        Utils.DisplayError(Localization.Strings.CantDownload, Localization.Strings.AlreadyInstalFail);

                    }
                }

                File.WriteAllText(queueFile, string.Join(",", queuedPkgs));
                await ReceiveMSMQ();
            }
        }
    }
}
