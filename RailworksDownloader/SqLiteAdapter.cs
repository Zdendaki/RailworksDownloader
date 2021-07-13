using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using static RailworksDownloader.Utils;

namespace RailworksDownloader
{
    internal class SqLiteAdapter
    {
        private class Column
        {
            public string Name;
            public string Type;
            public string Key;
            public bool Validated = false;
            public bool IsKey = false;

            public Column(string name, bool isKey = true) : this(name, "", "") { IsKey = isKey; }

            public Column(string name, string type) : this(name, type, "") { }

            public Column(string name, string type, string key)
            {
                Name = name;
                Type = type;
                Key = key;
            }

            public string SQL => $"{Name} {Type} {Key}";
        }

        private class TableScheme
        {
            public string Name;
            public List<Column> Cols;

            public TableScheme(string name, List<Column> cols)
            {
                Name = name;
                Cols = cols;
            }

            public string SQL
            {
                get
                {
                    string content = string.Join(", ", Cols.Select(x => x.SQL));
                    return $"CREATE TABLE {Name} ({content});";
                }
            }
        }

        private class DatabaseScheme
        {

            public string Name;
            public List<TableScheme> Tables;

            public DatabaseScheme(string name, List<TableScheme> tables)
            {
                Name = name;
                Tables = tables;
            }
        }

        internal string DatabasePath { get; set; }

        private readonly string ConnectionString;

        private readonly DatabaseScheme RouteCache = new DatabaseScheme("RouteCache", new List<TableScheme>
        {
            new TableScheme("dependencies", new List<Column>
            {
                new Column("id", "INTEGER", "PRIMARY KEY"),
                new Column("path", "TEXT"),
                new Column("isScenario", "INTEGER")
            }),
            new TableScheme("checksums", new List<Column>
            {
                new Column("id", "INTEGER", "PRIMARY KEY"),
                new Column("folder", "VARCHAR(32)", "UNIQUE"),
                new Column("chcksum", "VARCHAR(32)"),
                new Column("last_write", "DATETIME")
            })
        });

        private readonly DatabaseScheme MainCache = new DatabaseScheme("MainCache", new List<TableScheme>
        {
            new TableScheme("package_list", new List<Column>
            {
                new Column("id", "INTEGER", "PRIMARY KEY"),
                new Column("file_name", "VARCHAR (260)"),
                new Column("display_name", "VARCHAR(1000)"),
                new Column("category", "INTEGER"),
                new Column("era", "INTEGER"),
                new Column("country", "INTEGER"),
                new Column("version", "INTEGER"),
                new Column("owner", "INTEGER"),
                new Column("datetime", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"),
                new Column("description", "VARCHAR(10000)"),
                new Column("target_path", "VARCHAR(260)")
            }),
            new TableScheme("file_list", new List<Column>
            {
                new Column("id", "INTEGER", "PRIMARY KEY"),
                new Column("package_id", "INTEGER"),
                new Column("file_name", "VARCHAR(260)"),
                new Column("UNIQUE(package_id, file_name)", true)
            }),
            new TableScheme("installed_files", new List<Column>
            {
                new Column("id", "INTEGER", "PRIMARY KEY"),
                new Column("package_id", "INTEGER"),
                new Column("file_name", "VARCHAR(260)"),
                new Column("UNIQUE(package_id, file_name)", true)
            }),
            new TableScheme("dependency_list", new List<Column>
            {
                new Column("record_id", "INTEGER", "PRIMARY KEY"),
                new Column("package_id", "INTEGER NOT NULL"),
                new Column("dependency_package_id", "INTEGER NOT NULL")
            }),
            new TableScheme("remote_package_list", new List<Column>
            {
                new Column("id", "INTEGER", "PRIMARY KEY"),
                new Column("file_name", "VARCHAR (260)"),
                new Column("display_name", "VARCHAR(1000)"),
                new Column("category", "INTEGER"),
                new Column("era", "INTEGER"),
                new Column("country", "INTEGER"),
                new Column("version", "INTEGER"),
                new Column("owner", "INTEGER"),
                new Column("datetime", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"),
                new Column("description", "VARCHAR(10000)"),
                new Column("target_path", "VARCHAR(260)"),
                new Column("paid", "INTEGER"),
                new Column("steamappid", "INTEGER"),
                new Column("steam_dev", "VARCHAR(1024)")
            }),
            new TableScheme("remote_file_list", new List<Column>
            {
                new Column("id", "INTEGER", "PRIMARY KEY"),
                new Column("package_id", "INTEGER"),
                new Column("file_name", "VARCHAR(260)"),
                new Column("UNIQUE(package_id, file_name)", true)
            }),
            new TableScheme("remote_dependency_list", new List<Column>
            {
                new Column("record_id", "INTEGER", "PRIMARY KEY"),
                new Column("package_id", "INTEGER NOT NULL"),
                new Column("dependency_package_id", "INTEGER NOT NULL")
            })
        });

        private SQLiteConnection MemoryConn { get; set; }
        private SQLiteConnection FileConn { get; set; }

        private readonly object DBLock = new object();

        internal SqLiteAdapter(string path, bool isMainCache = false)
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
                ValidateDatabaseScheme(MainCache);
            else
                ValidateDatabaseScheme(RouteCache);
        }

        internal void FlushToFile(bool keepOpen = false)
        {
            PermissionSet permissionSet = new PermissionSet(PermissionState.None);
            FileIOPermission writePermission = new FileIOPermission(FileIOPermissionAccess.Write, DatabasePath);
            permissionSet.AddPermission(writePermission);

            if (permissionSet.IsSubsetOf(AppDomain.CurrentDomain.PermissionSet))
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
            else if (!IsAdministrator())
                ElevatePrivileges();
        }

        internal void SaveRoute(LoadedRoute route)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(SQLqueries.InsertChckSum, MemoryConn))
            {
                for (int i = 0; i < 7; i++)
                {
                    switch (i)
                    {
                        case 0:
                            cmd.Parameters.AddWithValue("folder", "loft");
                            cmd.Parameters.AddWithValue("chcksum", route.LoftChecksum);
                            cmd.Parameters.AddWithValue("last_write", route.LoftLastWrite);
                            break;
                        case 1:
                            cmd.Parameters.AddWithValue("folder", "road");
                            cmd.Parameters.AddWithValue("chcksum", route.RoadChecksum);
                            cmd.Parameters.AddWithValue("last_write", route.RoadLastWrite);
                            break;
                        case 2:
                            cmd.Parameters.AddWithValue("folder", "track");
                            cmd.Parameters.AddWithValue("chcksum", route.TrackChecksum);
                            cmd.Parameters.AddWithValue("last_write", route.TrackLastWrite);
                            break;
                        case 3:
                            cmd.Parameters.AddWithValue("folder", "scenery");
                            cmd.Parameters.AddWithValue("chcksum", route.SceneryChecksum);
                            cmd.Parameters.AddWithValue("last_write", route.SceneryLastWrite);
                            break;
                        case 4:
                            cmd.Parameters.AddWithValue("folder", "AP");
                            cmd.Parameters.AddWithValue("chcksum", route.APChecksum);
                            cmd.Parameters.AddWithValue("last_write", route.APLastWrite);
                            break;
                        case 5:
                            cmd.Parameters.AddWithValue("folder", "routeProperties");
                            cmd.Parameters.AddWithValue("chcksum", route.RoutePropertiesChecksum);
                            cmd.Parameters.AddWithValue("last_write", route.RoutePropertiesLastWrite);
                            break;
                        case 6:
                            cmd.Parameters.AddWithValue("folder", "scenarios");
                            cmd.Parameters.AddWithValue("chcksum", route.ScenariosChecksum);
                            cmd.Parameters.AddWithValue("last_write", route.ScenariosLastWrite);
                            break;
                    }
                    cmd.ExecuteNonQuery();
                }
            }

            using (SQLiteCommand cmd = new SQLiteCommand(SQLqueries.DeleteAllDeps, MemoryConn))
            {
                cmd.ExecuteNonQuery();
            }

            using (SQLiteCommand cmd = new SQLiteCommand(SQLqueries.InsertDeps, MemoryConn))
            {
                int depsCount = route.Dependencies.Count;
                for (int i = 0; i < depsCount + route.ScenarioDeps.Count; i++)
                {
                    if (i < depsCount)
                    {
                        if (string.IsNullOrWhiteSpace(route.Dependencies.ElementAt(i)))
                            continue;
                        cmd.Parameters.AddWithValue("path", route.Dependencies.ElementAt(i));
                        cmd.Parameters.AddWithValue("isScenario", false);
                        cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(route.ScenarioDeps.ElementAt(i - depsCount)))
                            continue;
                        cmd.Parameters.AddWithValue("path", route.ScenarioDeps.ElementAt(i - depsCount));
                        cmd.Parameters.AddWithValue("isScenario", true);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        internal LoadedRoute LoadSavedRoute()
        {
            LoadedRoute loadedRoute = new LoadedRoute();

            using (SQLiteCommand cmd = new SQLiteCommand(SQLqueries.SelectAllChckSums, MemoryConn))
            {
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime lastWrite = new DateTime();
                        object _lastWrite = reader["last_write"];
                        if (!(_lastWrite is DBNull))
                            lastWrite = Convert.ToDateTime(_lastWrite);

                        switch (reader["folder"])
                        {
                            case "loft":
                                loadedRoute.LoftChecksum = Convert.ToString(reader["chcksum"]);
                                loadedRoute.LoftLastWrite = lastWrite;
                                break;
                            case "road":
                                loadedRoute.RoadChecksum = Convert.ToString(reader["chcksum"]);
                                loadedRoute.RoadLastWrite = lastWrite;
                                break;
                            case "track":
                                loadedRoute.TrackChecksum = Convert.ToString(reader["chcksum"]);
                                loadedRoute.TrackLastWrite = lastWrite;
                                break;
                            case "scenarios":
                                loadedRoute.ScenariosChecksum = Convert.ToString(reader["chcksum"]);
                                loadedRoute.ScenariosLastWrite = lastWrite;
                                break;
                            case "scenery":
                                loadedRoute.SceneryChecksum = Convert.ToString(reader["chcksum"]);
                                loadedRoute.SceneryLastWrite = lastWrite;
                                break;
                            case "AP":
                                loadedRoute.APChecksum = Convert.ToString(reader["chcksum"]);
                                loadedRoute.APLastWrite = lastWrite;
                                break;
                            case "routeProperties":
                                loadedRoute.RoutePropertiesChecksum = Convert.ToString(reader["chcksum"]);
                                loadedRoute.RoutePropertiesLastWrite = lastWrite;
                                break;
                        }
                    }
                }
            }

            loadedRoute.Dependencies = new HashSet<string>();
            loadedRoute.ScenarioDeps = new HashSet<string>();

            using (SQLiteCommand cmd = new SQLiteCommand(SQLqueries.SelectAllDeps, MemoryConn))
            {
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
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
                }
            }

            return loadedRoute;
        }

        internal void SavePackage(Package package, bool cached = false)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(string.Format(SQLqueries.DeletePkgFiles, cached ? SQLqueries.RemoteFiles : SQLqueries.LocalFiles), MemoryConn))
            {
                cmd.Parameters.AddWithValue("id", package.PackageId);
                cmd.ExecuteNonQuery();
            }

            using (SQLiteCommand cmd = new SQLiteCommand(string.Format(SQLqueries.DeletePkgDeps, cached ? SQLqueries.RemoteDeps : SQLqueries.LocalDeps), MemoryConn))
            {
                cmd.Parameters.AddWithValue("id", package.PackageId);
                cmd.ExecuteNonQuery();
            }

            using (SQLiteCommand cmd = new SQLiteCommand(string.Format(cached ? SQLqueries.InsertRemotePkg : SQLqueries.InsertPkg, cached ? SQLqueries.RemotePackages : SQLqueries.LocalPackages), MemoryConn))
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
                if (cached)
                {
                    cmd.Parameters.AddWithValue("paid", package.IsPaid);
                    cmd.Parameters.AddWithValue("steamappid", package.SteamAppID);
                }
                cmd.ExecuteNonQuery();
            }

            using (SQLiteCommand cmd = new SQLiteCommand(string.Format(SQLqueries.InsertPkgFiles, cached ? SQLqueries.RemoteFiles : SQLqueries.LocalFiles), MemoryConn))
            {
                cmd.Parameters.AddWithValue("package_id", package.PackageId);
                foreach (string file in package.FilesContained)
                {
                    cmd.Parameters.AddWithValue("file_name", file);
                    cmd.ExecuteNonQuery();
                }
            }

            using (SQLiteCommand cmd = new SQLiteCommand(string.Format(SQLqueries.InsertPkgDeps, cached ? SQLqueries.RemoteDeps : SQLqueries.LocalDeps), MemoryConn))
            {
                cmd.Parameters.AddWithValue("package_id", package.PackageId);
                foreach (int pkgDepId in package.Dependencies)
                {
                    cmd.Parameters.AddWithValue("dependency_package_id", pkgDepId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        internal void RemoveInstalledPackage(int pkgId)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(string.Format(SQLqueries.DeletePkgFiles, SQLqueries.LocalDeps), MemoryConn))
            {
                cmd.Parameters.AddWithValue("id", pkgId);
                cmd.ExecuteNonQuery();
            }

            using (SQLiteCommand cmd = new SQLiteCommand(string.Format(SQLqueries.DeletePkgDeps, SQLqueries.LocalDeps), MemoryConn))
            {
                cmd.Parameters.AddWithValue("id", pkgId);
                cmd.ExecuteNonQuery();
            }

            using (SQLiteCommand cmd = new SQLiteCommand(string.Format(SQLqueries.DeletePkg, SQLqueries.LocalPackages), MemoryConn))
            {
                cmd.Parameters.AddWithValue("id", pkgId);
                cmd.ExecuteNonQuery();
            }
        }

        internal List<Package> LoadPackages(bool cached = false)
        {
            List<Package> loadedPackages = new List<Package>();

            using (SQLiteCommand pkgCommand = new SQLiteCommand(string.Format(SQLqueries.SelectAllPkgs, cached ? SQLqueries.RemotePackages : SQLqueries.LocalPackages), MemoryConn))
            {
                using (SQLiteDataReader pkgReader = pkgCommand.ExecuteReader())
                {
                    using (SQLiteCommand filesCommand = new SQLiteCommand(string.Format(SQLqueries.SelectPkgFilesWhere, cached ? SQLqueries.RemoteFiles : SQLqueries.LocalFiles), MemoryConn))
                    {
                        using (SQLiteCommand dependenciesCommand = new SQLiteCommand(string.Format(SQLqueries.SelectPkgDepsWhere, cached ? SQLqueries.RemoteDeps : SQLqueries.LocalDeps), MemoryConn))
                        {
                            while (pkgReader.Read())
                            {
                                Package loadedPackage = cached ? new Package(
                                    Convert.ToInt32(pkgReader["id"]),
                                    Convert.ToString(pkgReader["display_name"]),
                                    Convert.ToInt32(pkgReader["category"]),
                                    Convert.ToInt32(pkgReader["era"]),
                                    Convert.ToInt32(pkgReader["country"]),
                                    Convert.ToInt32(pkgReader["owner"]),
                                    Convert.ToString(pkgReader["datetime"]),
                                    Convert.ToString(pkgReader["target_path"]),
                                    new List<string>(),
                                    Convert.ToString(pkgReader["file_name"]),
                                    Convert.ToString(pkgReader["description"]),
                                    Convert.ToInt32(pkgReader["version"]),
                                    Convert.ToBoolean(pkgReader["paid"]),
                                    Convert.ToInt32(pkgReader["steamappid"])
                                ) : new Package(
                                    Convert.ToInt32(pkgReader["id"]),
                                    Convert.ToString(pkgReader["display_name"]),
                                    Convert.ToInt32(pkgReader["category"]),
                                    Convert.ToInt32(pkgReader["era"]),
                                    Convert.ToInt32(pkgReader["country"]),
                                    Convert.ToInt32(pkgReader["owner"]),
                                    Convert.ToString(pkgReader["datetime"]),
                                    Convert.ToString(pkgReader["target_path"]),
                                    new List<string>(),
                                    Convert.ToString(pkgReader["file_name"]),
                                    Convert.ToString(pkgReader["description"]),
                                    Convert.ToInt32(pkgReader["version"])
                                );

                                filesCommand.Parameters.AddWithValue("package_id", loadedPackage.PackageId);
                                using (SQLiteDataReader filesReader = filesCommand.ExecuteReader())
                                {
                                    while (filesReader.Read())
                                    {
                                        loadedPackage.FilesContained.Add(NormalizePath(Convert.ToString(filesReader["file_name"])));
                                    }
                                }

                                dependenciesCommand.Parameters.AddWithValue("package_id", loadedPackage.PackageId);
                                using (SQLiteDataReader dependenciesReader = dependenciesCommand.ExecuteReader())
                                {
                                    while (dependenciesReader.Read())
                                    {
                                        loadedPackage.Dependencies.Add(Convert.ToInt32(dependenciesReader["dependency_package_id"]));
                                    }
                                }

                                loadedPackages.Add(loadedPackage);
                            }
                        }
                    }
                }
            }

            return loadedPackages;
        }

        internal void SavePackageFiles(int id, List<string> filesToAdd)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(SQLqueries.InsertInstalledFiles, MemoryConn))
            {
                cmd.Parameters.AddWithValue("package_id", id);
                foreach (string file in filesToAdd)
                {
                    try
                    {
                        cmd.Parameters.AddWithValue("file_name", file);
                        cmd.ExecuteNonQuery();
                    }
                    catch { }
                }
            }
        }

        internal void RemovePackageFiles(List<string> filesToRemove)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(SQLqueries.DeleteInstalledFilesWhere, MemoryConn))
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

            using (SQLiteCommand cmd = new SQLiteCommand(SQLqueries.SelectInstalledFilesWhere, MemoryConn))
            {
                cmd.Parameters.AddWithValue("package_id", id);
                SQLiteDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    loadedFiles.Add(Convert.ToString(r["file_name"]));
                }
            }

            return loadedFiles;
        }

        private void ValidateTableScheme(TableScheme tableScheme)
        {
            try
            {
                using (SQLiteCommand cmd = new SQLiteCommand(string.Format(SQLqueries.ListTableRows, tableScheme.Name), MemoryConn))
                {
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Column col = tableScheme.Cols.FirstOrDefault(x => x.Name == (string)reader["name"]);
                            if (col != null)
                                col.Validated = true;
                        }
                    }
                }

                foreach (Column col in tableScheme.Cols.Where(x => !x.Validated && !x.IsKey))
                    using (SQLiteCommand cmd = new SQLiteCommand(string.Format(SQLqueries.AddColumn, tableScheme.Name, col.Name, col.Type), MemoryConn))
                        cmd.ExecuteNonQuery();
            }
            catch
            {
                using (SQLiteCommand cmd = new SQLiteCommand(tableScheme.SQL, MemoryConn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ValidateDatabaseScheme(DatabaseScheme database)
        {
            foreach (TableScheme tableScheme in database.Tables)
                ValidateTableScheme(tableScheme);
        }
    }
}