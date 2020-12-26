using System.Collections.Generic;

namespace RailworksDownloader
{
    internal struct LoadedRoute
    {
        public string LoftChecksum { get; set; }

        public string RoadChecksum { get; set; }

        public string TrackChecksum { get; set; }

        public string SceneryChecksum { get; set; }

        public string RoutePropertiesChecksum { get; set; }

        public string ScenariosChecksum { get; set; }

        public string APChecksum { get; set; }

        public HashSet<string> Dependencies { get; set; }

        public HashSet<string> ScenarioDeps { get; set; }

        public LoadedRoute(string loftChecksum, string roadChecksum, string trackChecksum, string sceneryChecksum, string routePropertiesChecksum, string apChecksum, string scenariosChecksum, HashSet<string> dependencies, HashSet<string> scenarioDeps)
        {
            LoftChecksum = loftChecksum;
            RoadChecksum = roadChecksum;
            TrackChecksum = trackChecksum;
            SceneryChecksum = sceneryChecksum;
            RoutePropertiesChecksum = routePropertiesChecksum;
            ScenariosChecksum = scenariosChecksum;
            APChecksum = apChecksum;
            Dependencies = dependencies;
            ScenarioDeps = scenarioDeps;
        }
    }
}
