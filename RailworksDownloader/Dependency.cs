using System.Collections.Generic;
using System.Linq;

namespace RailworksDownloader
{
    public enum DependencyState
    {
        Available,
        Unavailable,
        Unknown,
        Downloaded,
        Paid
    }

    public class Dependency
    {
        public string Name { get; set; }

        public string PrettyState
        {
            get
            {
                switch (State)
                {
                    case DependencyState.Available:
                        return Localization.Strings.DepStateAvail;
                    case DependencyState.Unavailable:
                        return Localization.Strings.DepStateUnav;
                    case DependencyState.Paid:
                        return Localization.Strings.DepStatePaid;
                    case DependencyState.Downloaded:
                        return Localization.Strings.DepStateDown;
                    default:
                        return Localization.Strings.DepStateUnk;
                }
            }
        }

        public DependencyState State { get; set; }

        public bool IsScenario { get; set; }

        public bool IsRoute { get; set; }

        public int? PkgID { get; set; }

        public HashSet<string> Presence { get; set; }

        public Dependency(string name, int? pkgId) : this(name, false, pkgId) { }

        public Dependency(string name, bool scenario, int? pkgId) : this(name, DependencyState.Unknown, scenario, !scenario, pkgId) { }

        public Dependency(string name, DependencyState state, bool scenario, bool route, int? pkgId)
        {
            Name = name;
            State = state;
            IsScenario = scenario;
            IsRoute = route;
            PkgID = pkgId;
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

        public int Buyable => Items.Count(x => x.State == DependencyState.Paid && x.IsRoute);

        public int BuyableScenario => Items.Count(x => x.State == DependencyState.Paid && x.IsScenario);

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