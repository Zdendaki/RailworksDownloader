using Microsoft.Win32;
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
using System.Windows.Shapes;

namespace RailworksDownoader
{
    /// <summary>
    /// Interakční logika pro FindPath.xaml
    /// </summary>
    public partial class FindPath : Window
    {
        public FindPath()
        {
            InitializeComponent();

        }

        private void PathSelect_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.FileName = "RailWorks.exe";
            ofd.Filter = "RailWorks.exe|RailWorks.exe";
            
        }
    }
}
