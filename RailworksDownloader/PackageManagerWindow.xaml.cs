using System.Diagnostics;
using System.Windows;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro PackageManagerWindow.xaml
    /// </summary>
    public partial class PackageManagerWindow : Window
    {
        private readonly InstallPackageDialog IPD;

        private PackageManager PM { get; set; }

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

        private void RemoveSelectedPackage_Click(object sender, RoutedEventArgs e)
        {
            foreach (Package package in PackagesList.SelectedItems)
            {
                PM.RemovePackage(package.PackageId);
            }
            PackagesList.ItemsSource = null;
            PackagesList.ItemsSource = PM.InstalledPackages;
        }

        private void PackagesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RemoveSelectedPackage.IsEnabled = PackagesList.SelectedItems.Count > 0;
        }

        private void PackagesList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            int id = ((Package)PackagesList.SelectedItem).PackageId;
            Process.Start($"https://dls.rw.jachyhm.cz?package={id}");
        }
    }
}
