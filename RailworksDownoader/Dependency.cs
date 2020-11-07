namespace RailworksDownloader
{
    internal class Dependency
    {
        public string Name { get; set; }

        public DependencyState State { get; set; }

        public Dependency(string name, DependencyState state)
        {
            Name = name;
            State = state;
        }
    }
}
