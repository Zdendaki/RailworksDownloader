using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RailworksDownloader
{
    public class RouteItemData
    {
        public string Name { get; set; }

        public int Progress { get; set; }

        public RouteItemData(string routeName, int routeProgress)
        {
            Name = routeName;
            Progress = routeProgress;
        }
    }
}
