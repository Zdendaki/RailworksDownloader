namespace RailworksDownloader
{
    public class DependencyPackage
    {
        public string Name { get; set; }
        
        public string PrettyState { 
            get { 
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

        public DependencyPackage(string name) : this(name, false) { }

        public DependencyPackage(string name, bool scenario) : this(name, DependencyState.Unknown, scenario, !scenario) { }

        public DependencyPackage(string name, DependencyState state, bool scenario, bool route)
        {
            Name = name;
            State = state;
            IsScenario = scenario;
            IsRoute = route;
        }
    }
}