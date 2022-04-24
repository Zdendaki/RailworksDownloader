using Microsoft.Win32;
using ModernWpf.Controls;
using Sentry;
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
        public Uri ApiUrl = App.Debug ? new Uri("https://dls.rw.jachyhm.cz/api/") : new Uri("https://dls.rw.jachyhm.cz/api/");

        public static Color Blue { get; } = Color.FromArgb(255, 0, 151, 230); //new SolidColorBrush(Color.FromArgb(255, 0, 151, 230));
        public static Color Green { get; } = Color.FromArgb(255, 76, 209, 55); //new SolidColorBrush(Color.FromArgb(255, 76, 209, 55));
        public static Color Yellow { get; } = Color.FromArgb(255, 251, 197, 49); //new SolidColorBrush(Color.FromArgb(255, 251, 197, 49))
        public static Color Red { get; } = Color.FromArgb(255, 232, 65, 24); //new SolidColorBrush(Color.FromArgb(255, 232, 65, 24))
        public static Color Purple { get; } = Color.FromArgb(255, 190, 46, 221); //new SolidColorBrush(Color.FromArgb(255, 190, 46, 221))
        public static Color Gray { get; } = Color.FromArgb(255, 113, 128, 147); //new SolidColorBrush(Color.FromArgb(255, 113, 128, 147))

        private readonly EventWaitHandle dlcReportFinishedHandler = new EventWaitHandle(false, EventResetMode.ManualReset);
        private bool Saving = false;
        private bool CheckingDLC = false;
        public static bool ReportedDLC = false;
        internal Railworks RW;
        private PackageManager PM;
        private bool crawlingComplete = false;
        private bool loadingComplete = false;
        private bool exitConfirmed = false;
        private readonly object indexLock = new object();
        private string debugTitle = App.Debug ? " DEBUG!!!" : "";

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                DataContext = this;

                Title = $"Railworks DLS client v{App.Version}{debugTitle}";

                App.Window = this;

                try
                {
                    App.SteamManager = new SteamManager();
                }
                catch (Exception e)
                {
                    SentrySdk.CaptureException(e);
                    Debug.Assert(false, Localization.Strings.SteamInitFail);
                }

                Closing += MainWindowDialog_Closing;

                string savedRWPath = App.Settings.RailworksLocation;
                App.Railworks = new Railworks(string.IsNullOrWhiteSpace(savedRWPath) ? App.SteamManager?.RWPath : savedRWPath);
                App.Railworks.ProgressUpdated += RW_ProgressUpdated;
                App.Railworks.RouteSaving += RW_RouteSaving;
                App.Railworks.CrawlingComplete += RW_CrawlingComplete;
                RW = App.Railworks;

                try
                {
                    Updater updater = new Updater();
                    if (updater.CheckUpdates(ApiUrl) && !App.Debug)
                    {
                        Task.Run(async () =>
                        {
                            await updater.UpdateAsync();
                        });
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(RW.RWPath))
                        {
                            RailworksPathDialog rpd = new RailworksPathDialog();
                            App.DialogQueue.AddDialog(Environment.TickCount, 3, rpd);
                        }

                        if (string.IsNullOrWhiteSpace(App.Settings.RailworksLocation) && !string.IsNullOrWhiteSpace(RW.RWPath))
                        {
                            App.Settings.RailworksLocation = RW.RWPath;
                            App.Settings.Save();
                        }

                        Cleanup c = new Cleanup();
                        c.PerformCleanup();

                        PathChanged();

                        App.Settings.RailworksPathChanged += RailworksPathChanged;

                        RegistryKey dlsKey = Registry.CurrentUser.OpenSubKey("Software", true).OpenSubKey("Classes", true).CreateSubKey("dls");
                        dlsKey.SetValue("URL Protocol", "");
                        RegistryKey shellKey = dlsKey.CreateSubKey(@"shell\open\command");
                        shellKey.SetValue("", $"\"{System.Reflection.Assembly.GetEntryAssembly().Location}\" \"%1\"");

                        if (RW.RWPath != null && System.IO.Directory.Exists(RW.RWPath))
                        {
                            Task.Run(async () =>
                            {
                                await Utils.CheckLogin(ReportDLC, this, ApiUrl);
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    SentrySdk.CaptureException(e);
                    Trace.Assert(false, Localization.Strings.UpdaterPanic, e.ToString());
                }
            }
            catch (Exception e)
            {
                if (e.GetType() != typeof(ThreadInterruptedException) && e.GetType() != typeof(ThreadAbortException))
                {
                    SentrySdk.CaptureException(e);
                    Trace.Assert(false, e.ToString());
                }
            }
        }

        public void ReportDLC()
        {
            dlcReportFinishedHandler.Reset();

            if (string.IsNullOrWhiteSpace(App.Token) || App.SteamManager == null)
            {
                PM.CacheInit.WaitOne();
                PM.CachedPackages = PM.CachedPackages.Union(PM.InstalledPackages).ToList();
                dlcReportFinishedHandler.Set();
                return;
            }

            RW_CheckingDLC(false);
            Task.Run(async () =>
            {
                try
                {
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
                } 
                catch (Exception e)
                {
                    SentrySdk.CaptureException(e);
                    Trace.Assert(false, Localization.Strings.DLCReportError, e.Message);
                }
                finally
                {
                    PM.CacheInit.WaitOne();
                    PM.CachedPackages = PM.CachedPackages.Union(PM.InstalledPackages).ToList();
                }
                dlcReportFinishedHandler.Set();
                ReportedDLC = true;
                RW_CheckingDLC(true);
            });
        }

        private void MainWindowDialog_Closing(object sender, CancelEventArgs e)
        {
            if (exitConfirmed)
                return;

            if (Saving || CheckingDLC || App.IsDownloading)
            {
                e.Cancel = true;

                Utils.DisplayYesNo(Localization.Strings.Warning, Localization.Strings.OperationsRunning, Localization.Strings.Yes, Localization.Strings.No, (result) =>
                {
                    if (result)
                    {
                        exitConfirmed = true;
                        Close();
                    }
                });
            }
        }

        private void IterateDeps(ref List<Dependency> deps, HashSet<string> filesToIterate, bool isRoute = true)
        {
            for (int i = 0; i < filesToIterate.Count; i++)
            {
                string dep = filesToIterate.ElementAt(i);

                string[] parts = dep.Split(new string[] { "\\"}, 3, StringSplitOptions.None);
                if (parts.Length != 3)
                    continue;

                string provider = parts[0];
                string product = parts[1];
                string file = parts[2];

                if (dep != string.Empty)
                {
                    int? pkgId = null;

                    DependencyState state;
                    if (RW.AllInstalledDeps.Contains(dep))
                    {
                        state = DependencyState.Downloaded;
                        if (PM.DownloadableDeps.Contains(dep))
                        {
                            pkgId = PM.DownloadableDepsPackages[provider][product][file];
                        }
                        else if (PM.DownloadablePaidDeps.Contains(dep))
                        {
                            pkgId = PM.DownloadablePaidDepsPackages[provider][product][file];
                        }
                        //pkgId = PM.CachedPackages.FirstOrDefault(x => x.FilesContained.Contains(dep))?.PackageId;
                    }
                    else if (PM.DownloadableDeps.Contains(dep))
                    {
                        state = DependencyState.Available;
                        pkgId = PM.DownloadableDepsPackages[provider][product][file];
                    }
                    else if (PM.DownloadablePaidDeps.Contains(dep))
                    {
                        state = DependencyState.Paid;
                        pkgId = PM.DownloadablePaidDepsPackages[provider][product][file];
                    }
                    else
                        state = DependencyState.Unavailable;

                    deps.Add(new Dependency(dep, state, !isRoute, isRoute, pkgId));
                }
            }
        }

        internal void RW_CrawlingComplete()
        {
            crawlingComplete = true;
            PM.msmqWatcher?.Dispose();

            Dispatcher.Invoke(() =>
            {
                DownloadMissing.IsEnabled = false;
                ScanRailworks.IsEnabled = false;
                TotalProgress.Value = 100;
                TotalProgress.IsIndeterminate = true;
                TotalProgress.ToolTip = Localization.Strings.GettingRequired;
            });

            try
            {
                for (int i = 0; i < RW.Routes.Count; i++)
                {
                    RW.Routes[i].AllDependencies = RW.Routes[i].Dependencies.Union(RW.Routes[i].ScenarioDeps).ToArray();
                    RW.AllRequiredDeps.UnionWith(RW.Routes[i].AllDependencies);
                }

                //RW.Routes.Sort(delegate (RouteInfo x, RouteInfo y) { return x.AllDependencies.Length.CompareTo(y.AllDependencies.Length); });
                Dispatcher.Invoke(() =>
                {
                    TotalProgress.ToolTip = Localization.Strings.GettingInstalled;
                });

                RW.getAllInstalledDepsEvent.WaitOne();
                RW.AllMissingDeps = RW.AllRequiredDeps.Except(RW.AllInstalledDeps);
                Dispatcher.Invoke(() =>
                {
                    TotalProgress.ToolTip = Localization.Strings.GettingAvailable;
                });
                PM.CacheInit.WaitOne();
                PM.GetPackagesToDownload(RW.AllMissingDeps);
                Dispatcher.Invoke(() =>
                {
                    TotalProgress.ToolTip = Localization.Strings.DLCReportWait;
                });
                dlcReportFinishedHandler.WaitOne();

                Dispatcher.Invoke(() =>
                {
                    TotalProgress.ToolTip = Localization.Strings.GettingStates;
                });

                int maxThreads = Math.Min(Environment.ProcessorCount, RW.Routes.Count);
                int lastIndex = 0;
                Parallel.For(0, maxThreads, workerId =>
                {
                    while (lastIndex < RW.Routes.Count)
                    {
                        int i;
                        lock (indexLock)
                        {
                            i = lastIndex;
                            lastIndex++;
                            if (lastIndex > RW.Routes.Count)
                                break;
                        }
                        List<Dependency> deps = new List<Dependency>();

                        //int _i = ((i & 1) != 0) ? (i - 1) / 2 : (RW.Routes.Count - 1) - i / 2;
                        IterateDeps(ref deps, RW.Routes[i].Dependencies, true);
                        IterateDeps(ref deps, RW.Routes[i].ScenarioDeps, false);

                        RW.Routes[i].AllDependencies = null;
                        RW.Routes[i].ParsedDependencies = new DependenciesList(deps);
                        RW.Routes[i].Redraw();
                    }
                });

                loadingComplete = true;
                Dispatcher.Invoke(() =>
                {
                    TotalProgress.IsIndeterminate = false;
                    TotalProgress.ToolTip = null;

                    ScanRailworks.IsEnabled = true;
                    ScanRailworks.Content = Localization.Strings.MainRescan;
                });
            }
            catch (Exception e)
            {
                if (e.GetType() != typeof(ThreadInterruptedException) && e.GetType() != typeof(ThreadAbortException))
                {
                    SentrySdk.CaptureException(e);
                    Trace.Assert(false, e.ToString());
                }
            }

            new Task(async () =>
            {
                await PM.ResolveConflicts();
                PM.CheckUpdates();
                Dispatcher.Invoke(() =>
                {
                    //if (PM.PkgsToDownload.Count > 0)
                    DownloadMissing.IsEnabled = true;
                });
                PM.msmqWatcher = PM.RunQueueWatcher();
            }).Start();
        }

        private void UpdateSavingGrid()
        {
            if (!SavingGrid.Dispatcher.HasShutdownStarted)
            {
                SavingGrid.Dispatcher.Invoke(() =>
                {
                    SavingLabel.Content = Saving ? CheckingDLC ? App.IsDownloading ? $"{Localization.Strings.Saving} | {Localization.Strings.CheckingDLC} | {Localization.Strings.Downloading}" :
                        $"{Localization.Strings.Saving} | {Localization.Strings.CheckingDLC}" : Localization.Strings.Saving : App.IsDownloading ? $"{Localization.Strings.Downloading} | {Localization.Strings.CheckingDLC}" : Localization.Strings.CheckingDLC;
                    SavingGrid.Visibility = (Saving || CheckingDLC || App.IsDownloading) ? Visibility.Visible : Visibility.Hidden;
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

        private void RailworksPathChanged()
        {
            RW.RWPath = App.Settings.RailworksLocation;
            PathChanged();
        }

        private void PathChanged()
        {
            bool flag = RW.RWPath != null && System.IO.Directory.Exists(RW.RWPath);
            ScanRailworks.IsEnabled = flag;
            crawlingComplete = false;

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

                Title = $"Railworks DLS client v{App.Version}{debugTitle} - " + RW.RWPath;

                LoadRoutes();

                RoutesCount.Content = string.Format(Localization.Strings.RoutesCountLabel, RW.Routes.Count);
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
                SentrySdk.CaptureException(e);
                Trace.Assert(false, Localization.Strings.RoutesLoadFail, e.ToString());
            }
        }

        private void SelectRailworksLocation_Click(object sender, RoutedEventArgs e)
        {
            RailworksPathDialog rpd = new RailworksPathDialog();
            App.DialogQueue.AddDialog(Environment.TickCount, 3, rpd);
        }

        private void ScanRailworks_Click(object sender, RoutedEventArgs e)
        {
            if (RW == null)
                return;

            Dispatcher.Invoke(() =>
            {
                ScanRailworks.IsEnabled = false;
                SelectRailworksLocation.IsEnabled = false;
                TotalProgress.Value = 0;
            });
            loadingComplete = false;
            RW.getAllInstalledDepsEvent.Reset();
            new Task(() =>
            {
                RW.GetInstalledDeps();
            }).Start();
            if (!crawlingComplete)
            {
                crawlingComplete = false;
                RW.RunAllCrawlers();
            }
            else
            {
                Task.Run(() =>
                {
                    RW_CrawlingComplete();
                });
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
            if (PM == null)
                return; 

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
                    {
                        SentrySdk.CaptureException(e);
                        Trace.Assert(false, e.ToString());
                    }
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
