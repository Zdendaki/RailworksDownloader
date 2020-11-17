using ModernWpf.Controls;
using RailworksDownloader.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SWC = System.Windows.Controls;

namespace RailworksDownloader
{
    /// <summary>
    /// Interakční logika pro MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Uri ApiUrl = new Uri("https://dls.rw.jachyhm.cz/api/");

        internal static Brush Blue = new SolidColorBrush(Color.FromArgb(255, 0, 151, 230));
        internal static Brush Green = new SolidColorBrush(Color.FromArgb(255, 76, 209, 55));
        internal static Brush Yellow = new SolidColorBrush(Color.FromArgb(255, 251, 197, 49));
        internal static Brush Red = new SolidColorBrush(Color.FromArgb(255, 232, 65, 24));
        internal static Brush Purple = new SolidColorBrush(Color.FromArgb(255, 190, 46, 221));

        private bool Saving = false;
        private bool CheckingDLC = false;
        private Railworks RW;
        private PackageManager PM;
        private bool crawlingComplete = false;
        private bool exitConfirmed = false;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                App.Window = this;

                App.SteamManager = new SteamManager();

                Closing += MainWindowDialog_Closing;

                string savedRWPath = Settings.Default.RailworksLocation;
                App.Railworks = new Railworks(string.IsNullOrWhiteSpace(App.SteamManager.RWPath) ? savedRWPath : App.SteamManager.RWPath);
                App.Railworks.ProgressUpdated += RW_ProgressUpdated;
                App.Railworks.RouteSaving += RW_RouteSaving;
                App.Railworks.CrawlingComplete += RW_CrawlingComplete;

                RW = App.Railworks;

                if (string.IsNullOrWhiteSpace(RW.RWPath))
                {
                    RailworksPathDialog rpd = new RailworksPathDialog();
                    rpd.ShowAsync();
                }

                if (string.IsNullOrWhiteSpace(Settings.Default.RailworksLocation) && !string.IsNullOrWhiteSpace(RW.RWPath))
                {
                    Settings.Default.RailworksLocation = RW.RWPath;
                    Settings.Default.Save();
                }

                PathChanged();

                Settings.Default.PropertyChanged += PropertyChanged;
            }
            catch (Exception e)
            {
                Desharp.Debug.Log(e, Desharp.Level.DEBUG);
            }

            Task.Run(async () =>
            {
                try
                {
                    RW_CheckingDLC(false);
                    List<SteamManager.DLC> dlcList = App.SteamManager.GetInstalledDLCFiles();
                    await WebWrapper.ReportDLC(dlcList, ApiUrl);
                    RW_CheckingDLC(true);
                }
                catch (Exception e)
                {
                    Desharp.Debug.Log(e, Desharp.Level.DEBUG);
                }
            });
        }

        private async void MainWindowDialog_Closing(object sender, CancelEventArgs e)
        {
            if (exitConfirmed == true)
                return;
            
            if (Saving || CheckingDLC)
            {
                e.Cancel = true;

                ContentDialog dialog = new ContentDialog
                {
                    Title = "Warning",
                    Content = "Some operations are still running.\nDo you really want to close the app?",
                    PrimaryButtonText = "Yes",
                    SecondaryButtonText = "No"
                };

                var result = dialog.ShowAsync();

                exitConfirmed = (await result) == ContentDialogResult.Primary;
                if (exitConfirmed)
                {
                    Close();
                }
            }
        }

        private async void RW_CrawlingComplete()
        {
            TotalProgress.Dispatcher.Invoke(() => TotalProgress.Value = 100);
            TotalProgress.Dispatcher.Invoke(() => TotalProgress.IsIndeterminate = true);

            HashSet<string> globalDeps = new HashSet<string>();

            for (int i = 0; i < RW.Routes.Count; i++)
            {
                RW.Routes[i].AllDependencies = RW.Routes[i].Dependencies.Union(RW.Routes[i].ScenarioDeps).ToArray();
                globalDeps.UnionWith(RW.Routes[i].AllDependencies);
            }

            HashSet<string> existing = await RW.GetMissing(globalDeps);

            globalDeps.ExceptWith(existing);
            HashSet<string> downloadable = await PM.GetDownloadableDependencies(globalDeps);
            HashSet<string> paid = await PM.GetPaidDependencies(globalDeps);

            RW.Routes.Sort(delegate (RouteInfo x, RouteInfo y) { return x.AllDependencies.Length.CompareTo(y.AllDependencies.Length); });

            int maxThreads = Math.Min(Environment.ProcessorCount, RW.Routes.Count);
            Parallel.For(0, maxThreads, workerId =>
            {
                int max = RW.Routes.Count * (workerId + 1) / maxThreads;
                for (int i = RW.Routes.Count * workerId / maxThreads; i < max; i++)
                {
                    List<Dependency> deps = new List<Dependency>();

                    int _i = ((i & 1) != 0) ? (i - 1) / 2 : (RW.Routes.Count - 1) - i / 2;
                    for (int j = 0; j < RW.Routes[_i].AllDependencies.Length; j++)
                    {
                        string dep = RW.Routes[_i].AllDependencies[j];

                        if (dep != string.Empty)
                        {
                            bool isRoute = RW.Routes[_i].Dependencies.Contains(dep);
                            bool isScenario = RW.Routes[_i].ScenarioDeps.Contains(dep);

                            DependencyState state = DependencyState.Unknown;
                            if (existing.Contains(dep))
                                state = DependencyState.Downloaded;
                            else if (downloadable.Contains(dep))
                                state = DependencyState.Available;
                            else if (paid.Contains(dep))
                                state = DependencyState.Paid;
                            else
                                state = DependencyState.Unavailable;

                            deps.Add(new Dependency(dep, state, isScenario, isRoute));
                        }
                    }
                    RW.Routes[_i].Dependencies.Clear();
                    RW.Routes[_i].ScenarioDeps.Clear();
                    RW.Routes[_i].AllDependencies = null;
                    RW.Routes[_i].ParsedDependencies = new DependenciesList(deps);
                    RW.Routes[_i].Redraw();
                }
            });

            TotalProgress.Dispatcher.Invoke(() => {
                TotalProgress.IsIndeterminate = false;
                DownloadMissing.IsEnabled = true;
            });
            crawlingComplete = true;
        }

        private void ToggleSavingGrid(string type)
        {
            if (!SavingGrid.Dispatcher.HasShutdownStarted)
            {
                SavingGrid.Dispatcher.Invoke(() =>
                {
                    if (SavingGrid.Visibility == Visibility.Hidden)
                        SavingLabel.Content = type;
                    SavingGrid.Visibility = (Saving || CheckingDLC) ? Visibility.Visible : Visibility.Hidden;
                });
            }
        }

        private void RW_CheckingDLC(bool @checked)
        {
            CheckingDLC = !@checked;
            ToggleSavingGrid("Checking installed DLCs");
        }

        private void RW_RouteSaving(bool saved)
        {
            Saving = !saved;
            ToggleSavingGrid("Saving");
        }

        private void RW_ProgressUpdated(int percent)
        {
            if (!TotalProgress.Dispatcher.HasShutdownStarted)
            {
                TotalProgress.Dispatcher.Invoke(() => { TotalProgress.Value = percent; });
            }
        }

        private void PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "RailworksLocation")
            {
                RW.RWPath = Settings.Default.RailworksLocation;
                PathChanged();
            }
        }

        private void PathChanged()
        {
            PathSelected.IsChecked = ScanRailworks.IsEnabled = !string.IsNullOrWhiteSpace(RW.RWPath);

            if (RW.RWPath != null && System.IO.Directory.Exists(RW.RWPath))
            {
                TotalProgress.Dispatcher.Invoke(() => TotalProgress.Value = 0);

                App.Railworks = new Railworks(RW.RWPath);
                App.Railworks.ProgressUpdated += RW_ProgressUpdated;
                App.Railworks.RouteSaving += RW_RouteSaving;
                App.Railworks.CrawlingComplete += RW_CrawlingComplete;

                RW = App.Railworks;

                App.PackageManager = new PackageManager(RW.RWPath, ApiUrl, this);
                PM = App.PackageManager;

                Title = "Railworks download station client - " + RW.RWPath;

                LoadRoutes();
            }
        }

        private void LoadRoutes()
        {
            if (string.IsNullOrWhiteSpace(RW.RWPath))
                return;

            RW.InitRoutes();

            RoutesList.ItemsSource = RW.Routes.OrderBy(x => x.Name);
        }

        private void SelectRailworksLocation_Click(object sender, RoutedEventArgs e)
        {
            RailworksPathDialog rpd = new RailworksPathDialog();
            rpd.ShowAsync();
        }

        private void ScanRailworks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ScanRailworks.Dispatcher.Invoke(() =>
                {
                    ScanRailworks.IsEnabled = false;
                    SelectRailworksLocation.IsEnabled = false;
                    TotalProgress.Value = 0;
                });
                crawlingComplete = false;
                RW.RunAllCrawlers();
            }
            catch (Exception ex)
            {
                Desharp.Debug.Log(ex, Desharp.Level.DEBUG);
            }
        }

        private void ListViewItem_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SWC.ListViewItem item = (SWC.ListViewItem)sender;

            if (item?.IsSelected == true && crawlingComplete)
            {
                DependencyWindow dw = new DependencyWindow((RouteInfo)item.Content);
                dw.ShowDialog();
            }
        }

        private void ManagePackages_Click(object sender, RoutedEventArgs e)
        {
            PackageManagerWindow pmw = new PackageManagerWindow(PM);
            pmw.ShowDialog();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                if (!string.IsNullOrWhiteSpace(RW.RWPath))
                    ScanRailworks_Click(this, null);
            });
        }

        private void DownloadMissing_Click(object sender, RoutedEventArgs e)
        {
            PM.DownloadDependencies();
        }
    }
}
