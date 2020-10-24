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
            if (Directory.Exists(RoutePath))
            {
                AllFilesSize = CountAllFiles();

                await GetDependencies();

                Complete?.Invoke();
            }
        }

        private void ReportProgress(string file)
        {
            lock (ProgressLock)
            {
                Progress += GetFileSize(file);
            }

            Debug.Assert(Progress <= AllFilesSize, "Fatal, Progress is bigger than size of all files! "+Progress+":"+AllFilesSize);

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

        private async Task _GetDependencies(string routePath, bool isAP = false)
        {
            var savedRoute = LoadSavedRoute(isAP);
            bool loftsChanged = (CreateDirectoryMd5(Path.Combine(RoutePath, "Networks", "Loft Tiles")) != savedRoute.loftChcksum);
            bool roadsChanged = (CreateDirectoryMd5(Path.Combine(RoutePath, "Networks", "Road Tiles")) != savedRoute.roadChcksum);
            bool tracksChanged = (CreateDirectoryMd5(Path.Combine(RoutePath, "Networks", "Track Tiles")) != savedRoute.trackChcksum);
            bool sceneryChanged = (CreateDirectoryMd5(Path.Combine(RoutePath, "Scenery")) != savedRoute.sceneryChcksum);
            //bool rpChanged = (CreateDirectoryMd5(Path.Combine(RoutePath, "Networks", "Loft Tiles")) != savedRoute.loftChcksum);
            bool rpChanged = true;
            bool scenariosChanged = false;

            Dependencies.UnionWith(savedRoute.dependencies);

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

            savedRoute.dependencies = Dependencies.ToList();
            SaveRoute(savedRoute);
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
                    await _GetDependencies(Path.Combine(RoutePath, "temp"), true);

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

        private void CreateCacheFile()
        {
            var db_file = Path.Combine(RoutePath, "cache.dls");
            SQLiteConnection sqlite_conn = new SQLiteConnection("Data Source=" + db_file + "; Version = 3; Compress = True; ");
            sqlite_conn.Open();
            SQLiteCommand command = new SQLiteCommand(sqlite_conn)
            {
                CommandText = "CREATE TABLE dependencies (id INT UNIQUE, path TEXT); CREATE TABLE checksums (id INT UNIQUE, folder VARCHAR(32), chcksum VARCHAR(32)"
            };
            command.ExecuteNonQuery();
            command.Dispose();
        }

        private struct LoadedRoute
        {
            public string loftChcksum;
            public string roadChcksum;
            public string trackChcksum;
            public string sceneryChcksum;
            public string routePropertiesChcksum;
            public List<string> dependencies;
        }

        private void SaveRoute(LoadedRoute routeToSave)
        {
            var db_file = Path.Combine(RoutePath, "cache.dls");
            SQLiteConnection sqlite_conn = new SQLiteConnection("Data Source=" + db_file + "; Version = 3; Compress = True; ");
            sqlite_conn.Open();
            for (int i = 0; i < 5; i++)
            {
                SQLiteCommand insertSQL = new SQLiteCommand("INSERT INTO checksums (id, folder, chcksum) VALUES (?,?,?) ON CONFLICT(id) DO UPDATE SET id = ?, folder = ?, chcksum = ?;", sqlite_conn);
                insertSQL.Parameters.Add(i);
                switch (i)
                {
                    case 0:
                        insertSQL.Parameters.Add("loft");
                        insertSQL.Parameters.Add(routeToSave.loftChcksum);
                        insertSQL.Parameters.Add(i);
                        insertSQL.Parameters.Add("loft");
                        insertSQL.Parameters.Add(routeToSave.loftChcksum);
                        break;
                    case 1:
                        insertSQL.Parameters.Add("road");
                        insertSQL.Parameters.Add(routeToSave.roadChcksum);
                        insertSQL.Parameters.Add(i);
                        insertSQL.Parameters.Add("road");
                        insertSQL.Parameters.Add(routeToSave.roadChcksum);
                        break;
                    case 2:
                        insertSQL.Parameters.Add("track");
                        insertSQL.Parameters.Add(routeToSave.trackChcksum);
                        insertSQL.Parameters.Add(i);
                        insertSQL.Parameters.Add("track");
                        insertSQL.Parameters.Add(routeToSave.trackChcksum);
                        break;
                    case 3:
                        insertSQL.Parameters.Add("scenery");
                        insertSQL.Parameters.Add(routeToSave.sceneryChcksum);
                        insertSQL.Parameters.Add(i);
                        insertSQL.Parameters.Add("scenery");
                        insertSQL.Parameters.Add(routeToSave.sceneryChcksum);
                        break;
                    case 4:
                        insertSQL.Parameters.Add("routeProperties");
                        insertSQL.Parameters.Add(routeToSave.routePropertiesChcksum);
                        insertSQL.Parameters.Add(i);
                        insertSQL.Parameters.Add("routeProperties");
                        insertSQL.Parameters.Add(routeToSave.routePropertiesChcksum);
                        break;
                }
                try
                {
                    insertSQL.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }

            SQLiteCommand clear = new SQLiteCommand("REMOVE FROM dependencies WHERE id;", sqlite_conn);
            clear.ExecuteNonQuery();
            for (int i = 0; i < routeToSave.dependencies.Count; i++)
            {
                SQLiteCommand insertSQL = new SQLiteCommand("INSERT INTO dependencies (id, path) VALUES (?,?);", sqlite_conn);
                insertSQL.Parameters.Add(i);
                insertSQL.Parameters.Add(routeToSave.dependencies[i]);
                try
                {
                    insertSQL.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
        }

        private LoadedRoute LoadSavedRoute(bool isAP = false)
        {
            var db_file = Path.Combine(RoutePath, "cache.dls");
            LoadedRoute loadedRoute = new LoadedRoute();

            if (File.Exists(db_file))
            {
                SQLiteConnection sqlite_conn = new SQLiteConnection("Data Source=" + db_file + "; Version = 3; Compress = True; ");
                sqlite_conn.Open();
                SQLiteCommand command = new SQLiteCommand(sqlite_conn)
                {
                    CommandText = "SELECT * FROM checksums"
                };
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    switch (reader["folder"])
                    {
                        case "loft":
                            loadedRoute.loftChcksum = Convert.ToString(reader["chcksum"]);
                            break;
                        case "road":
                            loadedRoute.roadChcksum = Convert.ToString(reader["chcksum"]);
                            break;
                        case "track":
                            loadedRoute.trackChcksum = Convert.ToString(reader["chcksum"]);
                            break;
                        case "scenarios":
                            //TODO: implement scenarios
                            break;
                        case "scenery":
                            loadedRoute.sceneryChcksum = Convert.ToString(reader["chcksum"]);
                            break;
                        case "routeProperties":
                            loadedRoute.routePropertiesChcksum = Convert.ToString(reader["chcksum"]);
                            break;
                    }
                }
                command.Dispose();

                loadedRoute.dependencies = new List<string>();
                command = new SQLiteCommand(sqlite_conn)
                {
                    CommandText = "SELECT * FROM dependencies"
                };
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    loadedRoute.dependencies.Add(Convert.ToString(reader["path"]));
                }
                command.Dispose();
            } 
            else
            {
                CreateCacheFile();
                loadedRoute.dependencies = new List<string>();
                loadedRoute.loftChcksum = null;
                loadedRoute.roadChcksum = null;
                loadedRoute.routePropertiesChcksum = null;
                loadedRoute.sceneryChcksum = null;
                loadedRoute.trackChcksum = null;
            }

            return loadedRoute;
        }

        private static string CreateDirectoryMd5(string srcPath)
        {
            var filePaths = Directory.GetFiles(srcPath, "*.*bin").OrderBy(p => p).ToArray();

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
