using System.Linq;
using System.Windows;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro DependencyDetailsWindow.xaml
    /// </summary>
    public partial class DependencyDetailsWindow : Window
    {
        public DependencyDetailsWindow(DependencyPackage package, RouteInfo info)
        {
            InitializeComponent();

            PackageFilesList.ItemsSource = info.ParsedDependencies.Items.Where(x => x.PkgID == package.ID);
            Title = string.Format(Localization.Strings.DepsDetailsWindowTitle, package.Name, info.Name);
        }
    }
}
