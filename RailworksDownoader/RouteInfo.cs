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

        public DependenciesList Dependencies { get; set; }

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

        public RouteCrawler Crawler { get; set; }

        internal RouteInfo(string name, string hash, string path)
        {
            Name = name;
            Hash = hash;
            Path = path;
            Dependencies = new DependenciesList();
            Dependencies.DependenciesChanged += Dependencies_DependenciesChanged;
            Crawler = null;
        }

        private void Dependencies_DependenciesChanged()
        {
            Redraw();
        }

        public void Redraw()
        {
            OnPropertyChanged<Brush>("ProgressBackground");
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
            else if (Dependencies.Missing > 0 && Dependencies.Downloadable < Dependencies.Missing)
                return MainWindow.Red;
            else if (Dependencies.ScenariosCount > 0 && Dependencies.DownloadableScenario < Dependencies.MissingScenario)
                return MainWindow.Purple;
            else if (Dependencies.Downloadable + Dependencies.DownloadableScenario > 0)
                return MainWindow.Yellow;
            else
                return MainWindow.Green;
        }
    }
}
