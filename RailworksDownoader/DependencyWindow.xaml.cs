using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro DependencyWindow.xaml
    /// </summary>
    public partial class DependencyWindow : Window
    {
        private readonly List<Dependency> Dependencies;
        private readonly List<Dependency> ScenarioDeps;
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
