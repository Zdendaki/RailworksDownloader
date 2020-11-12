using System.Collections.Generic;
using System.Linq;

namespace RailworksDownloader
{
    public enum DependencyState
    {
        Unknown,
        Unavailable,
        Paid,
        Available,
        Downloaded
    }

    public class Dependency
    {
        public string Name { get; set; }

        public DependencyState State { get; set; }

        public bool IsScenario { get; set; }

        public bool IsRoute { get; set; }

        public HashSet<string> Presence { get; set; }

        public Dependency(string name) : this(name, false) { }

        public Dependency(string name, bool scenario) : this(name, DependencyState.Unknown, scenario, !scenario) { }

        public Dependency(string name, DependencyState state, bool scenario, bool route)
        {
            Name = name;
            State = state;
            IsScenario = scenario;
            IsRoute = route;
        }
    }

    public class DependenciesList
    {
        public List<Dependency> Items { get; set; } = new List<Dependency>();

        public int RouteCount => Items.Count(x => x.IsRoute);

        public int ScenariosCount => Items.Count(x => x.IsScenario);

        public int Missing => Items.Count(x => x.State != DependencyState.Downloaded && x.IsRoute);

        public int MissingScenario => Items.Count(x => x.State != DependencyState.Downloaded && x.IsScenario);

        public int Downloadable => Items.Count(x => x.State == DependencyState.Available && x.IsRoute);

        public int DownloadableScenario => Items.Count(x => x.State == DependencyState.Available && x.IsScenario);

        public bool Unknown => Items.Any(x => x.State == DependencyState.Unknown);

        public DependenciesList(IEnumerable<Dependency> dependencies)
        {
            Items = dependencies.ToList();
        }

        public DependenciesList()
        {
            Items = new List<Dependency>();
        }
    }
}