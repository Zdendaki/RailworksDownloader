using Microsoft.Win32;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace RailworksDownloader
{
    public class SteamManager
    {
        public const uint SteamAppId = 24010;
        public readonly string SteamPath = null;
        public readonly string RWPath = null;
        public readonly bool SteamFound = false;
        public readonly string AppManifestPath = null;

        public class DLC
        {
            public uint DLCAppId;
            public List<string> IncludedFiles;

            public DLC(uint dlcappid)
            {
                DLCAppId = dlcappid;
                IncludedFiles = new List<string>();
            }
        }

        public SteamManager(string rwPath = null)
        {
            var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam") ??
                      RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                          .OpenSubKey("SOFTWARE\\Valve\\Steam");

            RWPath = rwPath;

            if (key != null && key.GetValue("SteamPath") is string steamPath)
            {
                SteamPath = steamPath;
                SteamPath = Path.GetFullPath(SteamPath);
                AppManifestPath = GetAppManifestPath();
                if (RWPath == null)
                    RWPath = GetRWInstallPath();
                SteamFound = true;
            }
        }

        private string GetAppManifestPath()
        {
            IEnumerable<string> steamLibraries = GetLibraries();
            return steamLibraries.Select(library => Path.Combine(library, $"appmanifest_{SteamAppId}.acf")).FirstOrDefault(File.Exists);
        }

        public string GetRWInstallPath()
        {
            if (AppManifestPath == null)
                return null;

            return Path.Combine(Path.GetDirectoryName(AppManifestPath), "common", KeyValue.LoadAsText(AppManifestPath)["installdir"].Value);
        }

        public List<DLC> GetInstalledDLCFiles()
        {
            List<DLC> dlcList = new List<DLC>();

            if (AppManifestPath != null && File.Exists(AppManifestPath))
            {
                KeyValue appManifest = KeyValue.LoadAsText(AppManifestPath);
                Dictionary<string, string> depotManifests = new Dictionary<string, string>();

                foreach (KeyValue mountedDepot in appManifest["MountedDepots"].Children)
                {
                    depotManifests[mountedDepot.Name] = mountedDepot.Value;
                }

                foreach (KeyValue mountedDepot in appManifest["InstalledDepots"].Children)
                {
                    depotManifests[mountedDepot.Name] = mountedDepot["manifest"].Value;
                }

                foreach (KeyValuePair<string, string> depotManifest in depotManifests)
                {
                    uint dlcappid = Convert.ToUInt32(depotManifest.Key);
                    string manifestPath = Path.Combine(SteamPath, "depotcache", $"{dlcappid}_{depotManifest.Value}.manifest");

                    if (File.Exists(manifestPath))
                    {
                        DLC dlc = new DLC(dlcappid);
                        var manifest = DepotManifest.Deserialize(File.ReadAllBytes(manifestPath));

                        foreach (var file in manifest.Files)
                        {
                            if (file.Flags.HasFlag(EDepotFileFlag.Directory))
                            {
                                continue;
                            }

                            string fileName = file.FileName.ToLower();
                            string extension = Path.GetExtension(fileName).ToLower();

                            if (fileName.Contains("assets"))
                            {
                                if (extension.Contains("xml") || extension.Contains("bin"))
                                    dlc.IncludedFiles.Add(Railworks.NormalizePath(fileName));

                                if (extension == ".ap")
                                {
                                    string absoluteFileName = Path.Combine(RWPath, fileName);
                                    try
                                    {
                                        var zipFile = ZipFile.OpenRead(absoluteFileName);
                                        dlc.IncludedFiles.AddRange(from x in zipFile.Entries where (x.FullName.Contains(".xml") || x.FullName.Contains(".bin")) select Railworks.NormalizePath(Railworks.GetRelativePath(Path.Combine(RWPath, "Assets"), Path.Combine(Path.GetDirectoryName(absoluteFileName), x.FullName))));
                                    }
                                    catch { }
                                }

                            }
                        }

                        dlcList.Add(dlc);
                    }
                }
            }

            return dlcList;
        }

        private IEnumerable<string> GetLibraries()
        {
            var libraryFoldersPath = Path.Combine(SteamPath, "steamapps", "libraryfolders.vdf");
            var libraryFoldersKv = KeyValue.LoadAsText(libraryFoldersPath);
            var libraryFolders = new List<string>
            {
                Path.Combine(SteamPath, "steamapps")
            };

            if (libraryFoldersKv != null)
            {
                libraryFolders.AddRange(libraryFoldersKv.Children
                    .Where(libraryFolder => int.TryParse(libraryFolder.Name, out _))
                    .Select(x => Path.Combine(x.Value, "steamapps"))
                );
            }

            return libraryFolders;
        }
    }
}
