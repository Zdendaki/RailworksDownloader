using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
                foreach (Dependency dep in info.ParsedDependencies.Items)
                {
                    if (!dep.IsScenario)
                        Dependencies.Add(dep);
                    else
                        ScenarioDeps.Add(dep);
                }

                Title = info.Name;
            }

            Dependencies = Dependencies.OrderBy(x => x.State).ThenBy(x => x.Name).ToList();
            ScenarioDeps = ScenarioDeps.OrderBy(x => x.State).ThenBy(x => x.Name).ToList();
            DependenciesList.ItemsSource = Dependencies;
            ScenarioDepsList.ItemsSource = ScenarioDeps;

            CollectionView depsView = (CollectionView)CollectionViewSource.GetDefaultView(DependenciesList.ItemsSource);
            PropertyGroupDescription depsGroups = new PropertyGroupDescription("State");
            depsView.GroupDescriptions.Add(depsGroups);


            CollectionView scenView = (CollectionView)CollectionViewSource.GetDefaultView(ScenarioDepsList.ItemsSource);
            PropertyGroupDescription scenGroups = new PropertyGroupDescription("State");
            scenView.GroupDescriptions.Add(scenGroups);
        }

        private void ListViewItem_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }
    }
}
