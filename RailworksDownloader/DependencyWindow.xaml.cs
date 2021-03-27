using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        private readonly RouteInfo RouteInfo;

        public DependencyWindow(RouteInfo info, PackageManager pm)
        {
            InitializeComponent();

            Dependencies = new List<Dependency>();
            ScenarioDeps = new List<Dependency>();
            Packages = new List<DependencyPackage>();
            ScenarioPkgs = new List<DependencyPackage>();
            HashSet<string> parsedRouteFiles = new HashSet<string>();
            HashSet<string> parsedScenarioFiles = new HashSet<string>();
            RouteInfo = info;

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

            if (Packages.Count == 0)
            {
                DPLRD.MinHeight = 0;
                DPLRD.Height = new GridLength(0);
                DependenciesPackagesList.Visibility = Visibility.Hidden;
                DependencySplitter.Visibility = Visibility.Hidden;
            }
            else if (Dependencies.Count == 0)
            {
                DLRD.MinHeight = 0;
                DLRD.Height = new GridLength(0);
                UnknownDependenciesGrid.Visibility = Visibility.Hidden;
            }

            if (ScenarioPkgs.Count == 0)
            {
                SPLRD.MinHeight = 0;
                SPLRD.Height = new GridLength(0);
                ScenarioPackagesList.Visibility = Visibility.Hidden;
                ScenarioSplitter.Visibility = Visibility.Hidden;
            }
            else if (ScenarioDeps.Count == 0)
            {
                SLRD.MinHeight = 0;
                SLRD.Height = new GridLength(0);
                UnknownScenarioDepsGrid.Visibility = Visibility.Hidden;
            }
        }

        private void IterateDependenices(IEnumerable<Dependency> items, List<Dependency> depList, List<DependencyPackage> pkgList, HashSet<string> parsedFiles, PackageManager pm)
        {
            foreach (Dependency dep in items)
            {
                if (dep.State == DependencyState.Unknown)
                {
                    depList.Add(dep);
                }
                else if (!parsedFiles.Contains(dep.Name))
                {
                    if (dep.PkgID == null)
                    {
                        depList.Add(dep);
                        continue;
                    }
                    else
                    {
                        Package pkg = pm.CachedPackages.FirstOrDefault(x => x.PackageId == dep.PkgID);
                        Trace.Assert(pkg != null, Localization.Strings.NonCached);
                        pkgList.Add(new DependencyPackage(pkg.DisplayName, dep.State, pkg.PackageId));
                        parsedFiles.UnionWith(pkg.FilesContained);
                    }

                    /*foreach (Package pkg in pm.CachedPackages.Where(x => ids.Contains(x.PackageId)))
                    {
                        if (!pkgList.Any(x => x.Name == pkg.DisplayName))
                        {
                            pkgList.Add(new DependencyPackage(pkg.DisplayName, dep.State));
                        }

                    }*/
                    /*Package pkg = pm.CachedPackages.FirstOrDefault(x => x.FilesContained.Any(y => y == dep.Name));

                    if (pkg == default)
                        continue;*/

                }
            }
        }

        private void ListViewItem_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListViewItem item = (ListViewItem)sender;

            if (item != default)
            {
                DependencyPackage pkg = (DependencyPackage)item.DataContext;
                DependencyDetailsWindow ddw = new DependencyDetailsWindow(pkg, RouteInfo)
                {
                    Owner = this
                };
                ddw.ShowDialog();
            }
        }
    }
}
