using ICSharpCode.SharpZipLib.Zip;
using Sentry;
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

        private bool APchanged { get; set; }
        private bool LoftsChanged { get; set; }
        private bool RoadsChanged { get; set; }
        private bool TracksChanged { get; set; }
        private bool SceneryChanged { get; set; }
        private bool RPchanged { get; set; }
        private bool ScenariosChanged { get; set; }

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
            SavedRoute = Adapter.LoadSavedRoute();
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
                        {
                            SentrySdk.CaptureException(e);
                            Trace.Assert(false, string.Format(Localization.Strings.CrawlingRouteFail, RoutePath), e.ToString());
                        }
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

            Debug.Assert(Progress <= AllFilesSize, string.Format(Localization.Strings.ProgressFail, Progress, AllFilesSize, RoutePath));
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
                        ParseBlueprint(fs, blueprintPath);
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
                    ParseBlueprint(fs, blueprintPath, isScenario);
                }
            }
        }

        /// <summary>
        /// Parse blueprint file
        /// </summary>
        /// <param name="stream">Blueprint stream</param>
        /// <param name="isScenario">Is scenario file</param>
        /// <returns></returns>
        private void ParseBlueprint(Stream istream, string debugFname, bool isScenario = false)
        {
            MemoryStream stream = new MemoryStream();
            istream.CopyTo(stream);
            istream.Close();
            stream.Seek(0, SeekOrigin.Begin);

            if (stream.Length > 4)
            {
                if (CheckIsSerz(stream))
                {
                    SerzReader sr = new SerzReader(stream, debugFname);

                    if (isScenario)
                        lock (ScenarioDeps)
                            ScenarioDeps.UnionWith(sr.GetDependencies());
                    else
                        lock (Dependencies)
                            Dependencies.UnionWith(sr.GetDependencies());
                }
                else if (CheckIsXML(stream))
                {
                    ParseXMLBlueprint(stream, debugFname, isScenario);
                }
            }
        }

        /// <summary>
        /// Parse XML blueprint file
        /// </summary>
        /// <param name="stream">XML blueprint stream</param>
        /// <param name="isScenario">Is scenario file</param>
        /// <returns></returns>
        private void ParseXMLBlueprint(Stream stream, string debugFname, bool isScenario = false)
        {
            // Load blueprint file
            XmlDocument doc = new XmlDocument();

            try
            {
                doc.Load(XmlReader.Create(RemoveInvalidXmlChars(stream), new XmlReaderSettings() { CheckCharacters = false }));
            }
            catch (Exception e)
            {
                SentrySdk.WithScope(scope =>
                {
                    scope.AddAttachment(stream, debugFname);
                    SentrySdk.CaptureException(e);
                });
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
            /*bool loftsChanged = GetDirectoryChanged(Path.Combine(RoutePath, "Networks", "Loft Tiles"), ref SavedRoute.LoftLastWrite, ref SavedRoute.LoftChecksum);
            bool roadsChanged = GetDirectoryChanged(Path.Combine(RoutePath, "Networks", "Road Tiles"), ref SavedRoute.RoadLastWrite, ref SavedRoute.LoftChecksum);
            bool tracksChanged = GetDirectoryChanged(Path.Combine(RoutePath, "Networks", "Track Tiles"), ref SavedRoute.TrackLastWrite, ref SavedRoute.TrackChecksum);
            bool sceneryChanged = GetDirectoryChanged(Path.Combine(RoutePath, "Scenery"), ref SavedRoute.SceneryLastWrite, ref SavedRoute.SceneryChecksum);
            bool rpChanged = GetFileChanged(Path.Combine(RoutePath, "RouteProperties.xml"), ref SavedRoute.RoutePropertiesLastWrite, ref SavedRoute.RoutePropertiesChecksum);
            bool scenariosChanged = GetDirectoryChanged(Path.Combine(RoutePath, "Scenarios"), ref SavedRoute.ScenariosLastWrite, ref SavedRoute.ScenariosChecksum);*/

            lock (Dependencies)
            {
                if (SavedRoute.Dependencies != null)
                    lock (Dependencies)
                        Dependencies.UnionWith(SavedRoute.Dependencies);

                if (SavedRoute.ScenarioDeps != null)
                    lock (ScenarioDeps)
                        ScenarioDeps.UnionWith(SavedRoute.ScenarioDeps);
            }

            if (LoftsChanged || RoadsChanged || TracksChanged || SceneryChanged || RPchanged || ScenariosChanged)
            {
                Task n1 = null;
                Task n2 = null;
                Task n3 = null;
                Task s = null;
                Task t = null;
                Task prop = null;

                if (RPchanged)
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
                                        if (LoftsChanged)
                                            n1 = GetTilesDependencies(network_dir);
                                        break;
                                    case "road tiles":
                                        if (RoadsChanged)
                                            n2 = GetTilesDependencies(network_dir);
                                        break;
                                    case "track tiles":
                                        if (TracksChanged)
                                            n3 = GetTilesDependencies(network_dir);
                                        break;
                                }
                            });
                            break;
                        case "scenarios":
                            if (ScenariosChanged)
                                s = GetScenariosDependencies(dir);
                            break;
                        case "scenery":
                            if (SceneryChanged)
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

        private void ParseAPEntry(ZipFile file, ZipEntry entry, bool isScenario = false)
        {
            try
            {
                Stream inputStream = file.GetInputStream(entry);

                string ext = Path.GetExtension(entry.Name).ToLower();
                if (inputStream.CanRead && (ext == ".xml" || ext == ".bin"))
                {
                    ParseBlueprint(inputStream, Path.Combine(file.Name, entry.Name), isScenario);

                    ReportProgress(entry.Size);
                }
            }
            catch (Exception e)
            {
                if (e.GetType() != typeof(ThreadInterruptedException) && e.GetType() != typeof(ThreadAbortException))
                {
                    SentrySdk.CaptureException(e);
                    Trace.Assert(false, string.Format(Localization.Strings.GzipEntryFail, file.Name), e.ToString());
                }
            }
        }

        private async Task GetDependencies()
        {
            //int count = 0;
            if (ContainsAP && GetDirectoryChanged(RoutePath, ref SavedRoute.APLastWrite, ref SavedRoute.APChecksum, true)) // && GetDirectoryMD5(RoutePath, true) != SavedRoute.APChecksum)
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
                                Trace.Assert(false, string.Format(Localization.Strings.GzipFileFail, file));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.GetType() != typeof(ThreadInterruptedException) && e.GetType() != typeof(ThreadAbortException))
                        {
                            SentrySdk.CaptureException(e);
                            Trace.Assert(false, string.Format(Localization.Strings.GzipReadFail, file), e.ToString());
                        }
                    }
                });
            }

            await GetDependenciesInternal();
        }

        private long CountAllFiles()
        {
            long size = 0;

            APchanged = false;

            if (ContainsAP)
            {
                APchanged = GetDirectoryMD5(RoutePath, true) != SavedRoute.APChecksum;
            }

            LoftsChanged = GetDirectoryChanged(Path.Combine(RoutePath, "Networks", "Loft Tiles"), ref SavedRoute.LoftLastWrite, ref SavedRoute.LoftChecksum);
            RoadsChanged = GetDirectoryChanged(Path.Combine(RoutePath, "Networks", "Road Tiles"), ref SavedRoute.RoadLastWrite, ref SavedRoute.LoftChecksum);
            TracksChanged = GetDirectoryChanged(Path.Combine(RoutePath, "Networks", "Track Tiles"), ref SavedRoute.TrackLastWrite, ref SavedRoute.TrackChecksum);
            SceneryChanged = GetDirectoryChanged(Path.Combine(RoutePath, "Scenery"), ref SavedRoute.SceneryLastWrite, ref SavedRoute.SceneryChecksum);
            RPchanged = GetFileChanged(Path.Combine(RoutePath, "RouteProperties.xml"), ref SavedRoute.RoutePropertiesLastWrite, ref SavedRoute.RoutePropertiesChecksum);
            ScenariosChanged = GetDirectoryChanged(Path.Combine(RoutePath, "Scenarios"), ref SavedRoute.ScenariosLastWrite, ref SavedRoute.ScenariosChecksum);

            if (RPchanged)
            {
                size += GetFileSize(Path.Combine(RoutePath, "RouteProperties.xml"));
            }

            string[] commonMask = new string[] { "*.bin", "*.xml" };

            foreach (string dir in Directory.GetDirectories(RoutePath))
            {
                switch (Path.GetFileName(dir).ToLower())
                {
                    case "networks":
                        if (LoftsChanged)
                            size += GetDirectorySize(Path.Combine(dir, "Loft Tiles"), commonMask);

                        if (RoadsChanged)
                            size += GetDirectorySize(Path.Combine(dir, "Road Tiles"), commonMask);

                        if (TracksChanged)
                            size += GetDirectorySize(Path.Combine(dir, "Track Tiles"), commonMask);

                        break;
                    case "scenarios":
                        if (ScenariosChanged)
                        {
                            foreach (string dir2 in Directory.GetDirectories(dir))
                            {
                                size += GetDirectorySize(dir2, commonMask, SearchOption.AllDirectories);
                            }
                        }
                        break;
                    case "scenery":
                        if (SceneryChanged)
                            size += GetDirectorySize(dir, commonMask);
                        break;
                }
            }

            if (APchanged)
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
                    catch (Exception e)
                    {
                        SentrySdk.CaptureException(e);
                        Trace.Assert(false, string.Format(Localization.Strings.CountSizeFail, file));
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

        private string GetDirectoryMD5(string path, bool isAP)
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

        private DateTime GetPathLastWriteTime(string path)
        {
            FileAttributes attr = File.GetAttributes(path);
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                FileInfo fi = new FileInfo(path);
                return fi.LastWriteTime;
            }
            else
            {
                DirectoryInfo di = new DirectoryInfo(path);
                return di.LastWriteTime;
            }
        }

        private DateTime GetDirectoryLastWriteTime(string path, bool isAP)
        {
            DateTime newestLastWrite = new DateTime();
            //string[] filePaths = isAP ? Directory.GetFiles(path, "*.ap", SearchOption.AllDirectories).OrderBy(p => p).ToArray() : Directory.GetFiles(path, "*.bin", SearchOption.AllDirectories).OrderBy(p => p).ToArray();
            string[] filePaths = isAP ? Directory.GetFiles(path, "*.ap", SearchOption.AllDirectories).OrderBy(p => p).ToArray() : (new List<string> { path }).ToArray();

            foreach (string fpath in filePaths)
            {
                DateTime lastWrite = GetPathLastWriteTime(fpath);

                if (lastWrite > newestLastWrite)
                    newestLastWrite = lastWrite;
            }

            return newestLastWrite;
        }

        private bool GetFileChanged(string path, ref DateTime lastWrite, ref string checksum, bool isAp = false)
        {
            if (!File.Exists(path))
                return false;

            DateTime newLastWrite = new FileInfo(path).LastWriteTime;
            if (newLastWrite <= lastWrite)
                return false;
            lastWrite = newLastWrite;

            string newChecksum = GetFileMD5(path);
            if (newChecksum == checksum)
                return false;
            checksum = newChecksum;

            return true;
        }

        private bool GetDirectoryChanged(string path, ref DateTime lastWrite, ref string checksum, bool isAp = false)
        {
            if (!Directory.Exists(path))
                return false;

            DateTime newLastWrite = GetDirectoryLastWriteTime(path, isAp);
            if (newLastWrite <= lastWrite)
                return false;
            lastWrite = newLastWrite;

            string newChecksum = GetDirectoryMD5(path, isAp);
            if (newChecksum == checksum)
                return false;
            checksum = newChecksum;
            return true;
        }
    }
}
