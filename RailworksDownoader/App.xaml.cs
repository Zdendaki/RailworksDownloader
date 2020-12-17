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

        protected override void OnStartup(StartupEventArgs e)
        {
            if (Default.UpgradeRequired)
            {
                Default.Upgrade();
                Default.UpgradeRequired = false;
                Default.Save();
            }
        }
    }
}
