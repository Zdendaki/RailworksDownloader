using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

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

        internal static Settings Settings { get; set; }

        internal static string Token { get; set; }

        internal static string Version { get; set; }

        internal static bool IsDownloading { get; set; } = false;

        internal static bool AutoDownload { get; set; } = true;

        internal static bool Debug { get; set; } = false;

        internal static bool ReportErrors { get; set; } = true;

        internal static IDisposable Sentry { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            Sentry = SentrySdk.Init("https://b3e42d20f2524d6b9e71b51b446929e8@o572516.ingest.sentry.io/5722005");

            if (!Debugger.IsAttached)
            {
                DispatcherUnhandledException += App_DispatcherUnhandledException;
            }

            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            if (e.Args.Length > 0)
            {
                for (int i = 0; i < e.Args.Length; i++)
                {
                    if (e.Args[i][0] == '-')
                    {
                        switch (e.Args[i].ToLower())
                        {
                            case "-preventautostart":
                                AutoDownload = false;
                                break;
                            case "-debug":
                                Debug = false;
                                break;
                        }
                    }
                    else
                    {
                        string[] parts = e.Args[i].Split(':');
                        if (parts.Count() == 2 && parts[0].ToLower() == "dls")
                        {
                            if (int.TryParse(parts[1], out int pkgId))
                            {
                                string queueFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DLS.queue");
                                HashSet<string> queuedPkgs = System.IO.File.Exists(queueFile) ? System.IO.File.ReadAllText(queueFile).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet() : new HashSet<string>();
                                queuedPkgs.Add(pkgId.ToString());
                                System.IO.File.WriteAllText(queueFile, string.Join(",", queuedPkgs));
                            }
                        }
                    }
                }
            }

            string thisprocessname = Process.GetCurrentProcess().ProcessName;
            if (Process.GetProcesses().Count(p => p.ProcessName == thisprocessname) > 1)
                Environment.Exit(0);

            Settings = new Settings();
            Settings.Load();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Settings.Save();

            Sentry.Dispose();
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                Trace.Assert(false, e.Exception.Message, e.Exception.ToString());
                e.Handled = true;
            }
            catch (Exception)
            {
                e.Handled = false;
            }
        }
    }
}
