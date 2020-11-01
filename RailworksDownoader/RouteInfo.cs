using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Windows.UI;

namespace RailworksDownloader
{
    public class RouteInfo : INotifyPropertyChanged
    {
        public string Name { get; set; }

        public string Path { get; set; }

        float progress = 0;
        
        public float Progress 
        { 
            get
            {
                return progress;
            }
            set
            {
                if (progress != value)
                    OnPropertyChanged<float>();

                progress = value;
            }
        }

        

        public Brush ProgressBackground
        {
            get
            {
                return FromInput(MissingCount, MissingScenariosCount, DownloadableCount, DownloadableScenarioCount);
            }
        }

        int count;
        int downloadableCount;
        int scenarioCount;
        int downloadableScenarioCount;

        public int DownloadableCount
        {
            get
            {
                return downloadableCount;
            }
            set
            {
                if (downloadableCount != value)
                {
                    OnPropertyChanged<Brush>("ProgressBackground");
                    OnPropertyChanged<int>();
                }

                downloadableCount = value;
            }
        }

        public int MissingCount
        {
            get
            {
                return count;
            }
            set
            {
                if (count != value)
                {
                    OnPropertyChanged<Brush>("ProgressBackground");
                    OnPropertyChanged<int>();
                }

                count = value;
            }
        }

        public int DownloadableScenarioCount
        {
            get
            {
                return downloadableScenarioCount;
            }
            set
            {
                if (downloadableScenarioCount != value)
                {
                    OnPropertyChanged<Brush>("ProgressBackground");
                    OnPropertyChanged<int>();
                }

                downloadableScenarioCount = value;
            }
        }

        public int MissingScenariosCount
        {
            get
            {
                return scenarioCount;
            }
            set
            {
                if (scenarioCount != value)
                {
                    OnPropertyChanged<Brush>("ProgressBackground");
                    OnPropertyChanged<int>();
                }

                scenarioCount = value;
            }
        }

        public RouteCrawler Crawler { get; set; }

        internal RouteInfo(string name, string path)
        {
            Name = name;
            Path = path;
            Crawler = null;
            count = -1;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged<T>([CallerMemberName]string caller = null)
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
