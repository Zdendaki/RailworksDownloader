using ICSharpCode.SharpZipLib.Zip;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace RailworksDownloader
{
    /// <summary>
    /// Crawles specific route
    /// </summary>
    public class RouteCrawler
    {
        // Route path
        private readonly string RoutePath;
        // Railworks path
        private readonly string RailworksPath;
        // Size of all route files
        private long AllFilesSize = 0;
        // Size of processed route files
        private long Progress = 0;
        // Thread locks
        private readonly object ProgressLock = new object();
        // Total crawling process (%)
        internal float PercentProgress = 0f;
        // Loaded route data
        private LoadedRoute SavedRoute;
        // Database adapter
        private readonly SqLiteAdapter Adapter;

        /// <summary>
        /// Is route from asset pack
        /// </summary>
        internal bool ContainsAP => Directory.GetFiles(RoutePath, "*.ap", SearchOption.AllDirectories).Any();

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
        public HashSet<string> ScenarioDeps { get; set; }

        /// <summary>
        /// Initializes route crawler
        /// </summary>
        /// <param name="path">Route path</param>
        /// <param name="railworksPath">RailWorks path</param>
        public RouteCrawler(string path, string railworksPath, HashSet<string> dependencies, HashSet<string> scenarioDeps)
        {
            RoutePath = path;
            RailworksPath = railworksPath;
            Dependencies = dependencies;
            ScenarioDeps = scenarioDeps;
            Adapter = new SqLiteAdapter(Path.Combine(RoutePath, "cache.dls"));
            SavedRoute = Adapter.LoadSavedRoute(ContainsAP);
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
        public void Start()
        {
            try
            {
                Task t = Task.Run(async () =>
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
                    }

                    // Crawling complete event
                    Complete?.Invoke();
                });
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

            Debug.Assert(Progress <= AllFilesSize, "Fatal, Progress is bigger than size of all files! " + Progress + ":" + AllFilesSize + "\nRoute: " + RoutePath);
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
        private void ReportProgress(long fsize)
        {
            float delta;

            lock (ProgressLock)
            {
                Progress += fsize;
            }

            Debug.Assert(Progress <= AllFilesSize, "Fatal, Progress is bigger than size of all files! " + Progress + ":" + AllFilesSize + "\nRoute: " + RoutePath);
            if (Progress <= AllFilesSize)
            {

                if (AllFilesSize > 0)
                {
                    PercentProgress = Progress * 100f / AllFilesSize;
                    delta = (float)(fsize * 100.0f / AllFilesSize);
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
                XmlNode absoluteBlueprintID = blueprintIDNode.FirstChild;
                XmlNode blueprintSetID = absoluteBlueprintID.FirstChild.FirstChild;
                string fname = absoluteBlueprintID.LastChild.InnerText.ToLower();
                if (string.IsNullOrWhiteSpace(fname) || !(Path.GetExtension(fname) == ".xml" || Path.GetExtension(fname) == ".bin"))
                    return string.Empty;
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
        private async Task<List<string>> GetRoutePropertiesDependencies(string propertiesPath)
        {
            // Check if route properties exists
            if (!File.Exists(propertiesPath))
                return new List<string>();

            List<string> dependencies = new List<string>();

            // Load route properties file
            XmlDocument doc = new XmlDocument();

            doc.Load(XmlReader.Create(Railworks.RemoveInvalidXmlChars(propertiesPath), new XmlReaderSettings() { CheckCharacters = false }));

            XmlElement root = doc.DocumentElement;

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
            foreach (XmlNode node in doc.SelectNodes("//Provider").Cast<XmlNode>().ToArray())
            {
                XmlNode blueprintSetID = node.ParentNode;
                XmlNode absoluteBlueprintID = blueprintSetID.ParentNode.ParentNode;
                string fname = absoluteBlueprintID.LastChild.InnerText.ToLower();
                if (!string.IsNullOrWhiteSpace(fname) && (Path.GetExtension(fname) == ".xml" || Path.GetExtension(fname) == ".bin"))
                {
                    if (isScenario)
                        lock (ScenarioDeps)
                            ScenarioDeps.Add(Railworks.NormalizePath(Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, fname)));
                    else
                        lock (Dependencies)
                            Dependencies.Add(Railworks.NormalizePath(Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, fname)));
                }
            }
        }

        private void ParseBlueprint(Stream stream, bool isScenario = false)
        {

            // Load blueprint file
            XmlDocument doc = new XmlDocument();

            try
            {
                doc.Load(XmlReader.Create(Railworks.RemoveInvalidXmlChars(stream), new XmlReaderSettings() { CheckCharacters = false }));
            }
            catch
            {
                return;
            }

            foreach (XmlNode node in doc.SelectNodes("//Provider").Cast<XmlNode>().ToArray())
            {
                XmlNode blueprintSetID = node.ParentNode;
                XmlNode absoluteBlueprintID = blueprintSetID.ParentNode.ParentNode;
                string fname = absoluteBlueprintID.LastChild.InnerText.ToLower();
                if (!string.IsNullOrWhiteSpace(fname) && (Path.GetExtension(fname) == ".xml" || Path.GetExtension(fname) == ".bin"))
                {
                    if (isScenario)
                        lock (ScenarioDeps)
                            ScenarioDeps.Add(Railworks.NormalizePath(Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, fname)));
                    else
                        lock (Dependencies)
                            Dependencies.Add(Railworks.NormalizePath(Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, fname)));
                }
            }
        }

        /// <summary>
        /// Get scenarios dependencies
        /// </summary>
        /// <param name="scenariosDir">Scenarios dir</param>
        /// <returns></returns>
        private async Task GetScenariosDependencies(string scenariosDir)
        {
            await Task.Run(() =>
            {
                string[] dirs = Directory.GetDirectories(scenariosDir);

                int maxThreads = Math.Min(Environment.ProcessorCount, dirs.Length);
                Parallel.For(0, maxThreads, workerId =>
                {
                    var max = dirs.Length * (workerId + 1) / maxThreads;
                    for (int i = dirs.Length * workerId / maxThreads; i < max; i++)
                    {
                        // Foreach all scenario files
                        foreach (string file in Directory.GetFiles(dirs[i], "*", SearchOption.AllDirectories))
                        {
                            string ext = Path.GetExtension(file).ToLower();
                            if (ext == ".bin")
                            {
                                SerzReader sr = new SerzReader(file);

                                lock (ScenarioDeps)
                                    ScenarioDeps.UnionWith(sr.GetDependencies());

                                ReportProgress(file);
                            }
                            else if (ext == ".xml")
                            {
                                ParseBlueprint(file, true);

                                ReportProgress(file);
                            }
                        }
                    }
                });
            });
        }

        /// <summary>
        /// Get network dependencies
        /// </summary>
        /// <param name="path">Network directory path</param>
        /// <returns></returns>
        private async Task GetNetworkDependencies(string dir)
        {
            await Task.Run(() =>
            {
                // Foreach all Network files
                foreach (string file in Directory.GetFiles(dir, "*.bin"))
                {
                    SerzReader sr = new SerzReader(file);

                    lock (Dependencies)
                        Dependencies.UnionWith(sr.GetDependencies());

                    ReportProgress(file);
                }
            });
        }

        /// <summary>
        /// Get scenery depencencies
        /// </summary>
        /// <param name="path">Route scenery directory</param>
        /// <returns></returns>
        private async Task GetSceneryDependencies(string path)
        {
            await Task.Run(() =>
            {
                // Foreach all Network files
                foreach (string file in Directory.GetFiles(path, "*.bin"))
                {
                    SerzReader sr = new SerzReader(file);

                    lock (Dependencies)
                        Dependencies.UnionWith(sr.GetDependencies());

                    ReportProgress(file);
                }
            });
        }

        private async Task _GetDependencies()
        {
            bool loftsChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Loft Tiles")) != SavedRoute.LoftChecksum;
            bool roadsChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Road Tiles")) != SavedRoute.RoadChecksum;
            bool tracksChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Track Tiles")) != SavedRoute.TrackChecksum;
            bool sceneryChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Scenery")) != SavedRoute.SceneryChecksum;
            bool rpChanged = GetFileMD5(Path.Combine(RoutePath, "RouteProperties.xml")) != SavedRoute.RoutePropertiesChecksum;
            bool scenariosChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Scenarios")) != SavedRoute.ScenariosChecksum;

            Task md5 = ComputeChecksums();

            lock (Dependencies)
            {
                if (SavedRoute.Dependencies != null)
                    lock (Dependencies)
                        Dependencies.UnionWith(SavedRoute.Dependencies);

                if (SavedRoute.ScenarioDeps != null)
                    lock (ScenarioDeps)
                        ScenarioDeps.UnionWith(SavedRoute.ScenarioDeps);
            }

            if (loftsChanged || roadsChanged || tracksChanged || sceneryChanged || rpChanged || scenariosChanged)
            {
                Task n1 = null;
                Task n2 = null;
                Task n3 = null;
                Task s = null;
                Task t = null;
                Task<List<string>> prop = null;

                if (rpChanged)
                    prop = GetRoutePropertiesDependencies(Path.Combine(RoutePath, "RouteProperties.xml"));
                
                Parallel.ForEach(Directory.GetDirectories(RoutePath), dir =>
                {
                    switch (Path.GetFileName(dir).ToLower())
                    {
                        case "networks":
                            // Foreach all network directories
                            Parallel.ForEach(Directory.GetDirectories(dir), network_dir =>
                            {
                                switch (Path.GetFileName(network_dir).ToLower())
                                {
                                    case "loft tiles":
                                        if (loftsChanged)
                                            n1 = GetNetworkDependencies(network_dir);
                                        break;
                                    case "road tiles":
                                        if (roadsChanged)
                                            n2 = GetNetworkDependencies(network_dir);
                                        break;
                                    case "track tiles":
                                        if (tracksChanged)
                                            n3 = GetNetworkDependencies(network_dir);
                                        break;
                                }
                            });
                            break;
                        case "scenarios":
                            if (scenariosChanged)
                                s = GetScenariosDependencies(dir);
                            break;
                        case "scenery":
                            if (sceneryChanged)
                                t = GetSceneryDependencies(dir);
                            break;
                    }
                });

                if (n1 != null)
                    await n1;
                if (n2 != null)
                    await n2;
                if (n3 != null)
                    await n3;
                if (s != null)
                    await s;
                if (t != null)
                    await t;

                List<string> routeProperties = new List<string>();
                if (prop != null)
                {
                    routeProperties = await prop;

                    lock (Dependencies)
                        Dependencies.UnionWith(routeProperties);
                }
            }

            //Dependencies.RemoveBlank();

            await md5;

            SavedRoute.Dependencies = Dependencies;
            SavedRoute.ScenarioDeps = ScenarioDeps;

            Thread tt = new Thread(() =>
            {
                RouteSaving?.Invoke(false);
                Adapter.SaveRoute(SavedRoute);
                Adapter.FlushToFile();
                RouteSaving?.Invoke(true);
            });

            tt.Start();
        }

        private async Task ComputeChecksums()
        {
            await Task.Run(() =>
            {
                if (ContainsAP)
                    SavedRoute.APChecksum = GetDirectoryMD5(RoutePath, true);
                SavedRoute.RoutePropertiesChecksum = GetFileMD5(Path.Combine(RoutePath, "RouteProperties.xml"));
                SavedRoute.LoftChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Loft Tiles"));
                SavedRoute.RoadChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Road Tiles"));
                SavedRoute.TrackChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Track Tiles"));
                SavedRoute.SceneryChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Scenery"));
                SavedRoute.ScenariosChecksum = GetDirectoryMD5(Path.Combine(RoutePath, "Scenarios"));
            });
        }

        private void ParseAPEntry(ZipFile file, ZipEntry entry, bool isScenario = false, string fname = "")
        {
            try
            {
                Stream inputStream = file.GetInputStream(entry);

                if (inputStream.CanRead && inputStream.CanSeek)
                {
                    string ext = Path.GetExtension(entry.Name).ToLower();
                    if (ext == ".xml")
                    {
                        ParseBlueprint(inputStream, isScenario);
                    }
                    else if (ext == ".bin")
                    {
                        SerzReader sr = new SerzReader(inputStream);

                        if (isScenario)
                            lock (ScenarioDeps)
                                ScenarioDeps.UnionWith(sr.GetDependencies());
                        else
                            lock (Dependencies)
                                Dependencies.UnionWith(sr.GetDependencies());
                    }

                    ReportProgress(entry.Size);
                }
            }
            catch (Exception e)
            {
                Desharp.Debug.Log(e);
                Debug.Assert(false, "Nastala kritická chyba při čtení souboru ZIP Entry!!!");
            }
        }

        private async Task GetDependencies()
        {
            //int count = 0;
            if (ContainsAP && GetDirectoryMD5(RoutePath, true) != SavedRoute.APChecksum) // && GetDirectoryMD5(RoutePath, true) != SavedRoute.APChecksum)
            {
                Parallel.ForEach(Directory.GetFiles(RoutePath, "*.ap", SearchOption.AllDirectories), file =>
                {
                    using (ZipFile zipFile = new ZipFile(file))
                    {
                        try
                        {
                            if (zipFile.TestArchive(true, TestStrategy.FindFirstError, null))
                            {
                                //.Where(x => Path.GetExtension(x.FileName).ToLower() == ".xml" || Path.GetExtension(x.FileName).ToLower() == ".bin")
                                foreach (ZipEntry entry in zipFile)
                                {
                                    if (Path.GetExtension(entry.Name).ToLower() == ".xml" || Path.GetExtension(entry.Name).ToLower() == ".bin")
                                    {
                                        string relativePath = Railworks.NormalizePath(Railworks.GetRelativePath(RoutePath, Path.Combine(Path.GetDirectoryName(file), entry.Name)));
                                        string mainFolder = relativePath.Split(Path.DirectorySeparatorChar)[0];
                                        if (mainFolder == "networks" || mainFolder == "scenarios" || mainFolder == "scenery")
                                        {
                                            switch (mainFolder)
                                            {
                                                case "networks":
                                                    string subFolder = relativePath.Split(Path.DirectorySeparatorChar)[1];
                                                    if (subFolder == "loft tiles" || subFolder == "road tiles" || subFolder == "track tiles")
                                                        ParseAPEntry(zipFile, entry, false, file);
                                                    break;
                                                case "scenarios":
                                                    ParseAPEntry(zipFile, entry, true, file);
                                                    break;
                                                case "scenery":
                                                    ParseAPEntry(zipFile, entry, false, file);
                                                    break;
                                            }
                                        }
                                        else if (Path.GetFileName(entry.Name).ToLower().Contains("routeproperties"))
                                        {
                                            ParseAPEntry(zipFile, entry, false, file);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Desharp.Debug.Log(e);
                            Debug.Assert(false, "Nastala kritická chyba při čtení souboru ZIP!!!");
                        }
                    }
                });
            }

            await _GetDependencies();
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
            {
                size += GetFileSize(Path.Combine(RoutePath, "RouteProperties.xml"));
            }

            string[] commonMask = new string[] { "*.bin", "*.xml" };

            foreach (string dir in Directory.GetDirectories(RoutePath))
            {
                switch (Path.GetFileName(dir).ToLower())
                {
                    case "networks":
                        if (loftsChanged)
                            size += GetDirectorySize(Path.Combine(dir, "Loft Tiles"), commonMask);

                        if (roadsChanged)
                            size += GetDirectorySize(Path.Combine(dir, "Road Tiles"), commonMask);

                        if (tracksChanged)
                            size += GetDirectorySize(Path.Combine(dir, "Track Tiles"), commonMask);

                        break;
                    case "scenarios":
                        if (scenariosChanged)
                        {
                            foreach (string dir2 in Directory.GetDirectories(dir))
                            {
                                size += GetDirectorySize(dir2, commonMask, SearchOption.AllDirectories);
                            }
                        }
                        break;
                    case "scenery":
                        if (sceneryChanged)
                            size += GetDirectorySize(dir, commonMask);
                        break;
                }
            }

            if (apChanged)
            {
                foreach (string file in Directory.GetFiles(RoutePath, "*.ap", SearchOption.AllDirectories))
                {
                    using (ZipFile zip = new ZipFile(file))
                    {
                        foreach (ZipEntry entry in zip)
                        {
                            string ext = Path.GetExtension(entry.Name).ToLower();
                            if (ext == ".xml" || ext == ".bin")
                            {
                                string relativePath = Railworks.NormalizePath(Railworks.GetRelativePath(RoutePath, Path.Combine(Path.GetDirectoryName(file), entry.Name)));
                                string mainFolder = relativePath.Split(Path.DirectorySeparatorChar)[0];
                                if (mainFolder == "scenarios" || mainFolder == "scenery")
                                {
                                    size += entry.Size;
                                }
                                else if (mainFolder == "networks")
                                {
                                    string subFolder = relativePath.Split(Path.DirectorySeparatorChar)[1];
                                    if (subFolder == "loft tiles" || subFolder == "road tiles" || subFolder == "track tiles")
                                    {
                                        size += entry.Size;
                                    }
                                }
                                else if (Path.GetFileName(entry.Name).ToLower().Contains("routeproperties"))
                                {
                                    size += entry.Size;
                                }
                            }
                        }
                    }
                }
            }

            return size;
        }

        private long GetDirectorySize(string directory, string[] maskArr, SearchOption so = SearchOption.TopDirectoryOnly)
        {
            long size = 0;

            for (int i = 0; i < maskArr.Length; i++)
            {
                if (Directory.Exists(directory))
                {
                    foreach (string file in Directory.GetFiles(directory, maskArr[i], so))
                    {
                        size += GetFileSize(file);
                    }
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

            using (MD5 md5 = MD5.Create())
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

            int nProcessID = Process.GetCurrentProcess().Id;
            string[] filePaths = isAP ? Directory.GetFiles(path, "*.ap", SearchOption.AllDirectories).OrderBy(p => p).ToArray() : Directory.GetFiles(path, "*.bin").OrderBy(p => p).ToArray();

            using (MD5 md5 = MD5.Create())
            {
                foreach (string filePath in filePaths)
                {
                    byte[] pathBytes = Encoding.UTF8.GetBytes(filePath);
                    md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                    /*ulong fsize = (ulong)GetFileSize(filePath);
                    ulong freeMem = Math.Min(new ComputerInfo().AvailablePhysicalMemory, (ulong)(0x100000000 - Railworks.MemoryInformation.GetMemoryUsageForProcess(nProcessID)));
                    if (fsize > 0x1F400000 || (new ComputerInfo().AvailablePhysicalMemory < fsize*20)) //File bigger than 10MB or total memory used > 2GB - read chunk by chunk
                    {
                        const int buffSize = 0x402000;
                        byte[] buffer = new byte[buffSize];
                        using (FileStream inputStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            int read;
                            while ((read = inputStream.Read(buffer, 0, buffSize)) > 0)
                            {
                                md5.TransformBlock(buffer, 0, read, buffer, 0);
                            }
                        }
                    }
                    else
                    {
                        byte[] fileBytes = File.ReadAllBytes(filePath);
                        md5.TransformBlock(fileBytes, 0, fileBytes.Length, fileBytes, 0);
                    }*/

                    try
                    {
                        byte[] fileBytes = File.ReadAllBytes(filePath);
                        md5.TransformBlock(fileBytes, 0, fileBytes.Length, fileBytes, 0);
                    }
                    catch
                    {
                        const int buffSize = 0x402000;
                        byte[] buffer = new byte[buffSize];
                        using (FileStream inputStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            int read;
                            while ((read = inputStream.Read(buffer, 0, buffSize)) > 0)
                            {
                                md5.TransformBlock(buffer, 0, read, buffer, 0);
                            }
                        }
                    }
                }

                md5.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(md5.Hash).Replace("-", "").ToLower();
            }
        }
    }
}
