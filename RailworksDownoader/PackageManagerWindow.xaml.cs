using System.Windows;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro PackageManagerWindow.xaml
    /// </summary>
    public partial class PackageManagerWindow : Window
    {
        InstallPackageDialog IPD;

        public PackageManagerWindow()
        {
            InitializeComponent();
            IPD = new InstallPackageDialog();
        }

        private void InstallPackage_Click(object sender, RoutedEventArgs e)
        {
            IPD.ShowAsync();
        }
    }
}
