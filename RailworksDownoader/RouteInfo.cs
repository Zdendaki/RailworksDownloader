using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace RailworksDownloader
{
    public class RouteInfo : INotifyPropertyChanged
    {
        public string Name { get; set; }

        public string Hash { get; set; }

        public string Path { get; set; }

        public DependencyList Dependencies { get; set; }

        private float progress = 0;

        public float Progress
        {
            get => progress;
            set
            {
                if (progress != value)
                    OnPropertyChanged<float>();

                progress = value;
            }
        }

        public Brush ProgressBackground => GetBrush();

        private readonly int count;
        private readonly int downloadableCount;
        private readonly int scenarioCount;
        private readonly int downloadableScenarioCount;

        public int DownloadableCount => Dependencies.Downloadable;

        public int MissingCount => Dependencies.Missing;

        public int DownloadableScenarioCount => Dependencies.DownloadableScenario;

        public int MissingScenariosCount => Dependencies.MissingScenario;

        public RouteCrawler Crawler { get; set; }

        internal RouteInfo(string name, string hash, string path)
        {
            Name = name;
            Hash = hash;
            Path = path;
            Dependencies = new DependencyList();
            Dependencies.DependenciesChanged += Dependencies_DependenciesChanged;
            Crawler = null;
            count = -1;
        }

        private void Dependencies_DependenciesChanged()
        {
            Redraw();
        }

        public void Redraw()
        {
            OnPropertyChanged<Brush>("ProgressBackground");
            OnPropertyChanged<int>("DownloadableCount");
            OnPropertyChanged<int>("MissingCount");
            OnPropertyChanged<int>("DownloadableScenarioCount");
            OnPropertyChanged<int>("MissingScenariosCount");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged<T>([CallerMemberName] string caller = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(caller));
        }

        public void ProgressUpdated(float progress)
        {
            Progress = progress;
        }

        public Brush GetBrush()
        {
            if (Dependencies.Unknown)
                return MainWindow.Blue;
            else if (Dependencies.Count > 0 && Dependencies.Downloadable < Dependencies.Count)
                return MainWindow.Red;
            else if (Dependencies.ScenariosCount > 0 && Dependencies.DownloadableScenario < Dependencies.ScenariosCount)
                return MainWindow.Purple;
            else if (Dependencies.Downloadable + Dependencies.DownloadableScenario > 0)
                return MainWindow.Yellow;
            else
                return MainWindow.Green;
        }
    }
}
