using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RailworksDownloader
{
    class Cleanup
    {
        static readonly string[] versions = new string[] { "1.2.0.0" };

        string version;
        
        public Cleanup()
        {
            version = Assembly.GetEntryAssembly().GetName().Version.ToString();
        }

        public void PerformCleanup()
        {
            if (versions.Contains(version) && !App.Settings.PerformedCleanups.Contains(version))
            {
                foreach (var route in Directory.GetDirectories(Path.Combine(App.Railworks.RWPath, "Content", "Routes")))
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
