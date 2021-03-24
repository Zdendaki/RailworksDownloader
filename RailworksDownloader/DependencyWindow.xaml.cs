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
        private readonly List<DependencyPackage> Packages;
        private readonly List<DependencyPackage> ScenarioPkgs;

        public DependencyWindow(RouteInfo info, PackageManager pm)
        {
            InitializeComponent();

            Dependencies = new List<Dependency>();
            ScenarioDeps = new List<Dependency>();
            Packages = new List<DependencyPackage>();
            ScenarioPkgs = new List<DependencyPackage>();
            HashSet<string> parsedFiles = new HashSet<string>(); 

            if (info != null)
            {
                foreach (Dependency dep in info.ParsedDependencies.Items)
                {
                    if (dep.State == DependencyState.Unknown)
                    {
                        if (dep.IsRoute)
                            Dependencies.Add(dep);

                        if (dep.IsScenario)
                            ScenarioDeps.Add(dep);
                    } 
                    else
                    {
                        if (!parsedFiles.Contains(dep.Name))
                        {                            
                            Package pkg = pm.CachedPackages.First(x => x.FilesContained.Any(y => y == dep.Name));



                            parsedFiles.UnionWith(pkg.FilesContained);
                        }
                        
                    }
                }

                Title = info.Name;
            }

            Dependencies = Dependencies.OrderBy(x => x.State).ThenBy(x => x.Name).ToList();
            ScenarioDeps = ScenarioDeps.OrderBy(x => x.State).ThenBy(x => x.Name).ToList();
            Packages = Packages.OrderBy(x => x.State).ThenBy(x => x.Name).ToList();
            ScenarioPkgs = ScenarioPkgs.OrderBy(x => x.State).ThenBy(x => x.Name).ToList();

            DependenciesList.ItemsSource = Dependencies;
            ScenarioDepsList.ItemsSource = ScenarioDeps;
            DependenciesPackagesList.ItemsSource = Packages;
            ScenarioPackagesList.ItemsSource = ScenarioPkgs;

            /*CollectionView depsView = (CollectionView)CollectionViewSource.GetDefaultView(DependenciesList.ItemsSource);
            PropertyGroupDescription depsGroups = new PropertyGroupDescription("State");
            depsView.GroupDescriptions.Add(depsGroups);

            CollectionView scenView = (CollectionView)CollectionViewSource.GetDefaultView(ScenarioDepsList.ItemsSource);
            PropertyGroupDescription scenGroups = new PropertyGroupDescription("State");
            scenView.GroupDescriptions.Add(scenGroups);*/
        }

        private void DependenciesGroupsList_DoubleClick(object sender, MouseButtonEventArgs e)
        {

        }

        private void ScenarioDepsGropusList_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }
    }
}
