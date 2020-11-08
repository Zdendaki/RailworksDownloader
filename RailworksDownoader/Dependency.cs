using System;
using System.Collections;
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

        public bool Scenario { get; set; }

        public HashSet<string> Presence { get; set; }

        public Dependency(string name) : this(name, false) { }

        public Dependency(string name, bool scenario) : this(name, DependencyState.Unknown, scenario) { }

        public Dependency(string name, DependencyState state, bool scenario)
        {
            Name = name;
            State = state;
            Scenario = scenario;
        }
    }

    public class DependencyEqualityComparer : IEqualityComparer<Dependency>
    {
        public bool Equals(Dependency x, Dependency y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(Dependency obj)
        {
            return obj.Name.GetHashCode();
        }
    }

    public class DependenciesList : IList<Dependency>, IEnumerable<Dependency>, IEnumerable
    {
        private readonly List<Dependency> Items;

        public Dependency this[int index]
        {
            get => Items[index];
            set => Items[index] = value;
        }

        public int Count => Items.Count;

        public int RouteCount => Items.Count(x => !x.Scenario);

        public int ScenariosCount => Items.Count(x => x.Scenario);

        public bool IsReadOnly => false;

        public int Missing => Items.Count(x => x.State != DependencyState.Downloaded && !x.Scenario);

        public int MissingScenario => Items.Count(x => x.State != DependencyState.Downloaded && x.Scenario);

        public int Downloadable => Items.Count(x => x.State == DependencyState.Available && !x.Scenario);

        public int DownloadableScenario => Items.Count(x => x.State == DependencyState.Available && x.Scenario);

        public bool Unknown => Items.Any(x => x.State == DependencyState.Unknown);

        public delegate void DependenciesChangedEventHandler();
        public event DependenciesChangedEventHandler DependenciesChanged;

        public DependenciesList()
        {
            Items = new List<Dependency>();
        }

        public DependenciesList(IEnumerable<Dependency> input)
        {
            Items = input.ToList();
        }

        public DependenciesList(IEnumerable<string> input)
        {
            Items = new List<Dependency>();
            for (int i = 0; i < input.Count(); i++)
            {
                Items.Add(new Dependency(Railworks.NormalizePath(input.ElementAt(i)), DependencyState.Unknown, false));
            }
        }

        public void Add(Dependency item)
        {
            if (Items.Any(x => x.Name == item?.Name) && !string.IsNullOrWhiteSpace(item?.Name))
            {
                Dependency dependency = Items.First(x => x.Name == item.Name);
                if (item.Presence != null)
                    dependency.Presence.UnionWith(item.Presence);
            }
            else
            {
                Items.Add(item);
            }

            DependenciesChanged?.Invoke();
        }

        public void Clear()
        {
            Items.Clear();
            DependenciesChanged?.Invoke();
        }

        public bool Contains(Dependency item)
        {
            return Items.Contains(item);
        }

        public void CopyTo(Dependency[] array, int arrayIndex)
        {
            Array.Copy(Items.ToArray(), 0, array, arrayIndex, Items.Count);
        }

        public IEnumerator<Dependency> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        public int IndexOf(Dependency item)
        {
            return Items.FindIndex(x => x.Name == item?.Name);
        }

        public void Insert(int index, Dependency item)
        {
            if (Items.Any(x => x.Name == item?.Name))
            {
                Dependency dependency = Items.First(x => x.Name == item.Name);
                if (item.Presence != null)
                    dependency.Presence.UnionWith(item.Presence);
            }
            else
            {
                Items.Insert(index, item);
            }
            DependenciesChanged?.Invoke();
        }

        public void AddDependencies(string[] dependencies, bool scenario)
        {
            for (int i = 0; i < dependencies.Length; i++)
            {
                string dep = dependencies[i];

                if (!Items.Any(x => x.Name == dep) && !string.IsNullOrWhiteSpace(dep))
                {
                    Items.Add(new Dependency(dep, scenario));
                }
            }
            DependenciesChanged?.Invoke();
        }

        public void AddDependencies(List<string> dependencies, bool scenario)
        {
            for (int i = 0; i < dependencies.Count; i++)
            {
                string dep = dependencies[i];

                if (!Items.Any(x => x.Name == dep) && !string.IsNullOrWhiteSpace(dep))
                {
                    Items.Add(new Dependency(dep, scenario));
                }
            }
            DependenciesChanged?.Invoke();
        }

        public bool Remove(Dependency item)
        {
            DependenciesChanged?.Invoke();
            return Items.RemoveAll(x => x.Name == item?.Name) > 0;
        }

        public void RemoveAt(int index)
        {
            Items.RemoveAt(index);
            DependenciesChanged?.Invoke();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Items.GetEnumerator();
        }
    }
}
