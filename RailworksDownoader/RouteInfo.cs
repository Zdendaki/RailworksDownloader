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
    class RouteInfo : INotifyPropertyChanged
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
                return FromInput(MissingCount);
            }
        }

        int count;

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

        public Brush FromInput(int input)
        {
            if (input > 0)
                return MainWindow.Red;
            else if (input == -1)
                return MainWindow.Blue;
            else
                return MainWindow.Green;
        }
    }
}
