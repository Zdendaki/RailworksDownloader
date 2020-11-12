using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RailworksDownloader
{
    static class Extensions
    {
        internal static IEnumerable<T2> Substract<T1, T2>(this IDictionary<T1, T2> dictionary, IEnumerable<T1> keys)
        {
            return keys.Where(x => dictionary.ContainsKey(x)).Select(x => dictionary[x]);
        }
    }
}
