using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RailworksDownloader
{
    internal class Cleanup
    {
        private static readonly string[] versions = new string[] { "1.2.0.0" };
        private readonly string version;

        public Cleanup()
        {
            version = Assembly.GetEntryAssembly()!.GetName().Version!.ToString();
        }

        public void PerformCleanup()
        {
            if (App.Railworks.RWPath == null)
                return;

            if (versions.Contains(version) && !App.Settings.PerformedCleanups.Contains(version))
            {
                foreach (string route in Directory.GetDirectories(Path.Combine(App.Railworks.RWPath, "Content", "Routes")))
                {
                    try
                    {
                        File.Delete(Path.Combine(route, "cache.dls"));
                    }
                    catch { }
                }

                App.Settings.PerformedCleanups.Add(version);
                App.Settings.Save();
            }
        }
    }
}
