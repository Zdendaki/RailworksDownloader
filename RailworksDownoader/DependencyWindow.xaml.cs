using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro DependencyWindow.xaml
    /// </summary>
    public partial class DependencyWindow : Window
    {
        List<Dependency> Dependencies;
        List<Dependency> ScenarioDeps;
        public DependencyWindow(RouteInfo info)
        {
            InitializeComponent();

            Dependencies = new List<Dependency>();
            ScenarioDeps = new List<Dependency>();

            if (info != null)
            {
                info.Crawler?.DownloadableDependencies.ForEach(x => Dependencies.Add(new Dependency(x, DependencyState.Available)));
                info.Crawler?.MissingDependencies.Except(info.Crawler?.DownloadableDependencies).ToList().ForEach(x => Dependencies.Add(new Dependency(x, DependencyState.Unavailable)));
                info.Crawler?.Dependencies.Except(info.Crawler?.MissingDependencies).ToList().ForEach(x => Dependencies.Add(new Dependency(x, DependencyState.Downloaded)));

                info.Crawler?.DownloadableScenarioDeps.ForEach(x => ScenarioDeps.Add(new Dependency(x, DependencyState.Available)));
                info.Crawler?.MissingScenarioDeps.Except(info.Crawler?.DownloadableScenarioDeps).ToList().ForEach(x => ScenarioDeps.Add(new Dependency(x, DependencyState.Unavailable)));
                info.Crawler?.ScenarioDeps.Except(info.Crawler?.MissingScenarioDeps).ToList().ForEach(x => ScenarioDeps.Add(new Dependency(x, DependencyState.Downloaded)));

                Title = info.Name;
            }

            Dependencies = Dependencies.OrderBy(x => x.State).ThenBy(x => x.Name).ToList();
            ScenarioDeps = ScenarioDeps.OrderBy(x => x.State).ThenBy(x => x.Name).ToList();
            DependenciesList.ItemsSource = Dependencies;
            ScenarioDepsList.ItemsSource = ScenarioDeps;
        }

        private void ListViewItem_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }
    }
}
