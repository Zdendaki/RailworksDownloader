using System.Collections.Generic;
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

        public DependenciesList ParsedDependencies { get; set; } = new DependenciesList();
        public readonly HashSet<string> Dependencies = new HashSet<string>();
        public readonly HashSet<string> ScenarioDeps = new HashSet<string>();
        public string[] AllDependencies { get; set; }

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
            Crawler = null;
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
            if (ParsedDependencies.Unknown || ParsedDependencies.Items.Count == 0)
                return MainWindow.Blue;
            else if (ParsedDependencies.Missing > 0 && ParsedDependencies.Downloadable < ParsedDependencies.Missing)
                return MainWindow.Red;
            else if (ParsedDependencies.ScenariosCount > 0 && ParsedDependencies.DownloadableScenario < ParsedDependencies.MissingScenario)
                return MainWindow.Purple;
            else if (ParsedDependencies.Downloadable + ParsedDependencies.DownloadableScenario > 0)
                return MainWindow.Yellow;
            else
                return MainWindow.Green;
        }
    }
}
