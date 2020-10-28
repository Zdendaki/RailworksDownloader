using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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

        public RouteCrawler Crawler { get; set; }

        internal RouteInfo(string name, string path)
        {
            Name = name;
            Path = path;
            Crawler = null;
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
    }
}
