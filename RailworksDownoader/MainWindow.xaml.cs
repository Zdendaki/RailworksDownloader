using ModernWpf.Controls;
using System;
using System.Collections.Generic;
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

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Railworks RW = new Railworks();
        
        public MainWindow()
        {
            InitializeComponent();

            RoutesList.Items.Add(new RouteItemData("TEST", 24));
            RoutesList.Items.Add(new RouteItemData("TEST", 100));
            RoutesList.Items.Add(new RouteItemData("TEST", 100));
            RoutesList.Items.Add(new RouteItemData("TEST", 2));

            RailworksPathDialog rpd = new RailworksPathDialog();
            rpd.ShowAsync();
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
            string path = Railworks.GetRWPath();

            if (path == null)
            {
                RailworksPathDialog rpd = new RailworksPathDialog();
                rpd.ShowAsync();
            }

            Properties.Settings.Default.RailworksLocation = path;
        }
    }
}
