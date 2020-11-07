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



        public Brush ProgressBackground => FromInput(MissingCount, MissingScenariosCount, DownloadableCount, DownloadableScenarioCount);

        private int count;
        private int downloadableCount;
        private int scenarioCount;
        private int downloadableScenarioCount;

        public int DownloadableCount
        {
            get => downloadableCount;
            set
            {
                if (downloadableCount != value || value == 0)
                {
                    OnPropertyChanged<Brush>("ProgressBackground");
                    OnPropertyChanged<int>();
                }

                downloadableCount = value;
            }
        }

        public int MissingCount
        {
            get => count;
            set
            {
                if (count != value || value == 0)
                {
                    OnPropertyChanged<Brush>("ProgressBackground");
                    OnPropertyChanged<int>();
                }

                count = value;
            }
        }

        public int DownloadableScenarioCount
        {
            get => downloadableScenarioCount;
            set
            {
                if (downloadableScenarioCount != value || value == 0)
                {
                    OnPropertyChanged<Brush>("ProgressBackground");
                    OnPropertyChanged<int>();
                }

                downloadableScenarioCount = value;
            }
        }

        public int MissingScenariosCount
        {
            get => scenarioCount;
            set
            {
                if (scenarioCount != value || value == 0)
                {
                    OnPropertyChanged<Brush>("ProgressBackground");
                    OnPropertyChanged<int>();
                }

                scenarioCount = value;
            }
        }

        public RouteCrawler Crawler { get; set; }

        internal RouteInfo(string name, string hash, string path)
        {
            Name = name;
            Hash = hash;
            Path = path;
            Crawler = null;
            count = -1;
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

        public Brush FromInput(int depsCount, int scenarioDepsCount, int downloadableCount, int downloadableScenarioCount)
        {
            if (depsCount > 0 && downloadableCount < depsCount)
                return MainWindow.Red;
            else if (depsCount == -1)
                return MainWindow.Blue;
            else if (scenarioDepsCount > 0 && downloadableScenarioCount < scenarioDepsCount)
                return MainWindow.Purple;
            else if (downloadableCount > 0 || downloadableScenarioCount > 0)
                return MainWindow.Yellow;
            else
                return MainWindow.Green;
        }
    }
}
