using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace RailworksDownloader
{
    class Railworks
    {
        public string RWPath { get; set; }

        public List<RouteInfo> Routes { get; set; }

        public HashSet<string> AllDependencies { get; set; }
        public HashSet<string> MissingDependencies { get; set; }

        int Total = 0;
        float Elapsed = 0f;
        int Completed = 0;
        object PercentLock = new object();
        object CompleteLock = new object();
        object SavingLock = new object();
        int Saving = 0;

        public delegate void ProgressUpdatedEventHandler(int percent);
        public event ProgressUpdatedEventHandler ProgressUpdated;

        public delegate void RouteSavingEventHandler(bool saved);
        public event RouteSavingEventHandler RouteSaving;

        public Railworks() : this (null) { }

        public Railworks(string path)
        {
            RWPath = string.IsNullOrWhiteSpace(path) ? GetRWPath() : path;
            AllDependencies = new HashSet<string>();
            Routes = new List<RouteInfo>();
        }

        public void InitRoutes()
        {
            Routes = GetRoutes().ToList();
        }
        
        public static string GetRWPath()
        {
            string path = (string)Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\RailSimulator.com\RailWorks", false)?.GetValue("install_path");

            if (path != null)
                return path;
            else
                return (string)Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 24010", false)?.GetValue("InstallLocation");
        }

        private string ParseDisplayNameNode(XmlNode displayNameNode) 
        {
            foreach (XmlNode n in displayNameNode.FirstChild)
            {
                if (!string.IsNullOrEmpty(n.InnerText))
                    return n.InnerText;
            }

            return null;
        }

        private string ParseRouteProperties(string path)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            
            return ParseDisplayNameNode(doc.DocumentElement.SelectSingleNode("DisplayName"));
        }

        /// <summary>
        /// Get list of routes
        /// </summary>
        /// <param name="path">Routes path</param>
        /// <returns></returns>
        public IEnumerable<RouteInfo> GetRoutes()
        {
            string path = Path.Combine(RWPath, "Content", "Routes");

            foreach (string dir in Directory.GetDirectories(path))
            {
                string rp_path = Path.Combine(dir, "RouteProperties.xml");

                if (File.Exists(rp_path)) 
                {
                    yield return new RouteInfo(ParseRouteProperties(rp_path), dir);
                }
                else
                {
                    foreach (string file in Directory.GetFiles(Path.Combine(path, dir), "*.ap"))
                    {
                        using (ZipArchive archive = ZipFile.OpenRead(file))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName.Contains("RouteProperties")))
                            {
                                if (!Directory.Exists(Path.Combine(path, dir, "temp")))
                                    Directory.CreateDirectory(Path.Combine(path, dir, "temp"));

                                entry.ExtractToFile(Path.Combine(path, dir, "temp", entry.FullName), true);
                                yield return new RouteInfo(ParseRouteProperties(Path.Combine(path, dir, "temp", entry.FullName)), dir);
                            }
                        }
                    }
                }
            }
        }

        internal void InitCrawlers()
        {
            Total = 0;
            foreach (RouteInfo ri in Routes)
            {
                ri.Crawler = new RouteCrawler(ri.Path, RWPath);
                ri.Crawler.DeltaProgress += OnProgress;
                ri.Crawler.ProgressUpdated += ri.ProgressUpdated;
                ri.Crawler.Complete += Complete;
                ri.Crawler.RouteSaving += Crawler_RouteSaving;
                Total += 100;
            }
        }

        private void Crawler_RouteSaving(bool saved)
        {
            lock (SavingLock)
            {
                if (saved)
                    Saving--;
                else
                    Saving++;
            }

            RouteSaving?.Invoke(Saving == 0);
        }

        internal void RunAllCrawlers()
        {
            InitCrawlers();
            
            foreach (RouteInfo ri in Routes)
            {
                var t = Task.Run(() => ri.Crawler.Start());
            }
        }

        private void Complete()
        {
            lock (CompleteLock)
            {
                Completed++;

                if (Completed == Routes.Count)
                    MessageBox.Show("DONE!!");
            }
        }

        private void OnProgress(float percent)
        {
            lock (PercentLock)
            {
                Elapsed += percent;

                ProgressUpdated?.Invoke((int)(Elapsed * 100 / Total));
            }
        }

        private bool CheckForFileInAP(string directory, string fileToFind)
        {
            foreach (var file in Directory.GetFiles(directory, "*.ap"))
            {
                var zipFile = ZipFile.OpenRead(file);
                bool hRes = zipFile.Entries.Any(entry => entry.FullName.EndsWith(fileToFind));

                if (!hRes)
                {
                    string parDir = Directory.GetParent(directory).FullName;
                    if (Directory.GetParent(parDir).FullName.EndsWith("Assets"))
                    {
                        return false;
                    }
                    else
                    {
                        return CheckForFileInAP(parDir, fileToFind);
                    }
                }
            }
            return false;
        }

        public void GetMissing()
        {
            foreach (string dependency in AllDependencies)
            {
                if (File.Exists(dependency) || CheckForFileInAP(Directory.GetParent(dependency).FullName, Path.GetFileName(dependency)))
                {
                    continue;
                }
                MissingDependencies.Add(dependency);
            }
        }
    }
}
