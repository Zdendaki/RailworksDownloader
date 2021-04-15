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

        public Color[] ProgressBackground => GetBrush();

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
            OnPropertyChanged<Color[]>("ProgressBackground");
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

        public Color[] GetBrush()
        {
            Color rColor = MainWindow.Blue;
            Color sColor = MainWindow.Blue;

            if (ParsedDependencies.Missing > 0)
            {
                if (ParsedDependencies.Downloadable > 0)
                    rColor = MainWindow.Purple;
                else if (ParsedDependencies.Buyable > 0)
                    rColor = MainWindow.Yellow;
                else
                    rColor = MainWindow.Red;
            }
            else if (ParsedDependencies.RouteCount > 0)
            {
                rColor = MainWindow.Green;
            }

            if (ParsedDependencies.MissingScenario > 0)
            {
                if (ParsedDependencies.DownloadableScenario > 0)
                    sColor = MainWindow.Purple;
                else if (ParsedDependencies.BuyableScenario > 0)
                    sColor = MainWindow.Yellow;
                else
                    sColor = MainWindow.Red;
            }
            else if (ParsedDependencies.ScenariosCount > 0)
            {
                sColor = MainWindow.Green;
            }

            return new Color[] { rColor, sColor };
        }
    }
}
