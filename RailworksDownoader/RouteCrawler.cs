using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Xml;
using System.Xml.Linq;

namespace RailworksDownloader
{
    /// <summary>
    /// Crawles specific route
    /// </summary>
    public class RouteCrawler
    {
        // Route path
        private string RoutePath;
        // Temp route path
        private string TempPath;
        // Railworks path
        private string RailworksPath;
        // Size of all route files
        private long AllFilesSize = 0;
        // Size of processed route files
        private long Progress = 0;
        // Thread locks
        private object DependenciesLock = new object();
        private object ScenarioDepsLock = new object();
        private object ProgressLock = new object();
        // Total crawling process (%)
        internal float PercentProgress = 0f;
        // Loaded route data
        private LoadedRoute SavedRoute;
        // Database adapter
        private SqLiteAdapter Adapter;

        /// <summary>
        /// Is route from asset pack
        /// </summary>
        internal bool ContainsAP { get => Directory.GetFiles(RoutePath, "*.ap", SearchOption.AllDirectories).Any(); }

        // Progress updated
        public delegate void ProgressUpdatedEventHandler(float percent);
        /// <summary>
        /// Total absolute progress updated
        /// </summary>
        public event ProgressUpdatedEventHandler ProgressUpdated;
        /// <summary>
        /// Total relative progress updated
        /// </summary>
        public event ProgressUpdatedEventHandler DeltaProgress;

        public delegate void CrawlingCompleteEventHandler();
        /// <summary>
        /// Crawling complete
        /// </summary>
        public event CrawlingCompleteEventHandler Complete;

        public delegate void RouteSavingEventHandler(bool saved);
        /// <summary>
        /// Is route saving
        /// </summary>
        public event RouteSavingEventHandler RouteSaving;

        /// <summary>
        /// All route dependencies
        /// </summary>
        public HashSet<string> Dependencies { get; set; }

        /// <summary>
        /// All scenarios dependencies
        /// </summary>
        public HashSet<string> ScenarioDeps { get; set; }

        /// <summary>
        /// All route missing dependencies
        /// </summary>
        public List<string> MissingDependencies { get; set; }

        /// <summary>
        /// All route missing dependencies
        /// </summary>
        public List<string> MissingScenarioDeps { get; set; }

        /// <summary>
        /// All route missing dependencies
        /// </summary>
        public List<string> DownloadableDependencies { get; set; }

        /// <summary>
        /// All route missing dependencies
        /// </summary>
        public List<string> DownloadableScenarioDeps { get; set; }

        /// <summary>
        /// Initializes route crawler
        /// </summary>
        /// <param name="path">Route path</param>
        /// <param name="railworksPath">RailWorks path</param>
        public RouteCrawler(string path, string railworksPath)
        {
            RoutePath = path;
            TempPath = ContainsAP ? GetTemporaryDirectory() : null;
            RailworksPath = railworksPath;
            Dependencies = new HashSet<string>();
            ScenarioDeps = new HashSet<string>();
            Adapter = new SqLiteAdapter(Path.Combine(RoutePath, "cache.dls"));
            SavedRoute = Adapter.LoadSavedRoute(ContainsAP);
            MissingDependencies = new List<string>();
            MissingScenarioDeps = new List<string>();
            DownloadableDependencies = new List<string>();
            DownloadableScenarioDeps = new List<string>();
        }

        public string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        /// <summary>
        /// Start route crawling
        /// </summary>
        /// <returns></returns>
        public async Task Start()
        {
            try
            {
                // If route directory exists
                if (Directory.Exists(RoutePath))
                {
                    // Counts size of all files
                    AllFilesSize = CountAllFiles();

                    // Find all dependencies
                    await GetDependencies();

                    // If crawling skipped because cache or inaccuracy, adds to 100 %
                    if (PercentProgress != 100)
                    {
                        DeltaProgress?.Invoke(100f - PercentProgress);
                        PercentProgress = 100;
                        ProgressUpdated?.Invoke(PercentProgress);
                    }

                    App.Railworks.AllDependencies.UnionWith(Dependencies);
                    App.Railworks.AllScenarioDeps.UnionWith(ScenarioDeps);
                }

                // Crawling complete event
                Complete?.Invoke();
            } 
            catch (Exception e)
            {
                Desharp.Debug.Log(e, Desharp.Level.DEBUG);

                // store exception with all inner exceptions and everything else
                // you need to know later in exceptions.html or exceptions.log file
                Desharp.Debug.Log(e.ToString());
            }
        }

        /// <summary>
        /// Report route progress
        /// </summary>
        /// <param name="file"></param>
        private void ReportProgress(string file)
        {
            float delta;
            
            lock (ProgressLock)
            {
                Progress += GetFileSize(file);
            }

            Debug.Assert(Progress <= AllFilesSize, "Fatal, Progress is bigger than size of all files! " + Progress + ":" + AllFilesSize+"\nRoute: "+RoutePath);
            if (Progress <= AllFilesSize)
            {

                if (AllFilesSize > 0)
                {
                    PercentProgress = Progress * 100f / AllFilesSize;
                    delta = (float)(GetFileSize(file) * 100.0f / AllFilesSize);
                }
                else
                {
                    PercentProgress = 100f;
                    delta = 100f;
                }

                // Invoke progress events
                ProgressUpdated?.Invoke(PercentProgress);
                DeltaProgress?.Invoke(delta);
            }
        }

        /// <summary>
        /// Parse blueprint ID node
        /// </summary>
        /// <param name="blueprintIDNode">Blueprint node</param>
        /// <returns>Parsed node</returns>
        private string ParseAbsoluteBlueprintIDNode(XmlNode blueprintIDNode)
        {
            if (blueprintIDNode != null)
            {
                var absoluteBlueprintID = blueprintIDNode.FirstChild;
                var blueprintSetID = absoluteBlueprintID.FirstChild.FirstChild;
                string fname = absoluteBlueprintID.LastChild.InnerText.ToLower();
                if (String.IsNullOrWhiteSpace(fname) || !(Path.GetExtension(fname) == ".xml" || Path.GetExtension(fname) == ".bin"))
                    return String.Empty;
                return Railworks.NormalizePath(Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, fname));
            }
            return null;
        }

        /// <summary>
        /// Parse skies node
        /// </summary>
        /// <param name="skiesNode">Skies node</param>
        /// <returns>Parsed node</returns>
        private IEnumerable<string> ParseSkiesNode(XmlNode skiesNode)
        {
            foreach (XmlNode n in skiesNode.FirstChild)
            {
                yield return ParseAbsoluteBlueprintIDNode(n);
            }
        }

        /// <summary>
        /// Get all route properties dependencies
        /// </summary>
        /// <param name="propertiesPath">Route properties path</param>
        /// <returns>All route properties dependencies</returns>
        private async Task<IEnumerable<string>> GetRoutePropertiesDependencies(string propertiesPath)
        {
            // Check if route properties exists
            if (!File.Exists(propertiesPath))
                return new List<string>();
            
            var dependencies = new List<string>();

            // Load route properties file
            XmlDocument doc = new XmlDocument();

            /*try
            {
                doc.Load(propertiesPath);
            }
            catch
            {
                return new List<string>();
            }*/
            doc.Load(XmlReader.Create(Railworks.RemoveInvalidXmlChars(propertiesPath), new XmlReaderSettings() { CheckCharacters = false }));

            var root = doc.DocumentElement;

            // Parse route properties entries
            await Task.Run(() =>
            {
                dependencies.Add(Railworks.NormalizePath(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("BlueprintID"))));
                dependencies.AddRange(ParseSkiesNode(root.SelectSingleNode("Skies")));
                dependencies.Add(Railworks.NormalizePath(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("WeatherBlueprint"))));
                dependencies.Add(Railworks.NormalizePath(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("TerrainBlueprint"))));
                dependencies.Add(Railworks.NormalizePath(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("MapBlueprint"))));
            });

            ReportProgress(propertiesPath);

            return dependencies;
        }

        /// <summary>
        /// Parse blueprint file
        /// </summary>
        /// <param name="blueprintPath">Blueprint file path</param>
        /// <returns>Parsed file</returns>
        private void ParseBlueprint(string blueprintPath, bool isScenario = false)
        {
            if (!File.Exists(blueprintPath))
                return;

            // Load blueprint file
            XmlDocument doc = new XmlDocument();

            try
            {
                doc.Load(XmlReader.Create(Railworks.RemoveInvalidXmlChars(blueprintPath), new XmlReaderSettings() { CheckCharacters = false }));
            }
            catch
            {
                return;
            }

            // Parse blueprint file
            //Parallel.ForEach(Routes, (ri) => ri.Crawler.Start());
            Parallel.ForEach(doc.SelectNodes("//Provider").Cast<XmlNode>().ToArray(), node =>
            {
                var blueprintSetID = node.ParentNode;
                var absoluteBlueprintID = blueprintSetID.ParentNode.ParentNode;
                string fname = absoluteBlueprintID.LastChild.InnerText.ToLower();
                if (!String.IsNullOrWhiteSpace(fname) && (Path.GetExtension(fname) == ".xml" || Path.GetExtension(fname) == ".bin"))
                {
                    if (isScenario)
                    {
                        lock (ScenarioDeps)
                        {
                            ScenarioDeps.Add(Railworks.NormalizePath(Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, fname)));
                        }
                    } else
                    {
                        lock (Dependencies)
                        {
                            Dependencies.Add(Railworks.NormalizePath(Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, fname)));
                        }
                    }
                }
            });
            /*foreach (XmlNode node in doc.SelectNodes("//Provider"))
            {
                var blueprintSetID = node.ParentNode;
                var absoluteBlueprintID = blueprintSetID.ParentNode.ParentNode;
                if (String.IsNullOrWhiteSpace(absoluteBlueprintID.LastChild.InnerText))
                    continue;
                yield return Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, absoluteBlueprintID.LastChild.InnerText);
            }*/
        }

        /// <summary>
        /// Get scenarios dependencies
        /// </summary>
        /// <param name="scenariosDir">Scenarios dir</param>
        /// <returns></returns>
        private async Task GetScenariosDependencies(string scenariosDir)
        {
            // Foreach all scenarios
            foreach (string scenarioDir in Directory.GetDirectories(scenariosDir))
            {
                // Foreach all scenario files
                foreach (string file in Directory.GetFiles(scenarioDir, "*.bin", SearchOption.AllDirectories))
                {
                    string xml = Path.ChangeExtension(file, ".xml");

                    // Parse .bin to .xml
                    await Task.Run(() => RunSERZ(file));
                    // Report progress
                    lock (ScenarioDepsLock)
                    {
                        //Dependencies.UnionWith(ParseBlueprint(xml));
                        ParseBlueprint(xml, true);
                        ReportProgress(file);
                    }
                    // Delete temporary .xml file
                    File.Delete(xml);
                }

                // Read scenario properties file
                string scenarioProperties = Path.Combine(scenarioDir, "ScenarioProperties.xml");
                if (File.Exists(scenarioProperties))
                {
                    lock (ScenarioDepsLock)
                    {
                        //Dependencies.UnionWith(ParseBlueprint(scenarioProperties));
                        ParseBlueprint(scenarioProperties, true);
                        ReportProgress(scenarioProperties);
                    }
                }
            }
        }

        /// <summary>
        /// Get network dependencies
        /// </summary>
        /// <param name="path">Network directory path</param>
        /// <returns></returns>
        private async Task GetNetworkDependencies(string path)
        {
            // Foreach all network directories
            foreach (string dir in Directory.GetDirectories(path).Where(x => x.ToLower() == "road tiles" || x.ToLower() == "track tiles" || x.ToLower() == "loft tiles"))
            {
                // Foreach all network .bin files
                foreach (string file in Directory.GetFiles(dir, "*.bin"))
                {
                    string xml = Path.ChangeExtension(file, ".xml");

                    // Parse .bin file to .xml
                    await Task.Run(() => RunSERZ(file));
                    lock (DependenciesLock)
                    {
                        //Dependencies.UnionWith(ParseBlueprint(xml));
                        ParseBlueprint(xml);
                        ReportProgress(file);
                    }
                    // Deletes temporary .xml file
                    File.Delete(xml);
                }
            }
        }

        /// <summary>
        /// Get scenery depencencies
        /// </summary>
        /// <param name="path">Route scenery directory</param>
        /// <returns></returns>
        private async Task GetSceneryDependencies(string path)
        {
            // Foreach all scenery .bin files
            foreach (string file in Directory.GetFiles(path, "*.bin"))
            {
                string xml = Path.ChangeExtension(file, ".xml");

                // Parse .bin file to .xml
                await Task.Run(() => RunSERZ(file));
                lock (DependenciesLock)
                {
                    //Dependencies.UnionWith(ParseBlueprint(xml));
                    ParseBlueprint(xml);
                    ReportProgress(file);
                }
                // Delete temporary .xml file
                File.Delete(xml);
            }
        }

        private void RunSERZ(string file)
        {
            //if (File.ReadAllText(file).Contains(""))
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

        private async Task _GetDependencies()
        {
            bool apChanged = false;

            if (ContainsAP)
            {
                apChanged = GetDirectoryMD5(RoutePath, true) != SavedRoute.APChecksum;
            }

            bool loftsChanged = ((GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Loft Tiles")) != SavedRoute.LoftChecksum) && !ContainsAP) || apChanged;
            bool roadsChanged = ((GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Road Tiles")) != SavedRoute.RoadChecksum) && !ContainsAP) || apChanged;
            bool tracksChanged = ((GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Track Tiles")) != SavedRoute.TrackChecksum) && !ContainsAP) || apChanged;
            bool sceneryChanged = ((GetDirectoryMD5(Path.Combine(RoutePath, "Scenery")) != SavedRoute.SceneryChecksum) && !ContainsAP) || apChanged;
            bool rpChanged = ((GetFileMD5(Path.Combine(RoutePath, "RouteProperties.xml")) != SavedRoute.RoutePropertiesChecksum) && !ContainsAP) || apChanged;
            bool scenariosChanged = ((GetDirectoryMD5(Path.Combine(RoutePath, "Scenarios")) != SavedRoute.ScenariosChecksum) && !ContainsAP) || apChanged;

            var md5 = ComputeChecksums();

            if (SavedRoute.Dependencies != null)
                Dependencies.UnionWith(SavedRoute.Dependencies);

            if (SavedRoute.ScenarioDeps != null)
                ScenarioDeps.UnionWith(SavedRoute.ScenarioDeps);

            if (loftsChanged || roadsChanged || tracksChanged || sceneryChanged || rpChanged || scenariosChanged)
            {
                Task n1 = null;
                Task s1 = null;
                Task t1 = null;
                Task n2 = null;
                Task s2 = null;
                Task t2 = null;
                Task<IEnumerable<string>> prop1 = null;
                Task<IEnumerable<string>> prop2 = null;

                if (rpChanged)
                    prop1 = GetRoutePropertiesDependencies(Path.Combine(RoutePath, "RouteProperties.xml"));
                if (ContainsAP)
                    prop2 = GetRoutePropertiesDependencies(Path.Combine(TempPath, "RouteProperties.xml"));

                foreach (string dir in Directory.GetDirectories(RoutePath))
                {
                    switch (Path.GetFileName(dir))
                    {
                        case "Networks":
                            if (loftsChanged || roadsChanged || tracksChanged)
                                n1 = GetNetworkDependencies(dir);
                            break;
                        case "Scenarios":
                            if (scenariosChanged)
                                s1 = GetScenariosDependencies(dir);
                            break;
                        case "Scenery":
                            if (sceneryChanged)
                                t1 = GetSceneryDependencies(dir);
                            break;
                    }
                }

                if (ContainsAP)
                {
                    foreach (string dir in Directory.GetDirectories(TempPath))
                    {
                        switch (Path.GetFileName(dir))
                        {
                            case "Networks":
                                if (loftsChanged || roadsChanged || tracksChanged)
                                    n2 = GetNetworkDependencies(dir);
                                break;
                            case "Scenarios":
                                if (scenariosChanged)
                                    s2 = GetScenariosDependencies(dir);
                                break;
                            case "Scenery":
                                if (sceneryChanged)
                                    t2 = GetSceneryDependencies(dir);
                                break;
                        }
                    }
                }

                if (n1 != null)
                    await n1;
                if (s1 != null)
                    await s1;
                if (t1 != null)
                    await t1;

                if (n2 != null)
                    await n2;
                if (s2 != null)
                    await s2;
                if (t2 != null)
                    await t2;

                IEnumerable<string> routeProperties1 = new List<string>();
                if (prop1 != null)
                    routeProperties1 = await prop1;

                lock (DependenciesLock)
                {
                    Dependencies.UnionWith(routeProperties1);
                }

                if (ContainsAP)
                {
                    IEnumerable<string> routeProperties2 = new List<string>();
                    if (prop2 != null)
                        routeProperties2 = await prop2;

                    lock (DependenciesLock)
                    {
                        Dependencies.UnionWith(routeProperties2);
                    }
                }

                Dependencies.RemoveWhere(x => string.IsNullOrWhiteSpace(x));

                await md5;

                SavedRoute.Dependencies = Dependencies.ToList();
                SavedRoute.ScenarioDeps = ScenarioDeps.ToList();

                Thread tt = new Thread(() => 
                {
                    RouteSaving?.Invoke(false);
                    Adapter.SaveRoute(SavedRoute);
                    RouteSaving?.Invoke(true);
                });

                tt.Start();
            }
        }

        private async Task ComputeChecksums()
        {
            await Task.Run(() =>
            {
                if (ContainsAP)
                {
                    SavedRoute.APChecksum = GetDirectoryMD5(RoutePath, true);
                }
                else
                {
                    SavedRoute.RoutePropertiesChecksum = GetFileMD5(Path.Combine(RoutePath, "RouteProperties.xml"));
                    SavedRoute.LoftChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Loft Tiles"));
                    SavedRoute.RoadChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Road Tiles"));
                    SavedRoute.TrackChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Track Tiles"));
                    SavedRoute.SceneryChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Scenery"));
                    SavedRoute.ScenariosChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Scenarios"));
                }
            });
        }

        private async Task GetDependencies()
        {
            int count = 0;
            if (ContainsAP && GetDirectoryMD5(RoutePath, true) != SavedRoute.APChecksum) // && GetDirectoryMD5(RoutePath, true) != SavedRoute.APChecksum)
            {
                foreach (string file in Directory.GetFiles(RoutePath, "*.ap", SearchOption.AllDirectories))
                {
                    try {
                        string relFpat = Railworks.GetRelativePath(RoutePath, file);
                        ZipFile.ExtractToDirectory(file, Path.Combine(TempPath, relFpat));
                    } catch {}
                    finally
                    {
                        count++;
                    }
                }
            }

            await _GetDependencies();

            if (count > 0)
            {
                new Task(() => { Railworks.DeleteDirectory(TempPath); }).Start();
            }
        }

        private long CountAllFiles()
        {
            long size = 0;

            bool apChanged = false;

            if (ContainsAP)
            {
                apChanged = GetDirectoryMD5(RoutePath, true) != SavedRoute.APChecksum;
            }

            bool loftsChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Loft Tiles")) != SavedRoute.LoftChecksum;
            bool roadsChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Road Tiles")) != SavedRoute.RoadChecksum;
            bool tracksChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Track Tiles")) != SavedRoute.TrackChecksum;
            bool sceneryChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Scenery")) != SavedRoute.SceneryChecksum;
            bool rpChanged = GetFileMD5(Path.Combine(RoutePath, "RouteProperties.xml")) != SavedRoute.RoutePropertiesChecksum;
            bool scenariosChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Scenarios")) != SavedRoute.ScenariosChecksum;

            if (rpChanged)
                size += GetFileSize(Path.Combine(RoutePath, "RouteProperties.xml"));

            foreach (string dir in Directory.GetDirectories(RoutePath))
            {
                switch (Path.GetFileName(dir))
                {
                    case "Networks":
                        if (loftsChanged || roadsChanged || tracksChanged)
                        {
                            size += GetDirectorySize(Path.Combine(dir, "Loft Tiles"), "*.bin");
                            size += GetDirectorySize(Path.Combine(dir, "Road Tiles"), "*.bin");
                            size += GetDirectorySize(Path.Combine(dir, "Track Tiles"), "*.bin");
                        }

                        break;
                    case "Scenarios":
                        if (scenariosChanged)
                        {
                            foreach (string dir2 in Directory.GetDirectories(dir))
                            {
                                size += GetDirectorySize(dir2, "*.bin");
                                size += GetFileSize(Path.Combine(dir2, "ScenarioProperties.xml"));
                            }
                        }
                        break;
                    case "Scenery":
                        if (sceneryChanged)
                            size += GetDirectorySize(dir, "*.bin");
                        break;
                }
            }

            if (apChanged)
            {
                foreach (string file in Directory.GetFiles(RoutePath, "*.ap", SearchOption.AllDirectories))
                {
                    using (ZipArchive zip = ZipFile.OpenRead(file))
                    {
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            string ext = Path.GetExtension(entry.Name).ToLower();
                            if (ext == ".xml" || ext == ".bin")
                                size += entry.Length;
                        }
                    }
                }
            }

            return size;
        }

        private long GetDirectorySize(string directory, string mask)
        {
            long size = 0;

            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory, mask, SearchOption.AllDirectories))
                {
                    size += GetFileSize(file);
                }
            }

            return size;
        }

        private long GetFileSize(string file)
        {
            if (File.Exists(file))
            {
                FileInfo fi = new FileInfo(file);
                return fi.Length;
            }
            else
                return 0;

        }

        private string GetFileMD5(string path)
        {
            if (!File.Exists(path))
                return null;

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

        private string GetDirectoryMD5(string path, bool isAP = false)
        {
            if (!Directory.Exists(path))
                return null;
            
            var filePaths = isAP ? Directory.GetFiles(path, "*.ap", SearchOption.AllDirectories).OrderBy(p => p).ToArray() : Directory.GetFiles(path, "*.bin").OrderBy(p => p).ToArray();

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

        public void ParseRouteMissingAssets(HashSet<string> missingAll)
        {
            MissingDependencies = Dependencies.Intersect(missingAll).ToList();
            MissingScenarioDeps = ScenarioDeps.Intersect(missingAll).ToList();
        }

        public void ParseRouteDownloadableAssets(HashSet<string> downloadableAll)
        {
            DownloadableDependencies = MissingDependencies.Intersect(downloadableAll).ToList();
            DownloadableScenarioDeps = MissingScenarioDeps.Intersect(downloadableAll).ToList();
        }
    }
}
