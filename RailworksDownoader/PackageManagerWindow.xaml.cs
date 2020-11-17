using System.Windows;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro PackageManagerWindow.xaml
    /// </summary>
    public partial class PackageManagerWindow : Window
    {
        InstallPackageDialog IPD;
        PackageManager PM { get; set; }

        public PackageManagerWindow(PackageManager pm)
        {
            InitializeComponent();
            PM = pm;
            IPD = new InstallPackageDialog();

            PackagesList.ItemsSource = pm.InstalledPackages;
        }

        private void InstallPackage_Click(object sender, RoutedEventArgs e)
        {
            IPD.ShowAsync();
        }
    }
}
