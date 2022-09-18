namespace RailworksDownloader
{
    public class DependencyPackage : BaseDependency
    {
        public DependencyPackage(string name) : this(name, DependencyState.Unknown) { }

        public DependencyPackage(string name, DependencyState state, int id)
        {
            Name = name;
            State = state;
            PkgID = id;
        }

        public DependencyPackage(string name, DependencyState state) : this(name, state, -1) { }
    }
}