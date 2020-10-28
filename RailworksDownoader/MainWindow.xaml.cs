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

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Railworks RW;
        
        public MainWindow()
        {
            InitializeComponent();

            RW = new Railworks(Settings.Default.RailworksLocation);
            RW.ProgressUpdated += RW_ProgressUpdated;
            RW.RouteSaving += RW_RouteSaving;

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

            //RoutesList.Items.Add(new RouteInfo("TEST", ""));


        }

        private void RW_RouteSaving(bool saved)
        {
            SavingGrid.Dispatcher.Invoke(() => { SavingGrid.Visibility = saved ? Visibility.Hidden : Visibility.Visible; });
        }

        private void RW_ProgressUpdated(int percent)
        {
            TotalProgress.Dispatcher.Invoke(() => { TotalProgress.Value = percent; });

            //Dispatcher.Invoke(() => { UpdateLayout(); });
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
            RW.RunAllCrawlers();
        }
    }
}
