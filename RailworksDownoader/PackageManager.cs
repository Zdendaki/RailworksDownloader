using ModernWpf.Controls;
using Newtonsoft.Json.Linq;
using RailworksDownloader.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
        public int Era { get; set; }
        public int Country { get; set; }
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

        public HashSet<Package> CachedPackages { get; set; } = new HashSet<Package>();
        public HashSet<string> MissingDeps { get; set; }

        private readonly SqLiteAdapter SqLiteAdapter = new SqLiteAdapter(Path.GetFullPath("packages.mcf"));

        private HashSet<string> DownloadableDeps { get; set; }

        private HashSet<int> PkgsToDownload { get; set;} = new HashSet<int>();

        private Uri ApiUrl { get; set; }

        private WebWrapper WebWrapper { get; set; }

        private MainWindow MainWindow { get; set; }

        public PackageManager(Uri apiUrl, MainWindow mw)
        {
            ApiUrl = apiUrl;
            MainWindow = mw;

            //string commonpath = GetFolderPath(SpecialFolder.CommonApplicationData);
            //SqLiteAdapter = new SqLiteAdapter(Path.Combine(commonpath, "DLS", "packages.mcf"));

            InstalledPackages = SqLiteAdapter.LoadInstalledPackages();
            WebWrapper = new WebWrapper(ApiUrl);
        }

        public async Task<int> FindFile(string file_name)
        {
            Package package = InstalledPackages.FirstOrDefault(x => x.FilesContained.Contains(file_name));

            if (package != default)
                return package.PackageId;

            lock (CachedPackages)
                package = CachedPackages.FirstOrDefault(x => x.FilesContained.Contains(file_name));

            if (package != default)
                return package.PackageId;

            Package onlinePackage = await WebWrapper.SearchForFile(file_name);
            if (onlinePackage != null && onlinePackage.PackageId > 0)
            {
                lock (CachedPackages)
                {
                    CachedPackages.Add(onlinePackage);
                }
                return onlinePackage.PackageId;
            }

            return -1;
        }

        public async Task<HashSet<string>> GetDownloadableDependencies(HashSet<string> globalDependencies, HashSet<string> existing, MainWindow mw)
        {
            HashSet<string> allDownloadableDeps = await WebWrapper.QueryArray("listFiles");

            HashSet<string> conflictDeps = existing.Intersect(allDownloadableDeps).Except(InstalledPackages.SelectMany(x => x.FilesContained)).ToHashSet();

            HashSet<int> conflictPackages = new HashSet<int>();

            int maxThreads = Math.Min(Environment.ProcessorCount, conflictDeps.Count);
            Parallel.For(0, maxThreads, workerId =>
            {
                Task.Run(async () =>
                {
                    int max = conflictDeps.Count * (workerId + 1) / maxThreads;
                    for (int i = conflictDeps.Count * workerId / maxThreads; i < max; i++)
                    {
                        int id = await FindFile(conflictDeps.ElementAt(i));
                        if (conflictPackages.Contains(id))
                            continue;

                        conflictPackages.Add(id);
                    }
                }).Wait();
            });

            for (int i = 0; i < conflictPackages.Count; i++)
            {
                int id = conflictPackages.ElementAt(i);

                Task<ContentDialogResult> t = null;
                mw.Dispatcher.Invoke(() =>
                {
                    MainWindow.ContentDialog.Title = "Conflict file found!";
                    MainWindow.ContentDialog.Content = string.Format("Following package seems to be controlled by DLS but not installed through this app:\n{0}\nPlease decide how to continue!", CachedPackages.FirstOrDefault(x => x.PackageId == id).DisplayName);
                    MainWindow.ContentDialog.PrimaryButtonText = "Overwrite local";
                    MainWindow.ContentDialog.SecondaryButtonText = "Keep local";
                    MainWindow.ContentDialog.Owner = mw;
                    t = MainWindow.ContentDialog.ShowAsync();
                });

                ContentDialogResult result = await t;
                if (result == ContentDialogResult.Primary)
                {
                    PkgsToDownload.Add(id);
                }
            }

            DownloadableDeps = allDownloadableDeps.Intersect(globalDependencies).ToHashSet();
            return DownloadableDeps;
        }

        public async Task<HashSet<string>> GetPaidDependencies(HashSet<string> globalDependencies)
        {
            return (await WebWrapper.QueryArray("listPaid")).Intersect(globalDependencies).ToHashSet();
        }

        public void DownloadDependencies()
        {
            Task.Run(async () =>
            {
                await MainWindow.DownloadDialog.ShowAsync();

                if (App.Token != default)
                {
                    if (string.IsNullOrWhiteSpace(Settings.Default.Username) || string.IsNullOrWhiteSpace(Settings.Default.Password))
                    {
                        MainWindow.Dispatcher.Invoke(() => { LoginDialog ld = new LoginDialog(this, ApiUrl); });
                        return;
                    }

                    string login = Settings.Default.Username;
                    string passwd = Utils.PasswordEncryptor.Decrypt(Settings.Default.Password, login.Trim());

                    ObjectResult<LoginContent> result = await WebWrapper.Login(login, passwd, ApiUrl);

                    if (result == null || result.code != 1 || result.content == null || result.content.privileges < 0)
                    {
                        MainWindow.Dispatcher.Invoke(() => { LoginDialog ld = new LoginDialog(this, ApiUrl); });
                        return;
                    }

                    LoginContent loginContent = result.content;
                    App.Token = loginContent.token;
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
                            int pkgId = await FindFile(dependency);

                            if (pkgId >= 0)
                            {
                                lock (PkgsToDownload)
                                {
                                    PkgsToDownload.Add(pkgId);
                                }
                            }
                        }).Wait();
                    }
                });

                MainWindow.DownloadDialog.DownloadFile(PkgsToDownload, CachedPackages, InstalledPackages, WebWrapper, SqLiteAdapter).Wait();
            });

        }
    }

}
