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
            SteamAppID = packageJson.steamappid;
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
        private readonly SqLiteAdapter SqLiteAdapter = new SqLiteAdapter(Path.GetFullPath("packages.mcf"));

        private HashSet<string> DownloadableDeps { get; set; }

        public List<Package> InstalledPackages { get; set; }

        public HashSet<Package> CachedPackages = new HashSet<Package>();
        public HashSet<string> MissingDeps { get; set; }

        private readonly object CachedLock = new object();

        private string RWPath { get; set; }

        private string AssetsPath { get; set; }

        private Uri ApiUrl { get; set; }

        private WebWrapper WebWrapper { get; set; }

        private MainWindow MainWindow { get; set; }

        public PackageManager(string rwPath, Uri apiUrl, MainWindow mw)
        {
            RWPath = rwPath;
            ApiUrl = apiUrl;
            AssetsPath = Path.Combine(RWPath, "Assets");
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

            package = CachedPackages.FirstOrDefault(x => x.FilesContained.Contains(file_name));
            if (package != default)
                return package.PackageId;

            Package onlinePackage = await WebWrapper.SearchForFile(file_name);
            if (onlinePackage != null && onlinePackage.PackageId > 0)
            {
                lock (CachedLock)
                {
                    CachedPackages.Add(onlinePackage);
                }
                return onlinePackage.PackageId;
            }

            return -1;
        }

        public async Task<HashSet<string>> GetDownloadableDependencies(HashSet<string> globalDependencies)
        {
            DownloadableDeps = (await WebWrapper.QueryArray("listFiles")).Intersect(globalDependencies).ToHashSet();
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
                string token = loginContent.token;

                HashSet<int> pkgsToDownload = new HashSet<int>();

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
                                lock (pkgsToDownload)
                                {
                                    pkgsToDownload.Add(pkgId);
                                }
                            }
                        }).Wait();
                    }
                });

                for (int i = 0; i < pkgsToDownload.Count; i++)
                {
                    Task.Run(async () =>
                    {
                        int pkgId = pkgsToDownload.ElementAt(i);
                        ObjectResult<JObject> dl_result = await WebWrapper.DownloadPackage(pkgId, token);

                        if (dl_result.code == 1)
                        {
                            ZipFile.ExtractToDirectory(dl_result.message, Path.Combine(AssetsPath, CachedPackages.Where(x => x.PackageId == pkgId).Select(x => x.TargetPath).Single()));
                        }
                    }).Wait();
                }
            });

        }
    }

}
