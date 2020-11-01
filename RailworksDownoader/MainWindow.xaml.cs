using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using RailworksDownloader.Properties;
using SWC = System.Windows.Controls;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal static Brush Blue = new SolidColorBrush(Color.FromArgb(255, 0, 151, 230));
        internal static Brush Green = new SolidColorBrush(Color.FromArgb(255, 76, 209, 55));
        internal static Brush Yellow = new SolidColorBrush(Color.FromArgb(255, 251, 197, 49));
        internal static Brush Red = new SolidColorBrush(Color.FromArgb(255, 232, 65, 24));
        internal static Brush Purple = new SolidColorBrush(Color.FromArgb(255, 190, 46, 221));

        Railworks RW;

        PackageManager PM;

        bool crawlingComplete = false;
        
        public MainWindow()
        {
            InitializeComponent();

            App.Window = this;
            App.Railworks = new Railworks();

            App.Railworks = new Railworks(Settings.Default.RailworksLocation);
            App.Railworks.ProgressUpdated += RW_ProgressUpdated;
            App.Railworks.RouteSaving += RW_RouteSaving;
            App.Railworks.CrawlingComplete += RW_CrawlingComplete;

            RW = App.Railworks;

            App.PackageManager = new PackageManager(RW.RWPath);
            PM = App.PackageManager;

            if (string.IsNullOrWhiteSpace(RW.RWPath))
            {
                RailworksPathDialog rpd = new RailworksPathDialog();
                rpd.ShowAsync();
            }

            if (string.IsNullOrWhiteSpace(Settings.Default.RailworksLocation) && !string.IsNullOrWhiteSpace(RW.RWPath))
            {
                Settings.Default.RailworksLocation = RW.RWPath;
                Settings.Default.Save();
            }

            PathChanged();

            Settings.Default.PropertyChanged += PropertyChanged;

            if (!string.IsNullOrWhiteSpace(RW.RWPath))
                ScanRailworks_Click(this, null);

            //RoutesList.Items.Add(new RouteInfo("TEST", ""));
        }

        private async void RW_CrawlingComplete()
        {
            TotalProgress.Dispatcher.Invoke(() => TotalProgress.IsIndeterminate = true);
            await RW.GetMissing();
            await PM.GetDownloadableDependencies(RW.MissingDependencies);

            foreach (RouteInfo route in RW.Routes)
            {
                route.Crawler.ParseRouteMissingAssets(RW.MissingDependencies);
                route.Crawler.ParseRouteDownloadableAssets(PM.DownloadableDependencies);

                route.MissingCount = route.Crawler.MissingDependencies.Count;
                route.DownloadableCount = route.Crawler.DownloadableDependencies.Count;

                route.MissingScenariosCount = route.Crawler.MissingScenarioDeps.Count;
                route.DownloadableScenarioCount = route.Crawler.DownloadableScenarioDeps.Count;
            }

            TotalProgress.Dispatcher.Invoke(() => TotalProgress.IsIndeterminate = false);
            ScanRailworks.Dispatcher.Invoke(() => ScanRailworks.IsEnabled = true);
            crawlingComplete = true;
        }

        private void RW_RouteSaving(bool saved)
        {
            SavingGrid.Dispatcher.Invoke(() => { SavingGrid.Visibility = saved ? Visibility.Hidden : Visibility.Visible; });
        }

        private void RW_ProgressUpdated(int percent)
        {
            TotalProgress.Dispatcher.Invoke(() => { TotalProgress.Value = percent; });
        }

        private void PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "RailworksLocation")
            {
                RW.RWPath = Settings.Default.RailworksLocation;
                PathChanged();
            }
        }

        private void PathChanged()
        {
            PathSelected.IsChecked = ScanRailworks.IsEnabled = !string.IsNullOrWhiteSpace(RW.RWPath);

            LoadRoutes();
        }

        private void LoadRoutes()
        {
            if (string.IsNullOrWhiteSpace(RW.RWPath))
                return;

            RW.InitRoutes();

            foreach (var r in RW.Routes.OrderBy(x => x.Name))
            {
                RoutesList.Items.Add(r);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Railworks rw = new Railworks();

            Stopwatch sw = new Stopwatch();

            rw.InitCrawlers();

            //rw.ProgressUpdated += (perc) => { PB.Dispatcher.Invoke(() => { PB.Value = perc; }); };

            rw.RunAllCrawlers();

            //RouteCrawler rc = new RouteCrawler(@"D:\Hry\Steam\steamapps\common\RailWorks\Content\Routes\bd4aae03-09b5-4149-a133-297420197356", rw.RWPath);
            /*RouteCrawler rc = new RouteCrawler(Path.Combine(rw.RWPath, "Content", "Routes", "bd4aae03-09b5-4149-a133-297420197356"), rw.RWPath);


            rc.ProgressUpdated += (perc) => { PB.Value = perc; };
            rc.Complete += () => 
            { 
                sw.Stop();
                MessageBox.Show(sw.Elapsed.ToString());
            };

            sw.Start();
            await rc.Start();*/
        }

        private void SelectRailworksLocation_Click(object sender, RoutedEventArgs e)
        {
            RailworksPathDialog rpd = new RailworksPathDialog();
            rpd.ShowAsync();
        }

        private void ScanRailworks_Click(object sender, RoutedEventArgs e)
        {
            ScanRailworks.IsEnabled = false;
            crawlingComplete = false;
            TotalProgress.Value = 0;
            RW.RunAllCrawlers();
        }

        private void ListViewItem_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SWC.ListViewItem item = (SWC.ListViewItem)sender;

            if (item?.IsSelected == true && crawlingComplete)
            {
                DependencyWindow dw = new DependencyWindow((RouteInfo)item.Content);
                dw.ShowDialog();
            }
        }
    }
}
