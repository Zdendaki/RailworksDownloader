using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace RailworksDownoader
{
    class Railworks
    {
        private string RWPath;
        
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

        public bool CheckPath()
        {
            return File.Exists(RWPath);
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
                string rp_path = Path.Combine(path, dir, "RouteProperties.xml");

                if (File.Exists(rp_path)) 
                {
                    yield return ParseRouteProperties(rp_path);
                }
                else 
                {
                    foreach (string file in Directory.GetFiles(Path.Combine(path, dir), "*.ap"))
                    {
                        using (ZipArchive archive = ZipFile.OpenRead(Path.Combine(path, dir, file)))
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
    }
}
