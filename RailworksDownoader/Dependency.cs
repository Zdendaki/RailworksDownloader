using Desharp;
using RailworksDownloader;
using SteamKit2.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Windows.AI.MachineLearning;
using Windows.UI.Xaml.Automation;

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

    public class GlobalDependencies : IEnumerable, IDictionary<int, Dependency>
    {
        Dictionary<int, Dependency> Dependencies;
        internal object GlobalLock { get; set; } = new object();
        int id;

        public GlobalDependencies()
        {
            Dependencies = new Dictionary<int, Dependency>();
            id = 0;
        }

        public Dependency this[int key] 
        {
            get => Dependencies[key];
            set => Dependencies[key] = value;
        }

        public ICollection<int> Keys => Dependencies.Keys;

        public ICollection<Dependency> Values => Dependencies.Values;

        public int Count => Dependencies.Count;

        public bool IsReadOnly => false;

        public int Add(Dependency dependency)
        {

                if (Dependencies.Any(x => x.Value.Name == dependency.Name))
                {
                    return Dependencies.First(x => x.Value.Name == dependency.Name).Key;
                }
                else
                {
                    Dependencies.Add(id, dependency);
                    id++;
                    return id - 1;
                }
            
        }

        public void Add(int key, Dependency value)
        {
            throw new InvalidOperationException("Items cannot be added to dictionary with key. Key is generated automatically");
        }

        public void Add(KeyValuePair<int, Dependency> item)
        {
            throw new InvalidOperationException("Items cannot be added to dictionary with key. Key is generated automatically");
        }

        public void Clear()
        {
            Dependencies.Clear();
            id = 0;
        }

        public bool Contains(KeyValuePair<int, Dependency> item)
        {
            return Dependencies.Contains(item);
        }

        public bool ContainsKey(int key)
        {
            return Dependencies.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<int, Dependency>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator GetEnumerator()
        {
            return Dependencies.Values.GetEnumerator();
        }

        public bool Remove(int key)
        {
            return Dependencies.Remove(key);
        }

        public bool Remove(KeyValuePair<int, Dependency> item)
        {
            return Dependencies.Remove(item.Key);
        }

        public bool TryGetValue(int key, out Dependency value)
        {
            return Dependencies.TryGetValue(key, out value);
        }

        IEnumerator<KeyValuePair<int, Dependency>> IEnumerable<KeyValuePair<int, Dependency>>.GetEnumerator()
        {
            return Dependencies.GetEnumerator();
        }
    }

    public class DependenciesList : IList<Dependency>, IEnumerable<Dependency>, IEnumerable
    {
        private readonly List<int> Items;


        public Dependency this[int index]
        {
            get => App.Dependencies[Items[index]];
            set => App.Dependencies[index] = value;
        }

        public int Count => Items.Count;

        public int RouteCount => App.Dependencies.Substract(Items).Count(x => !x.Scenario);

        public int ScenariosCount => App.Dependencies.Substract(Items).Count(x => x.Scenario);

        public bool IsReadOnly => false;

        public int Missing => App.Dependencies.Substract(Items).Count(x => x.State != DependencyState.Downloaded && !x.Scenario);

        public int MissingScenario => App.Dependencies.Substract(Items).Count(x => x.State != DependencyState.Downloaded && x.Scenario);

        public int Downloadable => App.Dependencies.Substract(Items).Count(x => x.State == DependencyState.Available && !x.Scenario);

        public int DownloadableScenario => App.Dependencies.Substract(Items).Count(x => x.State == DependencyState.Available && x.Scenario);

        public bool Unknown => App.Dependencies.Substract(Items).Any(x => x.State == DependencyState.Unknown);

        public delegate void DependenciesChangedEventHandler();
        public event DependenciesChangedEventHandler DependenciesChanged;

        public DependenciesList()
        {
            Items = new List<int>();
        }

        public DependenciesList(IEnumerable<Dependency> input)
        {
            Items = new List<int>();
            foreach (var dep in input)
            {
                lock (App.Dependencies.GlobalLock)
                {
                    int id = App.Dependencies.Add(dep);

                    if (!Items.Contains(id))
                        Items.Add(id);
                }
            }
        }

        public DependenciesList(IEnumerable<string> input)
        {
            throw new NotImplementedException();
            
            Items = new List<int>();
            for (int i = 0; i < input.Count(); i++)
            {
                int id = App.Dependencies.Add(new Dependency(Railworks.NormalizePath(input.ElementAt(i)), DependencyState.Unknown, false));

                if (!Items.Contains(id))
                    Items.Add(id);
            }
        }

        public void Add(Dependency item)
        {
            lock (App.Dependencies.GlobalLock)
            {
                int id = App.Dependencies.Add(item);

                if (!Items.Contains(id))
                    Items.Add(id);
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
            return App.Dependencies.Any(x => x.Value == item);
        }

        public void CopyTo(Dependency[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<Dependency> GetEnumerator()
        {
            return App.Dependencies.Values.GetEnumerator();
        }

        public int IndexOf(Dependency item)
        {
            return App.Dependencies.FirstOrDefault(x => x.Value.Name == item?.Name).Key;
        }

        public void Insert(int index, Dependency item)
        {
            throw new NotImplementedException();
        }

        public void AddDependencies(string[] dependencies, bool scenario)
        {
            for (int i = 0; i < dependencies.Length; i++)
            {
                string dep = dependencies[i];

                lock (App.Dependencies.GlobalLock)
                {
                    if (string.IsNullOrWhiteSpace(dep))
                    {
                        Debug.Assert(false, "Dependency is whitespace!");
                        continue;
                    }

                    int id = App.Dependencies.Add(new Dependency(dep, scenario));

                    if (!Items.Contains(id))
                        Items.Add(id);
                }
                
            }

            DependenciesChanged?.Invoke();
        }

        public void AddDependencies(List<string> dependencies, bool scenario)
        {
            for (int i = 0; i < dependencies.Count; i++)
            {
                string dep = dependencies[i];

                lock (App.Dependencies.GlobalLock)
                {
                    if (string.IsNullOrWhiteSpace(dep))
                    {
                        Debug.Assert(false, "Dependency is whitespace!");
                        continue;
                    }

                    int id = App.Dependencies.Add(new Dependency(dep, scenario));

                    if (!Items.Contains(id))
                        Items.Add(id);
                }
                
            }
            DependenciesChanged?.Invoke();
        }

        public bool Remove(Dependency item)
        {
            DependenciesChanged?.Invoke();
            return Items.Remove(GetKey(item));
        }

        private int GetKey(Dependency item)
        {
            return App.Dependencies.FirstOrDefault(x => x.Value.Name == item?.Name).Key;
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
