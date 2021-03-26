using System;
using System.Collections.Generic;

namespace RailworksDownloader
{
    internal struct LoadedRoute
    {
        public string LoftChecksum;
        public DateTime LoftLastWrite;

        public string RoadChecksum;
        public DateTime RoadLastWrite;

        public string TrackChecksum;
        public DateTime TrackLastWrite;

        public string SceneryChecksum;
        public DateTime SceneryLastWrite;

        public string RoutePropertiesChecksum;
        public DateTime RoutePropertiesLastWrite;

        public string ScenariosChecksum;
        public DateTime ScenariosLastWrite;

        public string APChecksum;
        public DateTime APLastWrite;

        public HashSet<string> Dependencies { get; set; }

        public HashSet<string> ScenarioDeps { get; set; }

        public LoadedRoute(string loftChecksum, string roadChecksum, string trackChecksum, string sceneryChecksum, string routePropertiesChecksum,
            string apChecksum, string scenariosChecksum, HashSet<string> dependencies, HashSet<string> scenarioDeps,DateTime loftLastWrite = default, 
            DateTime roadLastWrite = default, DateTime trackLastWrite = default, DateTime sceneryLastWrite = default, DateTime routePropertiesLastWrite = default,
            DateTime scenariosLastWrite = default, DateTime apLastWrite = default)
        {
            LoftChecksum = loftChecksum;
            LoftLastWrite = loftLastWrite;
            RoadChecksum = roadChecksum;
            RoadLastWrite = roadLastWrite;
            TrackChecksum = trackChecksum;
            TrackLastWrite = trackLastWrite;
            SceneryChecksum = sceneryChecksum;
            SceneryLastWrite = sceneryLastWrite;
            RoutePropertiesChecksum = routePropertiesChecksum;
            RoutePropertiesLastWrite = routePropertiesLastWrite;
            ScenariosChecksum = scenariosChecksum;
            ScenariosLastWrite = scenariosLastWrite;
            APChecksum = apChecksum;
            APLastWrite = apLastWrite;
            Dependencies = dependencies;
            ScenarioDeps = scenarioDeps;
        }
    }
}
