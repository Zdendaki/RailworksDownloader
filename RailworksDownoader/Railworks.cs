using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;

namespace RailworksDownloader
{
    public static class LinqExtension
    {
        public static IEnumerable<TSource> Intersect<TSource>(this HashSet<TSource> first, HashSet<TSource> second)
        {
            if (first == null) throw new ArgumentException("first");
            if (second == null) throw new ArgumentException("second");

            return (first.Count > second.Count) ?
                first.IntersectEnumerator(second, EqualityComparer<TSource>.Default) :
                second.IntersectEnumerator(first, EqualityComparer<TSource>.Default);
        }

        public static IEnumerable<TSource> Intersect<TSource>(this HashSet<TSource> first, HashSet<TSource> second, EqualityComparer<TSource> comparer)
        {
            if (first == null) throw new ArgumentException("first");
            if (second == null) throw new ArgumentException("second");

            return (first.Count > second.Count) ?
                first.IntersectEnumerator(second, comparer) :
                    second.IntersectEnumerator(first, comparer);
        }

        private static IEnumerable<TSource> IntersectEnumerator<TSource>(this HashSet<TSource> first, HashSet<TSource> second, EqualityComparer<TSource> comparer)
        {
            if (first.Comparer != comparer)
                return Intersect(first, second, comparer);
            else
                return IntersectEnumerator(first, second);
        }

        private static IEnumerable<TSource> IntersectEnumerator<TSource>(this HashSet<TSource> first, HashSet<TSource> second)
        {
            foreach (var tmp in second)
            {
                if (first.Contains(tmp)) { yield return tmp; }
            }
        }
    }

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
                    AssetsPath = Path.Combine(RWPath, "Assets");
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
            object total_lock = new object();
            int maxThreads = Math.Min(Environment.ProcessorCount, Routes.Count);
            Parallel.For(0, maxThreads, workerId =>
            {
                var max = Routes.Count * (workerId + 1) / maxThreads;
                for (int i = Routes.Count * workerId / maxThreads; i < max; i++)
                {
                    RouteInfo ri = Routes[i];
                    ri.Progress = 0;
                    ri.Crawler = new RouteCrawler(ri.Path, RWPath, ri.Dependencies, ri.ScenarioDeps);
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
            try
            {
                InitCrawlers();

                APDependencies.Clear();

                int maxThreads = Math.Min(Environment.ProcessorCount, Routes.Count);
                Parallel.For(0, maxThreads, workerId =>
                {
                    var max = Routes.Count * (workerId + 1) / maxThreads;
                    for (int i = Routes.Count * workerId / maxThreads; i < max; i++)
                    {
                        Routes[i].Crawler.Start();
                    }
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
            if (NormalizePath(directory) == NormalizePath(AssetsPath))
            {
                return false;
            }
            else
            {
                if (Directory.Exists(directory))
                {
                    foreach (string file in Directory.GetFiles(directory, "*.ap"))
                    {
                        try
                        {
                            ZipArchive zipFile = ZipFile.OpenRead(file);

                            lock (APDepsLock)
                                APDependencies.UnionWith(from x in zipFile.Entries where (x.FullName.Contains(".xml") || x.FullName.Contains(".bin")) select NormalizePath(GetRelativePath(AssetsPath, Path.Combine(directory, x.FullName))));
                        }
                        catch { }
                    }
                    if (APDependencies.Contains(fileToFind) || APDependencies.Contains(NormalizePath(fileToFind, "xml")))
                    {
                        return true;
                    }
                }
                return CheckForFileInAP(Directory.GetParent(directory).FullName, fileToFind);
            }
        }

        public async Task<HashSet<string>> GetMissing(HashSet<string> globalDeps)
        {
            HashSet<string> existingDeps = new HashSet<string>();

            await Task.Run(() =>
            {
                int maxThreads = Math.Min(Environment.ProcessorCount, globalDeps.Count);
                Parallel.For(0, maxThreads, workerId =>
                {
                    var max = globalDeps.Count * (workerId + 1) / maxThreads;
                    for (int i = globalDeps.Count * workerId / maxThreads; i < max; i++)
                    {
                        string dependency = globalDeps.ElementAt(i);
                        if (!string.IsNullOrWhiteSpace(dependency))
                        {
                            string path = NormalizePath(Path.Combine(AssetsPath, dependency), "xml");
                            string path_bin = NormalizePath(path, "bin");
                            string relative_path = NormalizePath(GetRelativePath(AssetsPath, path));
                            string relative_path_bin = NormalizePath(relative_path, ".bin");

                            bool exists = File.Exists(path_bin) || File.Exists(path) || APDependencies.Contains(relative_path_bin) || APDependencies.Contains(relative_path) || CheckForFileInAP(Directory.GetParent(path).FullName, relative_path);

                            if (exists)
                                lock(existingDeps)
                                    existingDeps.Add(dependency);
                        }
                    }
                });
            });

            return existingDeps;
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

            Uri uri = new Uri(relativeTo);
            string rel = Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(path)).ToString()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
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

        public class MemoryInformation
        {
            [DllImport("KERNEL32.DLL")]
            private static extern int OpenProcess(uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);
            [DllImport("KERNEL32.DLL")]
            private static extern int CloseHandle(int handle);

            [StructLayout(LayoutKind.Sequential)]
            private class PROCESS_MEMORY_COUNTERS
            {
                public int cb;
                public int PageFaultCount;
                public int PeakWorkingSetSize;
                public int WorkingSetSize;
                public int QuotaPeakPagedPoolUsage;
                public int QuotaPagedPoolUsage;
                public int QuotaPeakNonPagedPoolUsage;
                public int QuotaNonPagedPoolUsage;
                public int PagefileUsage;
                public int PeakPagefileUsage;
            }

            [DllImport("psapi.dll")]
            private static extern int GetProcessMemoryInfo(int hProcess, [Out] PROCESS_MEMORY_COUNTERS counters, int size);

            public static long GetMemoryUsageForProcess(long pid)
            {
                long mem = 0;
                int pHandle = OpenProcess(0x0400 | 0x0010, 0, (uint)pid);
                try
                {
                    var pmc = new PROCESS_MEMORY_COUNTERS();
                    if (GetProcessMemoryInfo(pHandle, pmc, 40) != 0)
                        mem = pmc.WorkingSetSize;
                }
                finally
                {
                    CloseHandle(pHandle);
                }
                return mem;
            }
        }
    }
}
