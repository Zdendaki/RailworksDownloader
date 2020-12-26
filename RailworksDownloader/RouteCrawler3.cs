using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace RailworksDownoader
{
    class RouteCrawler
    {
        private string RoutePath;
        private string RailworksPath;
        private long AllFilesSize = 0;
        private long Progress = 0;
        private object DependenciesLock = new object();
        private object ProgressLock = new object();
        private int PercentProgress = 0;
        private LoadedRoute SavedRoute;
        private SqLiteAdapter Adapter;

        internal bool IsAP { get => !File.Exists(Path.Combine(RoutePath, "RouteProperties.xml")); }

        public delegate void ProgressUpdatedEventHandler(int percent);
        public event ProgressUpdatedEventHandler ProgressUpdated;

        public delegate void CrawlingCompleteEventHandler();
        public event CrawlingCompleteEventHandler Complete;

        public HashSet<string> Dependencies { get; private set; }

        public RouteCrawler(string path, string railworksPath)
        {
            RoutePath = path;
            RailworksPath = railworksPath;
            Dependencies = new HashSet<string>();
            Adapter = new SqLiteAdapter(Path.Combine(RoutePath, "cache.dls"));
            SavedRoute = Adapter.LoadSavedRoute(IsAP);
        }

        public async Task Start()
        {
            if (Directory.Exists(RoutePath))
            {
                AllFilesSize = CountAllFiles();

                await GetDependencies();

                if (PercentProgress != 100)
                    PercentProgress = 100;

                Complete?.Invoke();
            }
        }

        private void ReportProgress(string file)
        {
            lock (ProgressLock)
            {
                Progress += GetFileSize(file);
            }

            Debug.Assert(Progress <= AllFilesSize, "Fatal, Progress is bigger than size of all files! " + Progress + ":" + AllFilesSize);

            Progress = Math.Min(Progress, AllFilesSize);

            PercentProgress = (int)(Progress * 100 / AllFilesSize);

            ProgressUpdated?.Invoke(PercentProgress);
        }

        private string ParseAbsoluteBlueprintIDNode(XmlNode blueprintIDNode)
        {
            var absoluteBlueprintID = blueprintIDNode.FirstChild;
            var blueprintSetID = absoluteBlueprintID.FirstChild.FirstChild;
            return Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, absoluteBlueprintID.LastChild.InnerText);
        }

        private IEnumerable<string> ParseSkiesNode(XmlNode skiesNode)
        {
            foreach (XmlNode n in skiesNode.FirstChild)
            {
                yield return ParseAbsoluteBlueprintIDNode(n);
            }
        }

        private async Task<IEnumerable<string>> GetRoutePropertiesDependencies(string propertiesPath)
        {
            var dependencies = new List<string>();

            XmlDocument doc = new XmlDocument();
            doc.Load(propertiesPath);

            var root = doc.DocumentElement;

            await Task.Run(() =>
            {
                dependencies.Add(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("BlueprintID")));
                dependencies.AddRange(ParseSkiesNode(root.SelectSingleNode("Skies")));
                dependencies.Add(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("WeatherBlueprint")));
                dependencies.Add(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("TerrainBlueprint")));
                dependencies.Add(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("MapBlueprint")));
            });

            return dependencies;
        }

        private IEnumerable<string> ParseBlueprint(string blueprintPath)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(blueprintPath);

            foreach (XmlNode node in doc.SelectNodes("//Provider"))
            {
                var blueprintSetID = node.ParentNode;
                var absoluteBlueprintID = blueprintSetID.ParentNode.ParentNode;
                yield return Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, absoluteBlueprintID.LastChild.InnerText);
            }
        }

        private async Task GetScenariosDependencies(string path)
        {
            //TODO: implement scenarios
        }

        private async Task ParseNetworkDependencies(string path)
        {
            foreach (string dir in Directory.GetDirectories(path))
            {
                foreach (string file in Directory.GetFiles(dir, "*.*bin"))
                {
                    string xml = Path.ChangeExtension(file, ".xml");

                    await Task.Run(() => RunSERZ(file));
                    try
                    {
                        lock (DependenciesLock)
                        {
                            Dependencies.UnionWith(ParseBlueprint(xml));
                            ReportProgress(file);
                        }
                    }
                    catch { }
                    File.Delete(xml);
                }
            }
        }

        private async Task ParseSceneryDependencies(string path)
        {
            foreach (string file in Directory.GetFiles(path, "*.*bin"))
            {
                string xml = Path.ChangeExtension(file, ".xml");

                await Task.Run(() => RunSERZ(file));
                try
                {
                    lock (DependenciesLock)
                    {
                        Dependencies.UnionWith(ParseBlueprint(xml));
                        ReportProgress(file);
                    }
                }
                catch { }
                File.Delete(xml);
            }
        }

        private void RunSERZ(string file)
        {
            string xml = Path.ChangeExtension(file, ".xml");

            ProcessStartInfo si = new ProcessStartInfo();
            si.FileName = Path.Combine(RailworksPath, "serz.exe");
            si.Arguments = $"\"{file}\" /xml:\"{xml}\"";
            si.RedirectStandardOutput = true;
            si.RedirectStandardError = true;
            si.UseShellExecute = false;
            si.CreateNoWindow = true;

            Process serz = new Process();
            serz.StartInfo = si;

            serz.Start();
            serz.WaitForExit();
        }

        private async Task _GetDependencies(string routePath)
        {
            bool apChanged = false;

            if (IsAP)
            {
                apChanged = GetDirectoryMD5(RoutePath) != SavedRoute.APChecksum;
            }

            bool loftsChanged = (GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Loft Tiles")) != SavedRoute.LoftChecksum) || apChanged;
            bool roadsChanged = (GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Road Tiles")) != SavedRoute.RoadChecksum) || apChanged;
            bool tracksChanged = (GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Track Tiles")) != SavedRoute.TrackChecksum) || apChanged;
            bool sceneryChanged = (GetDirectoryMD5(Path.Combine(RoutePath, "Scenery")) != SavedRoute.SceneryChecksum) || apChanged;
            bool rpChanged = (GetFileMD5(Path.Combine(RoutePath, "RouteProperties.xml")) != SavedRoute.RoutePropertiesChecksum) || apChanged;
            bool scenariosChanged = false; //TODO: implement scenarios

            var md5 = ComputeChecksums();

            if (SavedRoute.Dependencies != null)
                Dependencies.UnionWith(SavedRoute.Dependencies);

            if (loftsChanged || roadsChanged || tracksChanged || sceneryChanged || rpChanged || scenariosChanged)
            {
                Task n = null;
                Task s = null;
                Task t = null;
                Task<IEnumerable<string>> prop = null;

                if (rpChanged)
                    prop = GetRoutePropertiesDependencies(Path.Combine(routePath, "RouteProperties.xml"));

                foreach (string dir in Directory.GetDirectories(routePath))
                {
                    switch (Path.GetFileName(dir))
                    {
                        case "Networks":
                            if (loftsChanged || roadsChanged || tracksChanged)
                                n = ParseNetworkDependencies(dir);
                            break;
                        case "Scenarios":
                            if (scenariosChanged)
                                s = GetScenariosDependencies(dir);
                            break;
                        case "Scenery":
                            if (sceneryChanged)
                                t = ParseSceneryDependencies(dir);
                            break;
                    }
                }

                if (n != null)
                    await n;
                if (s != null)
                    await s;
                if (t != null)
                    await t;

                IEnumerable<string> routeProperties = new List<string>();
                if (prop != null)
                    routeProperties = await prop;

                lock (DependenciesLock)
                {
                    Dependencies.UnionWith(routeProperties);
                    ReportProgress(Path.Combine(routePath, "RouteProperties.xml"));
                }

                Dependencies.RemoveWhere(x => string.IsNullOrWhiteSpace(x));

                await md5;

                SavedRoute.Dependencies = Dependencies.ToList();

                Thread tt = new Thread(() => Adapter.SaveRoute(SavedRoute));
                tt.Start();
            }
        }

        private async Task ComputeChecksums()
        {
            await Task.Run(() =>
            {
                SavedRoute.LoftChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Loft Tiles"));
                SavedRoute.RoadChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Road Tiles"));
                SavedRoute.TrackChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Track Tiles"));
                SavedRoute.SceneryChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Scenery"));
                if (IsAP)
                {
                    SavedRoute.APChecksum = GetDirectoryMD5(RoutePath);
                }
                else
                {
                    SavedRoute.RoutePropertiesChecksum = GetFileMD5(Path.Combine(RoutePath, "RouteProperties.xml"));
                }
            });
        }

        private async Task GetDependencies()
        {
            if (File.Exists(Path.Combine(RoutePath, "RouteProperties.xml")))
            {
                await _GetDependencies(RoutePath);
            }
            else
            {
                int count = 0;
                foreach (string file in Directory.GetFiles(RoutePath, "*.ap"))
                {
                    try
                    {
                        ZipFile.ExtractToDirectory(Path.Combine(RoutePath, file), Path.Combine(RoutePath, "temp"));
                    }
                    catch { }
                    finally
                    {
                        count++;
                    }
                }

                if (count > 0)
                {
                    await _GetDependencies(Path.Combine(RoutePath, "temp"));

                    await Task.Run(() => { DeleteDirectory(Path.Combine(RoutePath, "temp")); });
                }
            }
        }

        private long CountAllFiles()
        {
            long size = 0;

            size += GetFileSize(Path.Combine(RoutePath, "RouteProperties.xml"));

            foreach (string dir in Directory.GetDirectories(RoutePath))
            {
                switch (Path.GetFileName(dir))
                {
                    case "Networks":
                        foreach (string dir2 in Directory.GetDirectories(dir))
                        {
                            size += GetDirectorySize(dir2, "*.*bin");
                        }
                        break;
                    case "Scenarios":
                        //TODO: implement scenarios
                        break;
                    case "Scenery":
                        size += GetDirectorySize(dir, "*.*bin");
                        break;
                }
            }

            return size;
        }

        private static void DeleteDirectory(string directory)
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

            Directory.Delete(directory, false);
        }

        private long GetDirectorySize(string directory, string mask)
        {
            long size = 0;
            
            foreach (var file in Directory.GetFiles(directory, mask))
            {
                size += GetFileSize(file);
            }

            return size;
        }

        private long GetFileSize(string file)
        {
            FileInfo fi = new FileInfo(file);
            return fi.Length;
        }

        private string GetFileMD5(string path)
        {
            using (var md5 = MD5.Create())
            {
                byte[] pathBytes = Encoding.UTF8.GetBytes(path);
                md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                byte[] contentBytes = File.ReadAllBytes(path);
                md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);

                md5.TransformFinalBlock(new byte[0], 0, 0);

                return BitConverter.ToString(md5.Hash).Replace("-", "").ToLower();
            }
        }

        private string GetDirectoryMD5(string path)
        {
            var filePaths = Directory.GetFiles(path, "*.*bin").OrderBy(p => p).ToArray();

            using (var md5 = MD5.Create())
            {
                foreach (var filePath in filePaths)
                {
                    byte[] pathBytes = Encoding.UTF8.GetBytes(filePath);
                    md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                    byte[] contentBytes = File.ReadAllBytes(filePath);
                    md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
                }

                md5.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(md5.Hash).Replace("-", "").ToLower();
            }
        }
    }
}
