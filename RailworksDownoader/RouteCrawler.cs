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
using System.Windows;
using System.Xml;

namespace RailworksDownloader
{
    /// <summary>
    /// Crawles specific route
    /// </summary>
    public class RouteCrawler
    {
        // Route path
        private string RoutePath;
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
        private object DebugCountLock = new object();
        private object DebugReadedLock = new object();
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

        private List<string> DebugCountList { get; set; }
        private List<string> DebugReadedList { get; set; }

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
            RailworksPath = railworksPath;
            Dependencies = new HashSet<string>();
            ScenarioDeps = new HashSet<string>();
            Adapter = new SqLiteAdapter(Path.Combine(RoutePath, "cache.dls"));
            SavedRoute = Adapter.LoadSavedRoute(ContainsAP);
            MissingDependencies = new List<string>();
            MissingScenarioDeps = new List<string>();
            DownloadableDependencies = new List<string>();
            DownloadableScenarioDeps = new List<string>();
            DebugCountList = new List<string>();
            DebugReadedList = new List<string>();
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
            lock (DebugReadedLock)
            {
                DebugReadedList.Add(Railworks.NormalizePath(propertiesPath));
            }

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
                    }
                    else
                    {
                        lock (Dependencies)
                        {
                            Dependencies.Add(Railworks.NormalizePath(Path.Combine(blueprintSetID.FirstChild.InnerText, blueprintSetID.LastChild.InnerText, fname)));
                        }
                    }
                }
            });
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
                /*foreach (string file in Directory.GetFiles(scenarioDir, "*.bin", SearchOption.AllDirectories))
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
                }*/

                await Task.Run(() =>
                {
                    // Foreach all scenario files
                    Parallel.ForEach(Directory.GetFiles(scenarioDir, "*", SearchOption.AllDirectories), file =>
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if (ext == ".bin")
                        {
                            SerzReader sr = new SerzReader(file);

                            lock (ScenarioDepsLock)
                            {
                                ScenarioDeps.UnionWith(sr.GetDependencies());
                            }

                            lock (DebugReadedLock)
                            {
                                DebugReadedList.Add(Railworks.NormalizePath(file));
                            }

                            ReportProgress(file);
                        } 
                        else if (ext == ".xml")
                        {
                            lock (ScenarioDepsLock)
                            {
                                ParseBlueprint(file, true);
                            }
                            ReportProgress(file);

                            lock (DebugReadedLock)
                            {
                                DebugReadedList.Add(Railworks.NormalizePath(file));
                            }
                        }
                    });
                });
            }
        }

        /// <summary>
        /// Get network dependencies
        /// </summary>
        /// <param name="path">Network directory path</param>
        /// <returns></returns>
        private async Task GetNetworkDependencies(string dir)
        {
            // Foreach all network .bin files
            /*foreach (string file in Directory.GetFiles(dir, "*.bin"))
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
            }*/


            await Task.Run(() =>
            {
                // Foreach all Network files
                Parallel.ForEach(Directory.GetFiles(dir, "*.bin"), file =>
                {
                    SerzReader sr = new SerzReader(file);

                    lock (DependenciesLock)
                    {
                        Dependencies.UnionWith(sr.GetDependencies());
                    }

                    ReportProgress(file);
                    lock (DebugReadedLock)
                    {
                        DebugReadedList.Add(Railworks.NormalizePath(file));
                    }
                });
            });
        }

        /// <summary>
        /// Get scenery depencencies
        /// </summary>
        /// <param name="path">Route scenery directory</param>
        /// <returns></returns>
        private async Task GetSceneryDependencies(string path)
        {
            // Foreach all scenery .bin files
            /*foreach (string file in Directory.GetFiles(path, "*.bin"))
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
            }*/

            await Task.Run(() =>
            {
                // Foreach all Network files
                Parallel.ForEach(Directory.GetFiles(path, "*.bin"), file =>
                {
                    SerzReader sr = new SerzReader(file);

                    lock (DependenciesLock)
                    {
                        Dependencies.UnionWith(sr.GetDependencies());
                    }

                    ReportProgress(file);
                    lock (DebugReadedLock)
                    {
                        DebugReadedList.Add(Railworks.NormalizePath(file));
                    }
                });
            });
        }

        private void RunSERZ(string file)
        {
            //if (File.ReadAllText(file).Contains(""))
            string xml = Path.ChangeExtension(file, ".xml");

            /*ProcessStartInfo si = new ProcessStartInfo();
            si.FileName = Path.Combine(RailworksPath, "serz.exe");
            si.Arguments = $"\"{file}\" /xml:\"{xml}\"";
            si.RedirectStandardOutput = true;
            si.RedirectStandardError = true;
            si.UseShellExecute = false;
            si.CreateNoWindow = true;

            Process serz = new Process();
            serz.StartInfo = si;

            serz.Start();
            serz.WaitForExit();*/

            try
            {
                SerzReader sr = new SerzReader(file);
                sr.FlushToXML(xml);
            }
            catch (Exception e)
            {
                Desharp.Debug.Log(e);
            }
        }

        private async Task _GetDependencies()
        {
            bool loftsChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Loft Tiles")) != SavedRoute.LoftChecksum;
            bool roadsChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Road Tiles")) != SavedRoute.RoadChecksum;
            bool tracksChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Networks", "Track Tiles")) != SavedRoute.TrackChecksum;
            bool sceneryChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Scenery")) != SavedRoute.SceneryChecksum;
            bool rpChanged = GetFileMD5(Path.Combine(RoutePath, "RouteProperties.xml")) != SavedRoute.RoutePropertiesChecksum;
            bool scenariosChanged = GetDirectoryMD5(Path.Combine(RoutePath, "Scenarios")) != SavedRoute.ScenariosChecksum;

            var md5 = ComputeChecksums();

            if (SavedRoute.Dependencies != null)
                Dependencies.UnionWith(SavedRoute.Dependencies);

            if (SavedRoute.ScenarioDeps != null)
                ScenarioDeps.UnionWith(SavedRoute.ScenarioDeps);

            if (loftsChanged || roadsChanged || tracksChanged || sceneryChanged || rpChanged || scenariosChanged)
            {
                Task n1 = null;
                Task n2 = null;
                Task n3 = null;
                Task s = null;
                Task t = null;
                Task<IEnumerable<string>> prop = null;

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


                IEnumerable<string> routeProperties1 = new List<string>();
                if (prop != null)
                    routeProperties1 = await prop;

                lock (DependenciesLock)
                {
                    Dependencies.UnionWith(routeProperties1);
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
                {
                    SavedRoute.APChecksum = GetDirectoryMD5(RoutePath, true);
                }
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
                        if (!isScenario)
                        {
                            lock (DependenciesLock)
                            {
                                Dependencies.UnionWith(sr.GetDependencies());
                            }
                        }
                        else
                        {
                            lock (ScenarioDepsLock)
                            {
                                ScenarioDeps.UnionWith(sr.GetDependencies());
                            }
                        }
                    }

                    ReportProgress(entry.Size);
                    lock (DebugReadedLock)
                    {
                        DebugReadedList.Add(Railworks.NormalizePath(Path.Combine(Path.GetDirectoryName(fname), entry.Name)));
                    }
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
                    /*try {
                        string relFpat = Railworks.GetRelativePath(RoutePath, file);
                        ZipFile.ExtractToDirectory(file, Path.Combine(TempPath, relFpat));
                    } catch {}
                    finally
                    {
                        count++;
                    }*/

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

            foreach (string fname in DebugCountList.Except(DebugReadedList))
            {
                Debug.Assert(false, string.Format("Kritička chyba! Neparsovaný soubor {0} který byl načten při sčítání velikostí!", fname));
                Desharp.Debug.Log(DebugCountList);
                Desharp.Debug.Log(DebugReadedList);
            }

            foreach (string fname in DebugReadedList.Except(DebugCountList))
            {
                Debug.Assert(false, string.Format("Kritička chyba! Parsovaný soubor {0} nebyl načten při sčítání velikostí!", fname));
                Desharp.Debug.Log(DebugCountList);
                Desharp.Debug.Log(DebugReadedList);
            }

            /*if (count > 0)
            {
                new Task(() => { Railworks.DeleteDirectory(TempPath); }).Start();
            }*/
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
                if (size != 0)
                {
                    lock (DebugCountLock)
                    {
                        DebugCountList.Add(Railworks.NormalizePath(Path.Combine(RoutePath, "RouteProperties.xml")));
                    }
                }
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
                                    lock (DebugCountLock)
                                    {
                                        DebugCountList.Add(Railworks.NormalizePath(Path.Combine(Path.GetDirectoryName(file), entry.Name)));
                                    }
                                }
                                else if (mainFolder == "networks")
                                {
                                    string subFolder = relativePath.Split(Path.DirectorySeparatorChar)[1];
                                    if (subFolder == "loft tiles" || subFolder == "road tiles" || subFolder == "track tiles")
                                    {
                                        size += entry.Size;
                                        lock (DebugCountLock)
                                        {
                                            DebugCountList.Add(Railworks.NormalizePath(Path.Combine(Path.GetDirectoryName(file), entry.Name)));
                                        }
                                    }
                                } 
                                else if (Path.GetFileName(entry.Name).ToLower().Contains("routeproperties"))
                                {
                                    size += entry.Size;
                                    lock (DebugCountLock)
                                    {
                                        DebugCountList.Add(Railworks.NormalizePath(Path.Combine(Path.GetDirectoryName(file), entry.Name)));
                                    }
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
                    foreach (var file in Directory.GetFiles(directory, maskArr[i], so))
                    {
                        size += GetFileSize(file);

                        lock (DebugCountLock)
                        {
                            DebugCountList.Add(Railworks.NormalizePath(file));
                        }
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

                    long fsize = GetFileSize(filePath);
                    if (fsize > 0x6400000 || (new ComputerInfo().AvailablePhysicalMemory < 0x40000000 && fsize > 0xA00000)) //File bigger than 100MB or total memory used > 2GB - read chunk by chunk
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
                    }
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
