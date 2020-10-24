using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Shapes;

namespace RailworksDownoader
{
    /// <summary>
    /// Interakční logika pro MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool DeleteFileW([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

        public MainWindow()
        {
            InitializeComponent();

            ContentDialog cd = new ContentDialog()
            {

            };
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            Railworks rw = new Railworks();

            Stopwatch sw = new Stopwatch();

            RouteCrawler rc = new RouteCrawler(@"D:\Hry\Steam\steamapps\common\RailWorks\Content\Routes\bd4aae03-09b5-4149-a133-297420197356", rw.RWPath);


            rc.ProgressUpdated += (perc) => { PB.Value = perc; };
            rc.Complete += () => 
            { 
                sw.Stop();
                MessageBox.Show(sw.Elapsed.ToString());
            };

            sw.Start();
            await rc.Start();
        }
    }
}
