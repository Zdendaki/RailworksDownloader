using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using static RailworksDownloader.Properties.Settings;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal static MainWindow Window { get; set; }

        internal static Railworks Railworks { get; set; }

        internal static SteamManager SteamManager { get; set; }

        internal static PackageManager PackageManager { get; set; }

        internal static string Token { get; set; }

        internal static string Version { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            if (e.Args.Length > 0)
            {
                for (int i = 0; i < e.Args.Length; i++)
                {
                    string[] parts = e.Args[i].Split(':');
                    if (parts.Count() == 2)
                    {
                        if (int.TryParse(parts[1], out int pkgId))
                        {
                            string queueFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DLS.queue");
                            HashSet<string> queuedPkgs = System.IO.File.Exists(queueFile) ? System.IO.File.ReadAllText(queueFile).Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries).ToHashSet() : new HashSet<string>();
                            queuedPkgs.Add(pkgId.ToString());
                            System.IO.File.WriteAllText(queueFile, string.Join(",", queuedPkgs));
                        }
                    }
                }
            }

            string thisprocessname = Process.GetCurrentProcess().ProcessName;
            if (Process.GetProcesses().Count(p => p.ProcessName == thisprocessname) > 1)
                Environment.Exit(0);

            if (Default.UpgradeRequired)
            {
                Default.Upgrade();
                Default.UpgradeRequired = false;
                Default.Save();
            }
        }
    }
}
