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
        public string RWPath;
        List<RouteInfo> Routes;
        HashSet<string> AllDependencies;
        List<RouteCrawler> Crawlers;
        int Total = 0;
        float Elapsed = 0f;
        int Completed = 0;
        object PercentLock = new object();
        object CompleteLock = new object();

        public delegate void ProgressUpdatedEventHandler(int percent);
        public event ProgressUpdatedEventHandler ProgressUpdated;

        public Railworks() : this (null) { }

        public Railworks(string path)
        {
            RWPath = string.IsNullOrWhiteSpace(path) ? GetRWPath() : path;
            Routes = GetRoutes().ToList();
            AllDependencies = new HashSet<string>();
            Crawlers = new List<RouteCrawler>();
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
                Crawlers.Add(new RouteCrawler(ri.Path, RWPath));
                Total += 100;
            }
        }

        internal void RunAllCrawlers()
        {
            foreach (RouteCrawler rc in Crawlers)
            {
                rc.DeltaProgress += OnProgress;
                rc.Complete += Complete;

                var t = Task.Run(() => rc.Start());
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
    }
}
