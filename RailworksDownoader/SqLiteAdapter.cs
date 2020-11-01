using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;

namespace RailworksDownloader
{
    internal class SqLiteAdapter
    {
        internal string DatabasePath { get; set; }

        string ConnectionString;

        public SqLiteAdapter(string path)
        {
            DatabasePath = path;
            ConnectionString = $"Data Source={DatabasePath}; Version = 3; Compress = True;";
        }

        internal void SaveRoute(LoadedRoute route)
        {
            SQLiteConnection con = new SQLiteConnection(ConnectionString);
            con.Open();
            SQLiteCommand cmd = new SQLiteCommand("INSERT INTO checksums (folder, chcksum) VALUES (@folder,@chcksum) ON CONFLICT(folder) DO UPDATE SET folder = @folder, chcksum = @chcksum;", con);
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

            SQLiteCommand clear = new SQLiteCommand("DELETE FROM dependencies;", con);
            clear.ExecuteNonQuery();
            SQLiteCommand insertSQL = new SQLiteCommand("INSERT INTO dependencies (path, isScenario) VALUES (@path,@isScenario);", con);
            int depsCount = route.Dependencies.Count;
            for (int i = 0; i < depsCount + route.ScenarioDeps.Count; i++)
            {
                if (i < depsCount)
                {
                    insertSQL.Parameters.AddWithValue("@path", route.Dependencies[i]);
                    insertSQL.Parameters.AddWithValue("@isScenario", false);
                    insertSQL.ExecuteNonQuery();
                } 
                else
                {
                    insertSQL.Parameters.AddWithValue("@path", route.ScenarioDeps[i - depsCount]);
                    insertSQL.Parameters.AddWithValue("@isScenario", true);
                    insertSQL.ExecuteNonQuery();
                }
            }

            con.Close();
            con.Dispose();
        }

        internal LoadedRoute LoadSavedRoute(bool isAP = false)
        {
            LoadedRoute loadedRoute = new LoadedRoute();

            if (File.Exists(DatabasePath))
            {
                SQLiteConnection con = new SQLiteConnection(ConnectionString);
                con.Open();
                SQLiteCommand command = new SQLiteCommand(con)
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

                loadedRoute.Dependencies = new List<string>();
                loadedRoute.ScenarioDeps = new List<string>();
                command = new SQLiteCommand(con)
                {
                    CommandText = "SELECT * FROM dependencies"
                };
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    switch (Convert.ToInt32(reader["isScenario"]))
                    {
                        case 1:
                            loadedRoute.ScenarioDeps.Add(Convert.ToString(reader["path"]));
                            break;
                        default:
                            loadedRoute.Dependencies.Add(Convert.ToString(reader["path"]));
                            break;
                    }
                }
                command.Dispose();
                con.Close();
                con.Dispose();
            }
            else
            {
                CreateRouteCacheFile();
            }

            return loadedRoute;
        }

        internal void CreateRouteCacheFile()
        {
            SQLiteConnection con = new SQLiteConnection(ConnectionString);
            con.Open();
            SQLiteCommand command = new SQLiteCommand(con)
            {
                CommandText = "CREATE TABLE dependencies (id INTEGER PRIMARY KEY, path TEXT, isScenario INTEGER);CREATE TABLE checksums (id INTEGER PRIMARY KEY, folder VARCHAR(32) UNIQUE, chcksum VARCHAR(32));"
            };
            command.ExecuteNonQuery();
            command.Dispose();

            con.Close();
            con.Dispose();
        }

        internal void CreateMainCacheFile()
        {
            SQLiteConnection con = new SQLiteConnection(ConnectionString);
            con.Open();
            SQLiteCommand command = new SQLiteCommand(con)
            {
                CommandText = @"CREATE TABLE package_list (
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
CREATE TABLE file_list (
    id INTEGER PRIMARY KEY,
    package_id INTEGER,
    file_name VARCHAR(260)
)"
            };
            command.ExecuteNonQuery();
            command.Dispose();

            con.Close();
            con.Dispose();
        }

        internal void SaveInstalledPackage(Package package)
        {
            if (!File.Exists(DatabasePath))
                CreateMainCacheFile();

            SQLiteConnection con = new SQLiteConnection(ConnectionString);
            con.Open();

            SQLiteCommand cmd = new SQLiteCommand("DELETE FROM file_list;", con);
            cmd.ExecuteNonQuery();
            cmd.Dispose();

            cmd = new SQLiteCommand("INSERT INTO package_list (id, file_name, display_name, category, era, country, version, owner, datetime, description, target_path) VALUES (@id,@file_name,@display_name,@category,@era,@country,@version,@owner,@datetime,@desription,@target_path) ON CONFLICT(folder) DO UPDATE SET file_name = @file_name, display_name = @display_name, category = @category, era = @era, country = @country, version = @version, owner = @owner, datetime = @datetime, description = @description, version = @version;", con);
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
            cmd.Dispose();

            cmd = new SQLiteCommand("INSERT INTO file_list (package_id, file_name) VALUES (@package_id,@file_name);", con);
            cmd.Parameters.AddWithValue("package_id", package.PackageId);
            foreach (string file in package.DepsContained)
            {
                cmd.Parameters.AddWithValue("file_name", file);
                cmd.ExecuteNonQuery();
            }

            cmd.Dispose();
            con.Close();
            con.Dispose();
        }

        internal List<Package> LoadInstalledPackages()
        {
            List<Package> loadedPackages = new List<Package>();

            if (File.Exists(DatabasePath))
            {
                SQLiteConnection con = new SQLiteConnection(ConnectionString);
                con.Open();
                SQLiteCommand command = new SQLiteCommand(con)
                {
                    CommandText = "SELECT * FROM package_list;"
                };
                SQLiteDataReader reader = command.ExecuteReader();
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

                    SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM file_list WHERE package_id = @package_id;", con);
                    cmd.Parameters.AddWithValue("@package_id", loadedPackage.PackageId);
                    SQLiteDataReader r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        loadedPackage.DepsContained.Add(Convert.ToString(r["file_name"]));
                    }
                    cmd.Dispose();

                    loadedPackages.Add(loadedPackage);
                }
                command.Dispose();

                con.Close();
                con.Dispose();
            }
            else
            {
                CreateMainCacheFile();
            }

            return loadedPackages;
        }
    }
}