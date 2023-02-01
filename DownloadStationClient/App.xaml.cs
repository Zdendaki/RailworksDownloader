using DownloadStationClient.API;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;

namespace DownloadStationClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#if DEBUG
        internal const bool DEBUG = true;
        internal const string API_URL = "https://dls.rw.jachyhm.cz/api/";
#else
        internal const bool DEBUG = false;
        internal const string API_URL = "https://dls.rw.jachyhm.cz/api/";
#endif

        internal static MainWindow Window { get; set; } = null!;

        internal static User UserData { get; set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
    }
}
