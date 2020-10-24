using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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
        }

        public async Task Start()
        {
            AllFilesSize = CountAllFiles();

            await GetDependencies();

            Complete?.Invoke();
        }

        private void ReportProgress(string file)
        {
            lock (ProgressLock)
            {
                Progress += GetFileSize(file);
            }

            if (Progress > AllFilesSize)
                Progress = AllFilesSize;

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

        private async Task SelectDependencies(string routePath)
        {
            Task n = null;
            Task s = null;
            Task t = null;
            var prop = GetRoutePropertiesDependencies(Path.Combine(routePath, "RouteProperties.xml"));

            foreach (string dir in Directory.GetDirectories(routePath))
            {
                switch (Path.GetFileName(dir))
                {
                    case "Networks":
                        n = ParseNetworkDependencies(dir);
                        break;
                    case "Scenarios":
                        s = GetScenariosDependencies(dir);
                        break;
                    case "Scenery":
                        t = ParseSceneryDependencies(dir);
                        break;
                }
            }

            await n;
            await s;
            await t;

            var routeProperties = await prop;
            lock (DependenciesLock)
            {
                Dependencies.UnionWith(routeProperties);
                ReportProgress(Path.Combine(routePath, "RouteProperties.xml"));
            }
        }

        private async Task GetDependencies()
        {
            if (File.Exists(Path.Combine(RoutePath, "RouteProperties.xml")))
            {
                await SelectDependencies(RoutePath);
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
                    await SelectDependencies(Path.Combine(RoutePath, "temp"));

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
                            size += GetDirectorySize(dir, "*.*bin");
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
    }
}
