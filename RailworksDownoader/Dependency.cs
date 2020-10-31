using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RailworksDownloader
{
    class Dependency
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
