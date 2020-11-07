using System.Windows;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro PackageManagerWindow.xaml
    /// </summary>
    public partial class PackageManagerWindow : Window
    {
        public PackageManagerWindow()
        {
            InitializeComponent();
        }

        private void InstallPackage_Click(object sender, RoutedEventArgs e)
        {
            InstallPackageDialog ipd = new InstallPackageDialog();
            ipd.ShowAsync();
        }
    }
}
