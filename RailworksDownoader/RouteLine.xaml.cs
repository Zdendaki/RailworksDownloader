using System;
using System.Collections.Generic;
using System.Linq;
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

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro RouteLine.xaml
    /// </summary>
    public partial class RouteLine : UserControl
    {
        public string RouteName { get; set; }

        public int RouteProgress { get; set; }
        
        public RouteLine(string name, int progress)
        {
            InitializeComponent();

            RouteName = name;
            RouteProgress = progress;
        }

        public RouteLine()
        {
            InitializeComponent();

            RouteName = "";
            RouteProgress = 0;
        }
    }
}
