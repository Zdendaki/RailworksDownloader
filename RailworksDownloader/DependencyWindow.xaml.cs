using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            HashSet<string> parsedRouteFiles = new HashSet<string>();
            HashSet<string> parsedScenarioFiles = new HashSet<string>();

            if (info != null)
            {
                IterateDependenices(info.ParsedDependencies.Items.Where(x => x.IsRoute), Dependencies, Packages, parsedRouteFiles, pm);
                IterateDependenices(info.ParsedDependencies.Items.Where(x => x.IsScenario), ScenarioDeps, ScenarioPkgs, parsedScenarioFiles, pm);

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

        private void IterateDependenices(IEnumerable<Dependency> items, List<Dependency> depList, List<DependencyPackage> pkgList, HashSet<string> parsedFiles, PackageManager pm)
        {
            Task.Run(async () =>
            {
                foreach (Dependency dep in items)
                {
                    if (dep.State == DependencyState.Unknown)
                    {
                        depList.Add(dep);
                    }
                    else if (!parsedFiles.Contains(dep.Name))
                    {
                        List<int> ids = await pm.FindFile(dep.Name);

                        if (ids.Count == 0)
                        {
                            depList.Add(dep);
                            continue;
                        }

                        foreach (Package pkg in pm.CachedPackages.Where(x => ids.Contains(x.PackageId)))
                        {
                            if (!pkgList.Any(x => x.Name == pkg.DisplayName))
                            {
                                pkgList.Add(new DependencyPackage(pkg.DisplayName, dep.State));
                            }

                            parsedFiles.UnionWith(pkg.FilesContained);
                        }
                        /*Package pkg = pm.CachedPackages.FirstOrDefault(x => x.FilesContained.Any(y => y == dep.Name));

                        if (pkg == default)
                            continue;*/

                    }
                }
            }).Wait();
        }
    }
}
