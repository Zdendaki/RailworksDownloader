using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using Sentry;
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
using static RailworksDownloader.Utils;

namespace RailworksDownloader
{
    internal class Railworks
    {
        private string rwPath;
        public string RWPath
        {
            get => rwPath;
            set
            {
                rwPath = value;

                if (rwPath != null)
                    AssetsPath = NormalizePath(Path.Combine(RWPath, "Assets"));
            }
        }

        public string AssetsPath { get; set; }

        public List<RouteInfo> Routes { get; set; }

        private readonly HashSet<string> APDependencies = new HashSet<string>();
        private int Total = 0;
        private float Elapsed = 0f;
        private int Completed = 0;
        private readonly object PercentLock = new object();
        private readonly object CompleteLock = new object();
        private readonly object SavingLock = new object();
        private readonly object APDepsLock = new object();
        private int Saving = 0;

        public EventWaitHandle getAllInstalledDepsEvent = new EventWaitHandle(false, EventResetMode.ManualReset);

        public delegate void ProgressUpdatedEventHandler(int percent);
        public event ProgressUpdatedEventHandler ProgressUpdated;

        public delegate void RouteSavingEventHandler(bool saved);
        public event RouteSavingEventHandler RouteSaving;

        public delegate void CompleteEventHandler();
        public event CompleteEventHandler CrawlingComplete;

        public HashSet<string> AllRequiredDeps { get; set; } = new HashSet<string>();

        public HashSet<string> AllInstalledDeps { get; set; } = new HashSet<string>();

        public IEnumerable<string> AllMissingDeps { get; set; } = new string[0];

        public Railworks(string path = null)
        {
            RWPath = string.IsNullOrWhiteSpace(path) ? GetRWPath() : path;

            if (RWPath != null)
                AssetsPath = NormalizePath(Path.Combine(RWPath, "Assets"));

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

        private string ParseRouteProperties(Stream istream, string file, string routeHash, long? entryLength = null)
        {
            if ((entryLength ?? istream.Length) > 4L)
            {
                MemoryStream stream = new MemoryStream();
                istream.CopyTo(stream);
                istream.Close();
                stream.Seek(0, SeekOrigin.Begin);

                if (CheckIsSerz(stream))
                {
                    SerzReader sr = new SerzReader(stream, file, SerzReader.MODES.routeName);
                    Trace.Assert(sr.RouteName != null, Localization.Strings.NoRouteName);
                    return sr.RouteName ?? routeHash;
                }
                else
                {
                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(XmlReader.Create(RemoveInvalidXmlChars(stream), new XmlReaderSettings() { CheckCharacters = false }));

                        string routeName = ParseDisplayNameNode(doc.DocumentElement.SelectSingleNode("DisplayName"));
                        Trace.Assert(routeName != null, Localization.Strings.NoRouteName);
                        return routeName ?? routeHash;
                    }
                    catch (Exception e)
                    {
                        if (App.ReportErrors) {
                            SentrySdk.CaptureException(e, scope =>
                            {
                                scope.AddAttachment(stream.ToArray(), file);
                            });
                        }
                        MessageBox.Show(string.Format(Localization.Strings.ParseRoutePropFail, file), Localization.Strings.ParseRoutePropFailTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                if (App.ReportErrors)
                {
                    SentrySdk.CaptureMessage($"{file} has no route name!", scope =>
                    {
                        scope.AddAttachment(stream.ToArray(), file);
                    }, SentryLevel.Warning);
                }
            }
            Debug.Assert(false, Localization.Strings.NoRouteName);
            return routeHash;
        }

        private string ParseRouteProperties(string fpath, string routeHash)
        {
            using (Stream fs = File.OpenRead(fpath))
            {
                return ParseRouteProperties(fs, fpath, routeHash);
            }
        }

        internal static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                try
                {
                    string[] files = Directory.GetFiles(directory);
                    string[] dirs = Directory.GetDirectories(directory);

                    foreach (string file in files)
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }

                    foreach (string dir in dirs)
                    {
                        DeleteDirectory(dir);
                    }

                    Directory.Delete(directory, true);
                }
                catch { }
            }
        }

        /// <summary>
        /// Gets list of routes
        /// </summary>
        /// <param name="path">Routes path</param>
        /// <returns></returns>
        public IEnumerable<RouteInfo> GetRoutes()
        {
            string path = Path.Combine(RWPath, "Content", "Routes");
            List<RouteInfo> list = new List<RouteInfo>();

            if (!Directory.Exists(path))
                return list;

            try
            {
                foreach (string dir in Directory.GetDirectories(path))
                {
                    string rp_path = FindFile(dir, "RouteProperties.*");
                    string routeHash = Path.GetFileName(dir);

                    if (File.Exists(rp_path))
                    {
                        list.Add(
                            new RouteInfo(
                                ParseRouteProperties(rp_path, routeHash).Trim(),
                                routeHash,
                                dir + Path.DirectorySeparatorChar
                            )
                        );
                    }
                    else
                    {
                        try
                        {
                            foreach (string file in Directory.GetFiles(dir, "*.ap"))
                            {
                                try
                                {
                                    using (ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(file))
                                    {
                                        foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName.Contains("RouteProperties")))
                                        {
                                            list.Add(
                                                new RouteInfo(
                                                    ParseRouteProperties(
                                                        entry.Open(),
                                                        Path.Combine(file, entry.FullName),
                                                        routeHash,
                                                        entry.Length
                                                    ).Trim(),
                                                    Path.GetFileName(dir),
                                                    dir + Path.DirectorySeparatorChar
                                                )
                                            );
                                            break;
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    SentrySdk.CaptureException(e);
                                    Trace.Assert(false, string.Format(Localization.Strings.ReadingZipFail, file));
                                }
                            }
                        }
                        catch
                        {
                            DisplayError(Localization.Strings.RoutesLoadFail, string.Format(Localization.Strings.RoutesLoadFailText, routeHash));
                        }
                    }
                }
            }
            catch
            {
                DisplayError(Localization.Strings.RoutesLoadFail, Localization.Strings.RoutesLoadFailText);
            }

            return list;
        }

        internal void InitCrawlers()
        {
            Total = 0;
            object total_lock = new object();
            int maxThreads = Math.Min(Environment.ProcessorCount, Routes.Count);
            Parallel.For(0, maxThreads, workerId =>
            {
                int max = Routes.Count * (workerId + 1) / maxThreads;
                for (int i = Routes.Count * workerId / maxThreads; i < max; i++)
                {
                    RouteInfo ri = Routes[i];
                    ri.Progress = 0;
                    ri.Crawler = new RouteCrawler(ri.Path, ri.Dependencies, ri.ScenarioDeps);
                    ri.Crawler.DeltaProgress += OnProgress;
                    ri.Crawler.ProgressUpdated += ri.ProgressUpdated;
                    ri.Crawler.Complete += Complete;
                    ri.Crawler.RouteSaving += Crawler_RouteSaving;
                    lock (total_lock)
                        Total += 100;
                }
            });
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

            APDependencies.Clear();

            int maxThreads = Math.Min(Environment.ProcessorCount, Routes.Count);
            Parallel.For(0, maxThreads, workerId =>
            {
                int max = Routes.Count * (workerId + 1) / maxThreads;
                for (int i = Routes.Count * workerId / maxThreads; i < max; i++)
                {
                    Routes[i].Crawler.Start();
                }
            });
        }

        private void Complete()
        {
            lock (CompleteLock)
            {
                Completed++;

                if (Completed == Routes.Count)
                    CrawlingComplete?.Invoke();
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
            if (NormalizePath(directory) == NormalizePath(AssetsPath) || string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }
            else
            {
                if (Directory.Exists(directory))
                {
                    try
                    {
                        foreach (string file in Directory.GetFiles(directory, "*.ap"))
                        {
                            try
                            {
                                ZipArchive zipFile = System.IO.Compression.ZipFile.OpenRead(file);

                                lock (APDepsLock)
                                    APDependencies.UnionWith(from x in zipFile.Entries where x.FullName.Contains(".xml") || x.FullName.Contains(".bin") select NormalizePath(GetRelativePath(AssetsPath, Path.Combine(directory, x.FullName))));
                            }
                            catch (Exception e)
                            {
                                SentrySdk.CaptureException(e);
                            }
                        }
                        if (APDependencies.Contains(fileToFind) || APDependencies.Contains(NormalizePath(fileToFind, "xml")))
                        {
                            return true;
                        }
                    } catch { }
                }
                try
                {
                    return CheckForFileInAP(Directory.GetParent(directory).FullName, fileToFind);
                }
                catch
                {
                    return false;
                }
            }
        }

        public void GetInstalledDeps()
        {
            AllInstalledDeps = new HashSet<string>();

            if (!Directory.Exists(AssetsPath))
            {
                getAllInstalledDepsEvent.Set();
                return;
            }

            string lastFileName = "";
            try {
                //string[] files = Directory.GetFiles(AssetsPath, "*.*", SearchOption.AllDirectories);
                foreach (FileInfo fileInfo in new DirectoryInfo(AssetsPath).EnumerateFiles("*.*", SearchOption.AllDirectories))
                {
                    lastFileName = fileInfo.FullName;
                    string ext = fileInfo.Extension.ToLower();
                    if (ext == ".bin" || ext == ".xml")
                    {
                        AllInstalledDeps.Add(NormalizePath(GetRelativePath(AssetsPath, fileInfo.FullName)));
                    }
                    else if (ext == ".ap")
                    {
                        try
                        {
                            using (ICSharpCode.SharpZipLib.Zip.ZipFile zip = new ICSharpCode.SharpZipLib.Zip.ZipFile(fileInfo.FullName))
                            {
                                foreach (ZipEntry entry in zip)
                                {
                                    string iExt = Path.GetExtension(entry.Name).ToLower();
                                    if (iExt == ".xml" || iExt == ".bin")
                                    {
                                        AllInstalledDeps.Add(NormalizePath(GetRelativePath(AssetsPath, Path.Combine(Path.GetDirectoryName(fileInfo.FullName), entry.Name))));
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            SentrySdk.CaptureException(e);
                            Debug.Assert(false, string.Format(Localization.Strings.ReadingZipFail, fileInfo.FullName));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                Trace.Assert(false, string.Format(Localization.Strings.LoadingInstalledFilesError, lastFileName), e.Message);
            }
            getAllInstalledDepsEvent.Set();
        }

        public async Task<HashSet<string>> GetInstalledDeps(HashSet<string> globalDeps)
        {
            HashSet<string> existingDeps = new HashSet<string>();

            await Task.Run(() =>
            {
                int maxThreads = Math.Min(Environment.ProcessorCount, globalDeps.Count);
                Parallel.For(0, maxThreads, workerId =>
                {
                    int max = globalDeps.Count * (workerId + 1) / maxThreads;
                    for (int i = globalDeps.Count * workerId / maxThreads; i < max; i++)
                    {
                        string dependency = globalDeps.ElementAt(i);
                        if (!string.IsNullOrWhiteSpace(dependency))
                        {
                            string path = NormalizePath(Path.Combine(AssetsPath, dependency), "xml");
                            string path_bin = NormalizePath(path, "bin");
                            string relative_path = NormalizePath(GetRelativePath(AssetsPath, path));
                            string relative_path_bin = NormalizePath(relative_path, ".bin");

                            bool exists = false;
                            try
                            {
                                exists = APDependencies.Contains(relative_path_bin) || APDependencies.Contains(relative_path) || File.Exists(path_bin) || File.Exists(path) || CheckForFileInAP(Directory.GetParent(path).FullName, relative_path);
                            } catch { }

                            if (exists)
                                lock (existingDeps)
                                    existingDeps.Add(dependency);
                        }
                    }
                });
            });

            return existingDeps;
        }
    }
}
