using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RailworksDownoader
{
    struct LoadedRoute
    {
        public string LoftChecksum { get; set; }

        public string RoadChecksum { get; set; }

        public string TrackChecksum { get; set; }

        public string SceneryChecksum { get; set; }

        public string RoutePropertiesChecksum { get; set; }

        public string APChecksum { get; set; }

        public List<string> Dependencies { get; set; }

        public LoadedRoute(string loftChecksum, string roadChecksum, string trackChecksum, string sceneryChecksum, string routePropertiesChecksum, string apChecksum, List<string> dependencies)
        {
            LoftChecksum = loftChecksum;
            RoadChecksum = roadChecksum;
            TrackChecksum = trackChecksum;
            SceneryChecksum = sceneryChecksum;
            RoutePropertiesChecksum = routePropertiesChecksum;
            APChecksum = apChecksum;
            Dependencies = dependencies;
        }
    }
}
