using Microsoft.Win32;
using ModernWpf.Controls;
using RailworksDownloader.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
#if DEBUG
        public Uri ApiUrl = new Uri("https://dls.rw.jachyhm.cz/tests/api/");
#else
        public Uri ApiUrl = new Uri("https://dls.rw.jachyhm.cz/api/");
#endif

        internal static Brush Blue = new SolidColorBrush(Color.FromArgb(255, 0, 151, 230));
        internal static Brush Green = new SolidColorBrush(Color.FromArgb(255, 76, 209, 55));
        internal static Brush Yellow = new SolidColorBrush(Color.FromArgb(255, 251, 197, 49));
        internal static Brush Red = new SolidColorBrush(Color.FromArgb(255, 232, 65, 24));
        internal static Brush Purple = new SolidColorBrush(Color.FromArgb(255, 190, 46, 221));

        internal static DownloadDialog DownloadDialog = new DownloadDialog();
        internal static ContentDialog ContentDialog = new ContentDialog();
        internal static ContentDialog ErrorDialog = new ContentDialog();

        private readonly EventWaitHandle dlcReportFinishedHandler = new EventWaitHandle(false, EventResetMode.ManualReset);
        private bool Saving = false;
        private bool CheckingDLC = false;
        public bool ReportedDLC = false;
        internal Railworks RW;
        private PackageManager PM;
        private bool crawlingComplete = false;
        private bool loadingComplete = false;
        private bool exitConfirmed = false;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                Title = $"Railworks DLS client v{App.Version}";

                App.Window = this;

                try
                {
                    App.SteamManager = new SteamManager();
                }
                catch
                {
                    Debug.Assert(false, Localization.Strings.SteamInitFail);
                }

                Closing += MainWindowDialog_Closing;

                string savedRWPath = Settings.Default.RailworksLocation;
                App.Railworks = new Railworks(string.IsNullOrWhiteSpace(savedRWPath) ? App.SteamManager.RWPath : savedRWPath);
                App.Railworks.ProgressUpdated += RW_ProgressUpdated;
                App.Railworks.RouteSaving += RW_RouteSaving;
                App.Railworks.CrawlingComplete += RW_CrawlingComplete;
                RW = App.Railworks;

                try
                {
                    Updater updater = new Updater();
#if !DEBUG
                    if (updater.CheckUpdates(ApiUrl))
                    {
                        Task.Run(async () =>
                        {
                            await updater.UpdateAsync();
                        });
                    }
                    else
                    {
#endif
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

                        DownloadDialog.Owner = this;

                        RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", true).OpenSubKey("Classes", true).CreateSubKey("dls");
                        key.SetValue("URL Protocol", "");
                        //key.SetValue("DefaultIcon", "");
                        key.CreateSubKey(@"shell\open\command").SetValue("", $"\"{System.Reflection.Assembly.GetEntryAssembly().Location}\" \"%1\"");

                        if (RW.RWPath != null && System.IO.Directory.Exists(RW.RWPath))
                        {
                            Task.Run(async () =>
                            {
                                await Utils.CheckLogin(ReportDLC, this, ApiUrl);
                            });
                        }
#if !DEBUG
                    }
#endif
                }
                catch (Exception e)
                {
                    Trace.Assert(false, Localization.Strings.UpdaterPanic, e.ToString());
                }
            }
            catch (Exception e)
            {
                if (e.GetType() != typeof(ThreadInterruptedException) && e.GetType() != typeof(ThreadAbortException))
                    Trace.Assert(false, e.ToString());
            }
        }

        public void ReportDLC()
        {
            Task.Run(async () =>
            {
                RW_CheckingDLC(false);
                List<SteamManager.DLC> dlcList = App.SteamManager.GetInstalledDLCFiles();
                IEnumerable<Package> pkgs = await WebWrapper.ReportDLC(dlcList, App.Token, ApiUrl);
                PM.InstalledPackages = PM.InstalledPackages.Union(pkgs).ToList();
                new Task(() =>
                {
                    foreach (Package pkg in pkgs)
                    {
                        PM.SqLiteAdapter.SavePackage(pkg);
                    }
                    PM.SqLiteAdapter.FlushToFile(true);
                }).Start();
                PM.CacheInit.WaitOne();
                PM.CachedPackages = PM.CachedPackages.Union(PM.InstalledPackages).ToList();
                dlcReportFinishedHandler.Set();
                ReportedDLC = true;
                RW_CheckingDLC(true);
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
                    Title = Localization.Strings.Warning,
                    Content = Localization.Strings.OperationsRunning,
                    PrimaryButtonText = Localization.Strings.Yes,
                    SecondaryButtonText = Localization.Strings.No
                };

                Task<ContentDialogResult> result = dialog.ShowAsync();

                exitConfirmed = (await result) == ContentDialogResult.Primary;
                if (exitConfirmed)
                {
                    Close();
                }
            }
        }

        internal void RW_CrawlingComplete()
        {
            crawlingComplete = true;
            PM.StopMSMQ = true;

            Dispatcher.Invoke(() =>
            {
                DownloadMissing.IsEnabled = false;
                ScanRailworks.IsEnabled = false;
                TotalProgress.Value = 100;
                TotalProgress.IsIndeterminate = true;
            });

            try
            {
                for (int i = 0; i < RW.Routes.Count; i++)
                {
                    RW.Routes[i].AllDependencies = RW.Routes[i].Dependencies.Union(RW.Routes[i].ScenarioDeps).ToArray();
                    RW.AllRequiredDeps.UnionWith(RW.Routes[i].AllDependencies);
                }

                RW.Routes.Sort(delegate (RouteInfo x, RouteInfo y) { return x.AllDependencies.Length.CompareTo(y.AllDependencies.Length); }); // BUG: NullReferenceException

                RW.getAllInstalledDepsEvent.WaitOne();
                dlcReportFinishedHandler.WaitOne();

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
                                int? pkgId = null;

                                DependencyState state = DependencyState.Unknown;
                                if (RW.AllInstalledDeps.Contains(dep))
                                {
                                    state = DependencyState.Downloaded;
                                    if (PM.DownloadableDeps.Contains(dep))
                                    {
                                        pkgId = PM.DownloadableDepsPackages[dep];
                                    }
                                    else if (PM.DownloadablePaidDeps.Contains(dep))
                                    {
                                        pkgId = PM.DownloadablePaidDepsPackages[dep];
                                    }
                                    //pkgId = PM.CachedPackages.FirstOrDefault(x => x.FilesContained.Contains(dep))?.PackageId;
                                }
                                else if (PM.DownloadableDeps.Contains(dep))
                                {
                                    state = DependencyState.Available;
                                    pkgId = PM.DownloadableDepsPackages[dep];
                                }
                                else if (PM.DownloadablePaidDeps.Contains(dep))
                                {
                                    state = DependencyState.Paid;
                                    pkgId = PM.DownloadablePaidDepsPackages[dep];
                                }
                                else
                                    state = DependencyState.Unavailable;

                                deps.Add(new Dependency(dep, state, isScenario, isRoute, pkgId));
                            }
                        }

                        RW.Routes[_i].AllDependencies = null;
                        RW.Routes[_i].ParsedDependencies = new DependenciesList(deps);
                        RW.Routes[_i].Redraw();
                    }
                });

                loadingComplete = true;
                Dispatcher.Invoke(() =>
                {
                    TotalProgress.IsIndeterminate = false;

                    if (PM.DownloadableDepsPackages.Count + PM.PkgsToDownload.Count > 0) //TODO: get actuall downloadable missing
                        DownloadMissing.IsEnabled = true;

                    PM.StopMSMQ = false;
                    ScanRailworks.IsEnabled = true;
                    ScanRailworks.Content = Localization.Strings.MainRescan;
                });
            }
            catch (Exception e)
            {
                if (e.GetType() != typeof(ThreadInterruptedException) && e.GetType() != typeof(ThreadAbortException))
                    Trace.Assert(false, e.ToString());
            }

            new Task(async () =>
            {
                await PM.ResolveConflicts(this);
                PM.CheckUpdates();
                PM.RunQueueWatcher();
            }).Start();
        }

        private void UpdateSavingGrid()
        {
            if (!SavingGrid.Dispatcher.HasShutdownStarted)
            {
                SavingGrid.Dispatcher.Invoke(() =>
                {
                    SavingLabel.Content = Saving ? CheckingDLC ? $"{Localization.Strings.Saving} | {Localization.Strings.CheckingDLC}" : Localization.Strings.Saving : Localization.Strings.CheckingDLC;
                    SavingGrid.Visibility = (Saving || CheckingDLC) ? Visibility.Visible : Visibility.Hidden;
                });
            }
        }

        private void RW_CheckingDLC(bool @checked)
        {
            CheckingDLC = !@checked;
            UpdateSavingGrid();
        }

        private void RW_RouteSaving(bool saved)
        {
            Saving = !saved;
            UpdateSavingGrid();
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
            bool flag = RW.RWPath != null && System.IO.Directory.Exists(RW.RWPath);
            ScanRailworks.IsEnabled = flag;

            if (flag)
            {
                TotalProgress.Dispatcher.Invoke(() => TotalProgress.Value = 0);

                App.Railworks = new Railworks(RW.RWPath);
                App.Railworks.ProgressUpdated += RW_ProgressUpdated;
                App.Railworks.RouteSaving += RW_RouteSaving;
                App.Railworks.CrawlingComplete += RW_CrawlingComplete;

                RW = App.Railworks;

                App.PackageManager = new PackageManager(ApiUrl, this, RW.RWPath);
                PM = App.PackageManager;

                Title = $"Railworks DLS client v{App.Version} - " + RW.RWPath;

                LoadRoutes();
            }
        }

        private void LoadRoutes()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(RW.RWPath))
                    return;

                RW.InitRoutes();

                RoutesList.ItemsSource = RW.Routes.OrderBy(x => x.Name);
            }
            catch (Exception e)
            {
                Trace.Assert(false, Localization.Strings.RoutesLoadFail, e.ToString());
            }
        }

        private void SelectRailworksLocation_Click(object sender, RoutedEventArgs e)
        {
            RailworksPathDialog rpd = new RailworksPathDialog();
            rpd.ShowAsync();
        }

        private void ScanRailworks_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ScanRailworks.IsEnabled = false;
                SelectRailworksLocation.IsEnabled = false;
                TotalProgress.Value = 0;
            });
            loadingComplete = false;
            RW.getAllInstalledDepsEvent.Reset();
            if (!crawlingComplete)
            {
                crawlingComplete = false;
                new Task(() =>
                {
                    RW.GetInstalledDeps();
                }).Start();
                RW.RunAllCrawlers();
            }
            else
            {
                new Task(() =>
                {
                    RW.GetInstalledDeps();
                }).Start();
                new Task(() =>
                {
                    RW_CrawlingComplete();
                }).Start();
            }
        }

        private void ListViewItem_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SWC.ListViewItem item = (SWC.ListViewItem)sender;

            if (item?.IsSelected == true && crawlingComplete && loadingComplete)
            {
                DependencyWindow dw = new DependencyWindow((RouteInfo)item.Content, PM);
                dw.ShowDialog();
            }
        }

        private void ManagePackages_Click(object sender, RoutedEventArgs e)
        {
            PackageManagerWindow pmw = new PackageManagerWindow(PM);
            pmw.ShowDialog();
        }

        private void Window_Loaded(object sender, RoutedEventArgs _)
        {
            Task.Run(() =>
            {
                try
                {
                    if (System.IO.Directory.Exists(RW.RWPath) && App.AutoDownload)
                        ScanRailworks_Click(this, null);
                }
                catch (Exception e)
                {
                    if (e.GetType() != typeof(ThreadInterruptedException) && e.GetType() != typeof(ThreadAbortException))
                        Trace.Assert(false, e.ToString());
                }
            });
        }

        private void DownloadMissing_Click(object sender, RoutedEventArgs e)
        {
            ScanRailworks.IsEnabled = false;
            SelectRailworksLocation.IsEnabled = false;
            DownloadMissing.IsEnabled = false;
            PM.DownloadDependencies();
        }
    }
}
