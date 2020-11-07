using Microsoft.Win32;
using System.Windows;

namespace RailworksDownloader
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
            OpenFileDialog ofd = new OpenFileDialog
            {
                FileName = "RailWorks.exe",
                Filter = "RailWorks.exe|RailWorks.exe"
            };

        }
    }
}
