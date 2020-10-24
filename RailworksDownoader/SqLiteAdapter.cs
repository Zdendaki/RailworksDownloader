using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;

namespace RailworksDownoader
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
            SQLiteCommand cmd = new SQLiteCommand("INSERT INTO checksums (id, folder, chcksum) VALUES (@id,@folder,@chcksum) ON CONFLICT(id) DO UPDATE SET id = @id, folder = @folder, chcksum = @chcksum;", con);
            for (int i = 0; i < 7; i++)
            {
                cmd.Parameters.AddWithValue("@id", i);
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
            SQLiteCommand insertSQL = new SQLiteCommand("INSERT INTO dependencies (id, path) VALUES (@id,@path);", con);
            for (int i = 0; i < route.Dependencies.Count; i++)
            {
                insertSQL.Parameters.AddWithValue("@id", i);
                insertSQL.Parameters.AddWithValue("@path", route.Dependencies[i]);
                insertSQL.ExecuteNonQuery();
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
                command = new SQLiteCommand(con)
                {
                    CommandText = "SELECT * FROM dependencies"
                };
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    loadedRoute.Dependencies.Add(Convert.ToString(reader["path"]));
                }
                command.Dispose();
                con.Close();
                con.Dispose();
            }
            else
            {
                CreateCacheFile();
            }

            return loadedRoute;
        }

        internal void CreateCacheFile()
        {
            SQLiteConnection con = new SQLiteConnection(ConnectionString);
            con.Open();
            SQLiteCommand command = new SQLiteCommand(con)
            {
                CommandText = "CREATE TABLE dependencies (id INT UNIQUE, path TEXT);CREATE TABLE checksums (id INT UNIQUE, folder VARCHAR(32), chcksum VARCHAR(32));"
            };
            command.ExecuteNonQuery();
            command.Dispose();

            con.Close();
            con.Dispose();
        }
    }
}