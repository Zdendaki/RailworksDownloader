using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Environment;
using SteamKit2;
using Microsoft.Win32;
using System.Web.UI;
using System.IO.Compression;

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
        private SqLiteAdapter SqLiteAdapter { get; set; }

        public List<Package> InstalledPackages { get; set; }

        public HashSet<Package> CachedPackages { get; set; }

        public HashSet<string> DownloadableDependencies { get; set; }

        private object CachedLock = new object();

        private object DownloadableLock = new object();

        private string RWPath { get; set; }

        private string AssetsPath { get; set; }

        private static Uri ApiUrl { get; set; }

        public PackageManager(string rwPath, Uri apiUrl)
        {
            RWPath = rwPath;
            ApiUrl = apiUrl;
            AssetsPath = Path.Combine(RWPath, "Assets");

            //string commonpath = GetFolderPath(SpecialFolder.CommonApplicationData);
            //SqLiteAdapter = new SqLiteAdapter(Path.Combine(commonpath, "DLS", "packages.mcf"));
            SqLiteAdapter = new SqLiteAdapter(Path.GetFullPath("packages.mcf"));

            InstalledPackages = SqLiteAdapter.LoadInstalledPackages();
            CachedPackages = new HashSet<Package>();
            DownloadableDependencies = new HashSet<string>();
        }

        public async Task<int> FindFile(string file_name)
        {
            Package package = CachedPackages.Where(x => x.FilesContained.Contains(file_name)).First();
            if (package != null)
                return package.PackageId;

            package = CachedPackages.Where(x => x.FilesContained.Contains(file_name)).First();
            if (package != null)
                return package.PackageId;

            WebWrapper ww = new WebWrapper(ApiUrl);
            Package onlinePackage = await ww.SearchForFile(file_name);
            if (onlinePackage != null && onlinePackage.PackageId > 0)
            {
                lock (CachedLock)
                {
                    CachedPackages.Add(onlinePackage);
                }
                /*lock (DownloadableLock)
                    DownloadableDependencies.UnionWith(onlinePackage.DepsContained);*/
                return onlinePackage.PackageId;
            }

            return -1;
        }

        public async Task GetDownloadableDependencies()
        {
            WebWrapper ww = new WebWrapper(ApiUrl);
            HashSet<string> downloadableDeps = await ww.GetAllFiles();
            lock (DownloadableLock)
                DownloadableDependencies = downloadableDeps;

            /*if (DownloadableDependencies.Contains(path) || DownloadableDependencies.Contains(path_bin) || await FindFile(path))
                continue;
            }*/
        }

        public async Task GetSteamDependencies()
        {
        }
    }

}
