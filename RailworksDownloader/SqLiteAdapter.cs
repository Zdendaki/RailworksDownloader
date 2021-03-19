﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using static RailworksDownloader.Utils;

namespace RailworksDownloader
{
    public class SqLiteAdapter
    {
        internal string DatabasePath { get; set; }

        private readonly string ConnectionString;

        private SQLiteConnection MemoryConn { get; set; }
        private SQLiteConnection FileConn { get; set; }

        private readonly object DBLock = new object();

        public SqLiteAdapter(string path, bool isMainCache = false)
        {
            DatabasePath = path;
            ConnectionString = $"Data Source={DatabasePath}; Version = 3; Compress = True;";
            MemoryConn = new SQLiteConnection("Data Source=:memory:");
            MemoryConn.Open();

            if (File.Exists(DatabasePath))
            {
                FileConn = new SQLiteConnection(ConnectionString);
                FileConn.Open();
                FileConn.BackupDatabase(MemoryConn, "main", "main", -1, null, 0);
                FileConn.Close();
            }

            if (isMainCache)
                CreateMainCacheFile();
            else
                CreateRouteCacheFile();
        }

        public void FlushToFile(bool keepOpen = false)
        {
            lock (DBLock)
            {
                FileConn = new SQLiteConnection(ConnectionString);
                FileConn.Open();
                MemoryConn.BackupDatabase(FileConn, "main", "main", -1, null, 0);
                FileConn.Close();
                if (!keepOpen)
                    MemoryConn.Close();
            }
        }

        internal void SaveRoute(LoadedRoute route)
        {
            SQLiteCommand cmd = new SQLiteCommand("INSERT INTO checksums (folder, chcksum) VALUES (@folder,@chcksum) ON CONFLICT(folder) DO UPDATE SET folder = @folder, chcksum = @chcksum;", MemoryConn);
            for (int i = 0; i < 7; i++)
            {
                switch (i)
                {
                    case 0:
                        cmd.Parameters.AddWithValue("@folder", "loft");
                        cmd.Parameters.AddWithValue("@chcksum", route.LoftChecksum);
                        break;
                    case 1:
                        cmd.Parameters.AddWithValue("@folder", "road");
                        cmd.Parameters.AddWithValue("@chcksum", route.RoadChecksum);
                        break;
                    case 2:
                        cmd.Parameters.AddWithValue("@folder", "track");
                        cmd.Parameters.AddWithValue("@chcksum", route.TrackChecksum);
                        break;
                    case 3:
                        cmd.Parameters.AddWithValue("@folder", "scenery");
                        cmd.Parameters.AddWithValue("@chcksum", route.SceneryChecksum);
                        break;
                    case 4:
                        cmd.Parameters.AddWithValue("@folder", "AP");
                        cmd.Parameters.AddWithValue("@chcksum", route.APChecksum);
                        break;
                    case 5:
                        cmd.Parameters.AddWithValue("@folder", "routeProperties");
                        cmd.Parameters.AddWithValue("@chcksum", route.RoutePropertiesChecksum);
                        break;
                    case 6:
                        cmd.Parameters.AddWithValue("@folder", "scenarios");
                        cmd.Parameters.AddWithValue("@chcksum", route.ScenariosChecksum);
                        break;
                }
                cmd.ExecuteNonQuery();
            }

            SQLiteCommand clear = new SQLiteCommand("DELETE FROM dependencies;", MemoryConn);
            clear.ExecuteNonQuery();
            SQLiteCommand insertSQL = new SQLiteCommand("INSERT INTO dependencies (path, isScenario) VALUES (@path,@isScenario);", MemoryConn);
            int depsCount = route.Dependencies.Count;
            for (int i = 0; i < depsCount + route.ScenarioDeps.Count; i++)
            {
                if (i < depsCount)
                {
                    if (string.IsNullOrWhiteSpace(route.Dependencies.ElementAt(i)))
                        continue;
                    insertSQL.Parameters.AddWithValue("@path", route.Dependencies.ElementAt(i));
                    insertSQL.Parameters.AddWithValue("@isScenario", false);
                    insertSQL.ExecuteNonQuery();
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(route.ScenarioDeps.ElementAt(i - depsCount)))
                        continue;
                    insertSQL.Parameters.AddWithValue("@path", route.ScenarioDeps.ElementAt(i - depsCount));
                    insertSQL.Parameters.AddWithValue("@isScenario", true);
                    insertSQL.ExecuteNonQuery();
                }
            }
        }

        internal LoadedRoute LoadSavedRoute(bool isAP = false)
        {
            LoadedRoute loadedRoute = new LoadedRoute();

            if (File.Exists(DatabasePath))
            {
                SQLiteCommand command = new SQLiteCommand(MemoryConn)
                {
                    CommandText = "SELECT * FROM checksums;"
                };
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    switch (reader["folder"])
                    {
                        case "loft":
                            loadedRoute.LoftChecksum = Convert.ToString(reader["chcksum"]);
                            break;
                        case "road":
                            loadedRoute.RoadChecksum = Convert.ToString(reader["chcksum"]);
                            break;
                        case "track":
                            loadedRoute.TrackChecksum = Convert.ToString(reader["chcksum"]);
                            break;
                        case "scenarios":
                            loadedRoute.ScenariosChecksum = Convert.ToString(reader["chcksum"]);
                            break;
                        case "scenery":
                            loadedRoute.SceneryChecksum = Convert.ToString(reader["chcksum"]);
                            break;
                        case "AP":
                            loadedRoute.APChecksum = Convert.ToString(reader["chcksum"]);
                            break;
                        case "routeProperties":
                            loadedRoute.RoutePropertiesChecksum = Convert.ToString(reader["chcksum"]);
                            break;
                    }
                }
                command.Dispose();

                loadedRoute.Dependencies = new HashSet<string>();
                loadedRoute.ScenarioDeps = new HashSet<string>();
                command = new SQLiteCommand(MemoryConn)
                {
                    CommandText = "SELECT * FROM dependencies"
                };
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    switch (Convert.ToInt32(reader["isScenario"]))
                    {
                        case 1:
                            loadedRoute.ScenarioDeps.Add(NormalizePath(Convert.ToString(reader["path"])));
                            break;
                        default:
                            loadedRoute.Dependencies.Add(NormalizePath(Convert.ToString(reader["path"])));
                            break;
                    }
                }
                command.Dispose();
            }
            else
            {
                CreateRouteCacheFile();
            }

            return loadedRoute;
        }

        internal void CreateRouteCacheFile()
        {
            SQLiteCommand command = new SQLiteCommand(MemoryConn)
            {
                CommandText = "CREATE TABLE IF NOT EXISTS dependencies (id INTEGER PRIMARY KEY, path TEXT, isScenario INTEGER); CREATE TABLE IF NOT EXISTS checksums (id INTEGER PRIMARY KEY, folder VARCHAR(32) UNIQUE, chcksum VARCHAR(32));"
            };
            command.ExecuteNonQuery();
            command.Dispose();
        }

        internal void CreateMainCacheFile()
        {
            SQLiteCommand command = new SQLiteCommand(MemoryConn)
            {
                CommandText = @"CREATE TABLE IF NOT EXISTS package_list (
    id INTEGER PRIMARY KEY,
    file_name VARCHAR(260),
    display_name VARCHAR(1000),
    category INTEGER,
    era INTEGER,
    country INTEGER,
    version INTEGER,
    owner INTEGER,
    datetime TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    description VARCHAR(10000),
    target_path VARCHAR(260)
);
CREATE TABLE IF NOT EXISTS file_list (
    id INTEGER PRIMARY KEY,
    package_id INTEGER,
    file_name VARCHAR(260),
    UNIQUE(package_id, file_name)
);
CREATE TABLE IF NOT EXISTS installed_files (
    id INTEGER PRIMARY KEY,
    package_id INTEGER,
    file_name VARCHAR(260),
    UNIQUE(package_id, file_name)
);"
            };
            command.ExecuteNonQuery();
            command.Dispose();
        }

        internal void SaveInstalledPackage(Package package)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM file_list WHERE `package_id` = @id;", MemoryConn))
            {
                cmd.Parameters.AddWithValue("id", package.PackageId);
                cmd.ExecuteNonQuery();
            }

            using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO package_list (id, file_name, display_name, category, era, country, version, owner, datetime, description, target_path) VALUES (@id,@file_name,@display_name,@category,@era,@country,@version,@owner,@datetime,@description,@target_path) ON CONFLICT(id) DO UPDATE SET file_name = @file_name, display_name = @display_name, category = @category, era = @era, country = @country, version = @version, owner = @owner, datetime = @datetime, description = @description, version = @version;", MemoryConn))
            {
                cmd.Parameters.AddWithValue("id", package.PackageId);
                cmd.Parameters.AddWithValue("file_name", package.FileName);
                cmd.Parameters.AddWithValue("display_name", package.DisplayName);
                cmd.Parameters.AddWithValue("category", package.Category);
                cmd.Parameters.AddWithValue("era", package.Era);
                cmd.Parameters.AddWithValue("country", package.Country);
                cmd.Parameters.AddWithValue("version", package.Version);
                cmd.Parameters.AddWithValue("owner", package.Owner);
                cmd.Parameters.AddWithValue("datetime", package.Datetime);
                cmd.Parameters.AddWithValue("description", package.Description);
                cmd.Parameters.AddWithValue("target_path", package.TargetPath);
                cmd.ExecuteNonQuery();
            }

            using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO file_list (package_id, file_name) VALUES (@package_id,@file_name);", MemoryConn))
            {
                cmd.Parameters.AddWithValue("package_id", package.PackageId);
                foreach (string file in package.FilesContained)
                {
                    cmd.Parameters.AddWithValue("file_name", file);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        internal void RemoveInstalledPackage(int pkgId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM file_list WHERE `package_id` = @id;", MemoryConn))
            {
                cmd.Parameters.AddWithValue("id", pkgId);
                cmd.ExecuteNonQuery();
            }

            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM package_list WHERE `id` = @id;", MemoryConn))
            {
                cmd.Parameters.AddWithValue("id", pkgId);
                cmd.ExecuteNonQuery();
            }
        }

        internal List<Package> LoadInstalledPackages()
        {
            List<Package> loadedPackages = new List<Package>();

            using (SQLiteCommand command = new SQLiteCommand("SELECT * FROM package_list;", MemoryConn))
            {
                SQLiteDataReader reader = command.ExecuteReader();
                using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM file_list WHERE package_id = @package_id;", MemoryConn))
                {
                    while (reader.Read())
                    {
                        Package loadedPackage = new Package(
                            Convert.ToInt32(reader["id"]),
                            Convert.ToString(reader["display_name"]),
                            Convert.ToInt32(reader["category"]),
                            Convert.ToInt32(reader["era"]),
                            Convert.ToInt32(reader["country"]),
                            Convert.ToInt32(reader["owner"]),
                            Convert.ToString(reader["datetime"]),
                            Convert.ToString(reader["target_path"]),
                            new List<string>(),
                            Convert.ToString(reader["file_name"]),
                            Convert.ToString(reader["description"]),
                            Convert.ToInt32(reader["version"])
                        );

                        cmd.Parameters.AddWithValue("@package_id", loadedPackage.PackageId);
                        using (SQLiteDataReader r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                loadedPackage.FilesContained.Add(NormalizePath(Convert.ToString(r["file_name"])));
                            }
                        }

                        loadedPackages.Add(loadedPackage);
                    }
                }
            }

            return loadedPackages;
        }

        internal void SavePackageFiles(int id, List<string> filesToAdd)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO installed_files (package_id, file_name) VALUES (@package_id,@file_name);", MemoryConn))
            {
                cmd.Parameters.AddWithValue("package_id", id);
                foreach (string file in filesToAdd)
                {
                    cmd.Parameters.AddWithValue("file_name", file);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        internal void RemovePackageFiles(List<string> filesToRemove)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM installed_files WHERE `file_name` = @file_name;", MemoryConn))
            {
                foreach (string file in filesToRemove)
                {
                    cmd.Parameters.AddWithValue("file_name", file);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        internal List<string> LoadPackageFiles(int id)
        {
            List<string> loadedFiles = new List<string>();

            using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM installed_files WHERE package_id = @package_id;", MemoryConn))
            {
                cmd.Parameters.AddWithValue("@package_id", id);
                SQLiteDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    loadedFiles.Add(Convert.ToString(r["file_name"]));
                }
            }

            return loadedFiles;
        }
    }
}