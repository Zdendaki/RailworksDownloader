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
        private string rwPath;
        public string RWPath 
        {
            get
            {
                return rwPath;
            }
            set
            {
                rwPath = value;

                if (rwPath != null)
                    AssetsPath = Path.Combine(RWPath, "Assets");
            }
        }

        public string AssetsPath { get; set; }

        public List<RouteInfo> Routes { get; set; }

        public HashSet<string> AllDependencies { get; set; }

        public HashSet<string> AllScenarioDeps { get; set; }

        public HashSet<string> APDependencies { get; set; }

        public HashSet<string> MissingDependencies { get; set; }

        int Total = 0;
        float Elapsed = 0f;
        int Completed = 0;
        object PercentLock = new object();
        object CompleteLock = new object();
        object SavingLock = new object();
        object MissingLock = new object();
        object APDepsLock = new object();
        int Saving = 0;

        public delegate void ProgressUpdatedEventHandler(int percent);
        public event ProgressUpdatedEventHandler ProgressUpdated;

        public delegate void RouteSavingEventHandler(bool saved);
        public event RouteSavingEventHandler RouteSaving;

        public delegate void CompleteEventHandler();
        public event CompleteEventHandler CrawlingComplete;

        public Railworks(string path = null)
        {
            RWPath = string.IsNullOrWhiteSpace(path) ? GetRWPath() : path;
            if (RWPath != null)
                AssetsPath = Path.Combine(RWPath, "Assets");
            AllDependencies = new HashSet<string>();
            AllScenarioDeps = new HashSet<string>();
            Routes = new List<RouteInfo>();
            MissingDependencies = new HashSet<string>();
            APDependencies = new HashSet<string>();
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

        private string ParseRouteProperties(Stream fstream)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(XmlReader.Create(RemoveInvalidXmlChars(fstream), new XmlReaderSettings() { CheckCharacters = false }));

            return ParseDisplayNameNode(doc.DocumentElement.SelectSingleNode("DisplayName"));
        }

        private string ParseRouteProperties(string fpath)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(XmlReader.Create(RemoveInvalidXmlChars(fpath), new XmlReaderSettings() { CheckCharacters = false }));

            return ParseDisplayNameNode(doc.DocumentElement.SelectSingleNode("DisplayName"));
        }

        internal static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
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
                    yield return new RouteInfo(ParseRouteProperties(rp_path).Trim(), Path.GetFileName(dir), dir + Path.DirectorySeparatorChar);
                }
                else
                {
                    foreach (string file in Directory.GetFiles(dir, "*.ap"))
                    {
                        using (ZipArchive archive = ZipFile.OpenRead(file))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName.Contains("RouteProperties")))
                            {
                                yield return new RouteInfo(ParseRouteProperties(entry.Open()).Trim(), Path.GetFileName(dir), dir + Path.DirectorySeparatorChar);
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
                ri.Progress = 0;
                ri.MissingCount = -1;
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
            try {
                InitCrawlers();

                AllDependencies.Clear();
                AllScenarioDeps.Clear();
                MissingDependencies.Clear();
                APDependencies.Clear();

                Parallel.ForEach(Routes, ri =>
                {
                    var t = Task.Run(() => ri.Crawler.Start());
                });
            }
            catch (Exception e)
            {
                Desharp.Debug.Log(e, Desharp.Level.DEBUG);
            }
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
            if (NormalizePath(directory) == NormalizePath(AssetsPath)) //Directory.GetParent(parDir).FullName.EndsWith("Assets") || 
            {
                return false;
            }
            else
            {
                if (Directory.Exists(directory))
                {
                    foreach (var file in Directory.GetFiles(directory, "*.ap"))
                    {
                        try
                        {
                            var zipFile = ZipFile.OpenRead(file);

                            lock (APDepsLock)
                                APDependencies.UnionWith(from x in zipFile.Entries where (x.FullName.Contains(".xml") || x.FullName.Contains(".bin")) select NormalizePath(GetRelativePath(AssetsPath, Path.Combine(directory, x.FullName))));
                        } catch {}
                    }
                    if (APDependencies.Contains(fileToFind) || APDependencies.Contains(NormalizePath(fileToFind, "xml")))
                    {
                        return true;
                    }
                }
                return CheckForFileInAP(Directory.GetParent(directory).FullName, fileToFind);
            }
        }

        public async Task GetMissing()
        {
            await Task.Run(() =>
            {
                foreach (string dependency in AllDependencies.Union(AllScenarioDeps))
                {
                    if (!String.IsNullOrWhiteSpace(dependency))
                    {
                        string path = NormalizePath(Path.Combine(AssetsPath, dependency), "xml");
                        string path_bin = NormalizePath(path, "bin");
                        string relative_path = NormalizePath(GetRelativePath(AssetsPath, path));
                        string relative_path_bin = NormalizePath(relative_path, ".bin");

                        if (File.Exists(path_bin) || File.Exists(path) || APDependencies.Contains(relative_path_bin) || APDependencies.Contains(relative_path) || CheckForFileInAP(Directory.GetParent(path).FullName, relative_path))
                        {
                            continue;
                        }

                        lock (MissingLock)
                            MissingDependencies.Add(NormalizePath(dependency));
                    }
                }
            });
        }

        public static string NormalizePath(string path, string ext = null)
        {

            if (string.IsNullOrEmpty(path))
                return path;

            // Remove path root.
            string path_root = Path.GetPathRoot(path);
            path = Path.ChangeExtension(path.Substring(path_root.Length), ext).Replace('/', Path.DirectorySeparatorChar);

            string[] path_components = path.Split(Path.DirectorySeparatorChar);

            // "Operating memory" for construction of normalized path.
            // Top element is the last path component. Bottom of the stack is first path component.
            Stack<string> stack = new Stack<string>(path_components.Length);

            foreach (string path_component in path_components)
            {
                if (path_component.Length == 0)
                    continue;

                if (path_component == ".")
                    continue;

                if (path_component == ".." && stack.Count > 0 && stack.Peek() != "..")
                {
                    stack.Pop();
                    continue;
                }

                stack.Push(path_component.ToLower());
            }

            string result = string.Join(new string(Path.DirectorySeparatorChar, 1), stack.Reverse().ToArray());
            result = Path.Combine(path_root, result);

            return result;

        }

        public static string GetRelativePath(string relativeTo, string path)
        {
            if (!relativeTo.EndsWith("/") && !relativeTo.EndsWith("\\"))
                relativeTo += Path.DirectorySeparatorChar;

            var uri = new Uri(relativeTo);
            var rel = Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(path)).ToString()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (rel.Contains(Path.DirectorySeparatorChar.ToString()) == false)
            {
                rel = $".{ Path.DirectorySeparatorChar }{ rel }";
            }
            return rel;
        }

        public static Stream RemoveInvalidXmlChars(string fname)
        {
            /*FileStream istream = new FileStream(fname, FileMode.Open);
            Stream ms = new MemoryStream((byte[])StreamToByteArray(istream).Where(b => XmlConvert.IsXmlChar(Convert.ToChar(b))).ToArray());
            istream.Close();
            return ms;*/
            return new MemoryStream(File.ReadAllBytes(fname).Where(b => XmlConvert.IsXmlChar(Convert.ToChar(b))).ToArray());
        }

        public static Stream RemoveInvalidXmlChars(Stream istream)
        {
            return new MemoryStream(StreamToByteArray(istream).Where(b => XmlConvert.IsXmlChar(Convert.ToChar(b))).ToArray());
        }

        private static byte[] StreamToByteArray(Stream istream)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ostream = new MemoryStream())
            {
                int read;
                while ((read = istream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ostream.Write(buffer, 0, read);
                }
                return ostream.ToArray();
            }
        }
    }
}
