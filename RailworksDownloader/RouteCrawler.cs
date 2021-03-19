using ICSharpCode.SharpZipLib.Zip;
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
using static RailworksDownloader.Utils;

namespace RailworksDownloader
{
    /// <summary>
    /// Crawles specific route
    /// </summary>
    public class RouteCrawler
    {
        // Route path
        private readonly string RoutePath;
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
        public RouteCrawler(string path, HashSet<string> dependencies, HashSet<string> scenarioDeps)
        {
            RoutePath = path;
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
            Task t = Task.Run(async () =>
            {
                // If route directory exists
                if (Directory.Exists(RoutePath))
                {
                    try
                    {
                        // Counts size of all files
                        AllFilesSize = CountAllFiles();

                        // Find all dependencies
                        await GetDependencies();
                    }
                    catch (Exception e)
                    {
                        if (e.GetType() != typeof(ThreadInterruptedException) && e.GetType() != typeof(ThreadAbortException))
                            Trace.Assert(false, $"Error when crawling route \"{RoutePath}\":\n{e}");
                    }

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

        /// <summary>
        /// Report route progress
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private void ReportProgress(string file)
        {
            ReportProgress(GetFileSize(file));
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
        /// Get single file dependencies
        /// </summary>
        /// <param name="blueprintPath">Route properties path</param>
        /// <returns></returns>
        private async Task GetSingleFileDependencies(string blueprintPath)
        {
            await Task.Run(() =>
            {
                // Check if route properties exists
                if (File.Exists(blueprintPath))
                {
                    using (Stream fs = File.OpenRead(blueprintPath))
                    {
                        ParseBlueprint(fs);
                    }
                }
            });
        }

        /// <summary>
        /// Parse blueprint file
        /// </summary>
        /// <param name="blueprintPath">Blueprint file path</param>
        /// <param name="isScenario">Is scenario file</param>
        /// <returns></returns>
        private void ParseBlueprint(string blueprintPath, bool isScenario = false)
        {
            // Check if blueprint exists
            if (File.Exists(blueprintPath))
            {
                using (Stream fs = File.OpenRead(blueprintPath))
                {
                    ParseBlueprint(fs, isScenario);
                }
            }
        }

        /// <summary>
        /// Parse blueprint file
        /// </summary>
        /// <param name="stream">Blueprint stream</param>
        /// <param name="isScenario">Is scenario file</param>
        /// <returns></returns>
        private void ParseBlueprint(Stream istream, bool isScenario = false)
        {
            Stream stream = new MemoryStream();
            istream.CopyTo(stream);
            istream.Close();
            stream.Seek(0, SeekOrigin.Begin);

            if (stream.Length > 4)
            {
                if (Utils.CheckIsSerz(stream))
                {
                    SerzReader sr = new SerzReader(stream);

                    if (isScenario)
                        lock (ScenarioDeps)
                            ScenarioDeps.UnionWith(sr.GetDependencies());
                    else
                        lock (Dependencies)
                            Dependencies.UnionWith(sr.GetDependencies());
                }
                else
                {
                    ParseXMLBlueprint(stream, isScenario);
                }
            }
        }

        /// <summary>
        /// Parse XML blueprint file
        /// </summary>
        /// <param name="stream">XML blueprint stream</param>
        /// <param name="isScenario">Is scenario file</param>
        /// <returns></returns>
        private void ParseXMLBlueprint(Stream stream, bool isScenario = false)
        {
            // Load blueprint file
            XmlDocument doc = new XmlDocument();

            try
            {
                doc.Load(XmlReader.Create(RemoveInvalidXmlChars(stream), new XmlReaderSettings() { CheckCharacters = false }));
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
                string ext = Path.GetExtension(fname).ToLower();
                if (!string.IsNullOrWhiteSpace(fname) && (ext == ".xml" || ext == ".bin"))
                {
                    if (isScenario)
                        lock (ScenarioDeps)
                            ScenarioDeps.Add(NormalizePath(Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, fname)));
                    else
                        lock (Dependencies)
                            Dependencies.Add(NormalizePath(Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, fname)));
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
                    int max = dirs.Length * (workerId + 1) / maxThreads;
                    for (int i = dirs.Length * workerId / maxThreads; i < max; i++)
                    {
                        // Foreach all scenario files
                        foreach (string file in Directory.GetFiles(dirs[i], "*", SearchOption.AllDirectories))
                        {
                            string ext = Path.GetExtension(file).ToLower();
                            if (ext == ".xml" || ext == ".bin")
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
        /// Get tiles dependencies
        /// </summary>
        /// <param name="path">Directory path</param>
        /// <returns></returns>
        private async Task GetTilesDependencies(string dir)
        {
            await Task.Run(() =>
            {
                // Foreach all tiles files
                foreach (string file in Directory.GetFiles(dir))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".xml" || ext == ".bin")
                    {
                        ParseBlueprint(file);
                        ReportProgress(file);
                    }
                }
            });
        }

        private async Task GetDependenciesInternal()
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
                Task prop = null;

                if (rpChanged)
                    prop = GetSingleFileDependencies(Utils.FindFile(RoutePath, "RouteProperties.*"));

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
                                            n1 = GetTilesDependencies(network_dir);
                                        break;
                                    case "road tiles":
                                        if (roadsChanged)
                                            n2 = GetTilesDependencies(network_dir);
                                        break;
                                    case "track tiles":
                                        if (tracksChanged)
                                            n3 = GetTilesDependencies(network_dir);
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
                                t = GetTilesDependencies(dir);
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
                if (prop != null)
                    await prop;
            }

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

        private void ParseAPEntry(ZipFile file, ZipEntry entry, bool isScenario = false)
        {
            try
            {
                Stream inputStream = file.GetInputStream(entry);

                string ext = Path.GetExtension(entry.Name).ToLower();
                if (inputStream.CanRead && (ext == ".xml" || ext == ".bin"))
                {
                    ParseBlueprint(inputStream, isScenario);

                    ReportProgress(entry.Size);
                }
            }
            catch (Exception e)
            {
                if (e.GetType() != typeof(ThreadInterruptedException) && e.GetType() != typeof(ThreadAbortException))
                    Trace.Assert(false, $"Error when reading gzip entry of file \"{file.Name}\":\n{e}");
            }
        }

        private async Task GetDependencies()
        {
            //int count = 0;
            if (ContainsAP && GetDirectoryMD5(RoutePath, true) != SavedRoute.APChecksum) // && GetDirectoryMD5(RoutePath, true) != SavedRoute.APChecksum)
            {
                Parallel.ForEach(Directory.GetFiles(RoutePath, "*.ap", SearchOption.AllDirectories), file =>
                {
                    try
                    {
                        using (ZipFile zipFile = new ZipFile(file))
                        {
                            if (zipFile.TestArchive(true, TestStrategy.FindFirstError, null))
                            {
                                //.Where(x => Path.GetExtension(x.FileName).ToLower() == ".xml" || Path.GetExtension(x.FileName).ToLower() == ".bin")
                                foreach (ZipEntry entry in zipFile)
                                {
                                    if (Path.GetExtension(entry.Name).ToLower() == ".xml" || Path.GetExtension(entry.Name).ToLower() == ".bin")
                                    {
                                        string relativePath = NormalizePath(GetRelativePath(RoutePath, Path.Combine(Path.GetDirectoryName(file), entry.Name)));
                                        string mainFolder = relativePath.Split(Path.DirectorySeparatorChar)[0];
                                        if (mainFolder == "networks" || mainFolder == "scenarios" || mainFolder == "scenery")
                                        {
                                            switch (mainFolder)
                                            {
                                                case "networks":
                                                    string subFolder = relativePath.Split(Path.DirectorySeparatorChar)[1];
                                                    if (subFolder == "loft tiles" || subFolder == "road tiles" || subFolder == "track tiles")
                                                        ParseAPEntry(zipFile, entry, false);
                                                    break;
                                                case "scenarios":
                                                    ParseAPEntry(zipFile, entry, true);
                                                    break;
                                                case "scenery":
                                                    ParseAPEntry(zipFile, entry, false);
                                                    break;
                                            }
                                        }
                                        else if (Path.GetFileName(entry.Name).ToLower().Contains("routeproperties"))
                                        {
                                            ParseAPEntry(zipFile, entry, false);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Trace.Assert(false, $"Gzip file \"{file}\" is corrupted!");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.GetType() != typeof(ThreadInterruptedException) && e.GetType() != typeof(ThreadAbortException))
                            Trace.Assert(false, $"Error while reading gzip file: \"{file}\":\n{e}");
                    }
                });
            }

            await GetDependenciesInternal();
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
                    try
                    {
                        using (ZipFile zip = new ZipFile(file))
                        {
                            foreach (ZipEntry entry in zip)
                            {
                                string ext = Path.GetExtension(entry.Name).ToLower();
                                if (ext == ".xml" || ext == ".bin")
                                {
                                    string relativePath = NormalizePath(GetRelativePath(RoutePath, Path.Combine(Path.GetDirectoryName(file), entry.Name)));
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
                    catch
                    {
                        Trace.Assert(false, $"Error while counting size of file: \"{file}\"!");
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
                    ulong freeMem = Math.Min(new ComputerInfo().AvailablePhysicalMemory, (ulong)(0x100000000 - MemoryInformation.GetMemoryUsageForProcess(nProcessID)));
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
