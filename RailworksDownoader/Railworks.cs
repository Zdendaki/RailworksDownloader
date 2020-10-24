using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace RailworksDownoader
{
    class Railworks
    {
        public string RWPath;
        
        public Railworks()
        {
            RWPath = GetRWPath();
        }

        public Railworks(string path)
        {
            RWPath = path;
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

        private string ParseRouteProperties(string path)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            
            return ParseDisplayNameNode(doc.DocumentElement.SelectSingleNode("DisplayName"));
        }

        /// <summary>
        /// Get list of routes
        /// </summary>
        /// <param name="path">Routes path</param>
        /// <returns></returns>
        public IEnumerable<string> GetRoutes()
        {
            string path = Path.Combine(RWPath, "Content", "Routes");

            foreach (string dir in Directory.GetDirectories(path))
            {
                string rp_path = Path.Combine(dir, "RouteProperties.xml");

                if (File.Exists(rp_path)) 
                {
                    yield return ParseRouteProperties(rp_path);
                }
                else
                {
                    foreach (string file in Directory.GetFiles(Path.Combine(path, dir), "*.ap"))
                    {
                        using (ZipArchive archive = ZipFile.OpenRead(file))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName.Contains("RouteProperties")))
                            {
                                if (!Directory.Exists(Path.Combine(path, dir, "temp")))
                                    Directory.CreateDirectory(Path.Combine(path, dir, "temp"));

                                entry.ExtractToFile(Path.Combine(path, dir, "temp", entry.FullName), true);
                                yield return ParseRouteProperties(Path.Combine(path, dir, "temp", entry.FullName));
                            }
                        }
                    }
                }
            }
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

        private IEnumerable<string> GetRoutePropertiesDependencies(string propertiesPath)
        {
            var dependencies = new List<string>();

            XmlDocument doc = new XmlDocument();
            doc.Load(propertiesPath);

            var root = doc.DocumentElement;

            dependencies.Add(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("BlueprintID")));
            dependencies.AddRange(ParseSkiesNode(root.SelectSingleNode("Skies")));
            dependencies.Add(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("WeatherBlueprint")));
            dependencies.Add(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("TerrainBlueprint")));
            dependencies.Add(ParseAbsoluteBlueprintIDNode(root.SelectSingleNode("MapBlueprint")));
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

        private IEnumerable<string> GetScenariosDependencies(string scenariosPath)
        {
            var dependencies = new List<string>();

            //TODO: implement scenarios

            return dependencies;
        }

        private HashSet<string> ParseNetworkDependencies(string path)
        {
            var dependencies = new HashSet<string>();

            foreach (string dir in Directory.GetDirectories(path))
            {
                foreach (string file in Directory.GetFiles(dir, "*.*bin"))
                {
                    string xml = Path.ChangeExtension(file, ".xml");

                    RunSERZ(file);
                    dependencies.UnionWith(ParseBlueprint(xml));
                    File.Delete(xml);
                }
            }

            return dependencies;
        }

        private IEnumerable<string> ParseSceneryDependencies(string path)
        {
            var dependencies = new List<string>();
            
            foreach (string file in Directory.GetFiles(path, "*.*bin"))
            {
                string xml = Path.ChangeExtension(file, ".xml");

                RunSERZ(file);
                dependencies.AddRange(ParseBlueprint(xml));
                File.Delete(xml);
            }

            return dependencies;
        }

        private void RunSERZ(string file)
        {
            string xml = Path.ChangeExtension(file, ".xml");

            ProcessStartInfo si = new ProcessStartInfo();
            si.FileName = Path.Combine(RWPath, "serz.exe");
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

        private HashSet<string> SelectDependencies(string routePath)
        {
            var dependencies = new HashSet<string>();
            foreach (string dir in Directory.GetDirectories(routePath))
            {
                switch (Path.GetFileName(dir))
                {
                    case "Networks":
                        dependencies.UnionWith(ParseNetworkDependencies(dir));
                        break;
                    case "Scenarios":
                        dependencies.UnionWith(GetScenariosDependencies(dir));
                        break;
                    case "Scenery":
                        dependencies.UnionWith(ParseSceneryDependencies(dir));
                        break;
                }
            }
            dependencies.UnionWith(GetRoutePropertiesDependencies(Path.Combine(routePath, "RouteProperties.xml")));

            return dependencies;
        }

        public IEnumerable<string> GetDependencies(string routePath)
        {
            if (File.Exists(Path.Combine(routePath, "RouteProperties.xml"))) {
                return SelectDependencies(routePath);
            }
            else
            {
                foreach (string file in Directory.GetFiles(routePath, "*.ap"))
                {
                    ZipFile.ExtractToDirectory(Path.Combine(routePath, file), Path.Combine(routePath, "temp"));
                }
                var _ = SelectDependencies(Path.Combine(routePath, "temp"));
                Directory.Delete(Path.Combine(routePath, "temp"));

                return _;
            }
        }
    }
}
