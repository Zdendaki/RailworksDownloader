using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
            Packages = Packages.OrderByDescending(x => x.State).ThenBy(x => x.Name).ToList();
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
            items = items.OrderByDescending(x => x.State);
            //Package cachedPackage = null;
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

                        /*if (dep.PkgID != cachedPackage?.PackageId)
                            cachedPackage = pm.CachedPackages.FirstOrDefault(x => x.PackageId == dep.PkgID);
                        Trace.Assert(cachedPackage != null, Localization.Strings.NonCached);

                        if (!pkgList.Any(x => x.ID == dep.PkgID))
                            pkgList.Add(new DependencyPackage(cachedPackage.DisplayName, dep.State, cachedPackage.PackageId));
                        else
                        {
                            DependencyPackage pkg = pkgList.First(x => x.ID == dep.PkgID);
                            if (dep.State > pkg.State)
                                pkg.State = dep.State;
                        }

                        parsedFiles.UnionWith(items.TakeWhile(x => x.State == dep.State).Select(x => x.Name));*/
                    }
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

        private void ListView_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                if (!(sender is ListView))
                    return;

                ListView listView = (ListView)sender;
                if (listView == null || listView.SelectedItems.Count <= 0)
                    return;

                CopySelectedItemsToClipboard(listView, listView.SelectedItem is DependencyPackage);
            }
        }

        private void CopySelectedItemsToClipboard(ListView listView, bool isPackage = false)
        {
            List<BaseDependency> selectedItems = listView.SelectedItems.Cast<BaseDependency>().ToList();

            BaseDependency firstItem = selectedItems.First();
            DependencyState oldState = firstItem.State;

            StringBuilder builder = new StringBuilder();

            string type = isPackage ? Localization.Strings.ClipboardBuilderPackages : Localization.Strings.ClipboardBuilderItems;
            string placement = listView.ItemsSource == ScenarioDeps || listView.ItemsSource == ScenarioPkgs ? Localization.Strings.ClipboardBuilderScenarios : Localization.Strings.ClipboardBuilderRoutes;
            builder.AppendLine(string.Format(Localization.Strings.ClipboarBuilderMainString, selectedItems.Count, listView.Items.Count, type, placement, RouteInfo.Name));
            builder.AppendLine($"     -----{firstItem.PrettyState.ToUpper()}-----");

            foreach (BaseDependency dep in selectedItems)
            {
                if (dep.State != oldState)
                {
                    builder.AppendLine();
                    builder.AppendLine($"     -----{dep.PrettyState.ToUpper()}-----");
                    oldState = dep.State;
                }

                string extension = isPackage ? "" : ".xml";
                string pkgId = isPackage ? $" [https://dls.rw.jachyhm.cz/?package={dep.PkgID}]" : "";

                builder.AppendLine($"          {dep.PrettyState} - {dep.Name}{pkgId}{extension}");
            }

            Clipboard.SetText(builder.ToString());
        }
    }
}
