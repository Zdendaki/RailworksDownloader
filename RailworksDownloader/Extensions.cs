using System;
using System.Collections.Generic;
using System.Linq;

namespace RailworksDownloader
{
    internal static class Extensions
    {
        internal static IEnumerable<T2> Substract<T1, T2>(this IDictionary<T1, T2> dictionary, IEnumerable<T1> keys)
        {
            return keys.Where(x => dictionary.ContainsKey(x)).Select(x => dictionary[x]);
        }

        public static IEnumerable<TSource> Intersect<TSource>(this HashSet<TSource> first, HashSet<TSource> second)
        {
            if (first == null) throw new ArgumentException("first");
            if (second == null) throw new ArgumentException("second");

            return (first.Count > second.Count) ?
                first.IntersectEnumerator(second, EqualityComparer<TSource>.Default) :
                    second.IntersectEnumerator(first, EqualityComparer<TSource>.Default);
        }

        public static IEnumerable<TSource> Intersect<TSource>(this HashSet<TSource> first, HashSet<TSource> second, EqualityComparer<TSource> comparer)
        {
            if (first == null) throw new ArgumentException("first");
            if (second == null) throw new ArgumentException("second");

            return (first.Count > second.Count) ?
                first.IntersectEnumerator(second, comparer) :
                    second.IntersectEnumerator(first, comparer);
        }

        private static IEnumerable<TSource> IntersectEnumerator<TSource>(this HashSet<TSource> first, HashSet<TSource> second, EqualityComparer<TSource> comparer)
        {
            if (first.Comparer != comparer)
                return Intersect(first, second, comparer);
            else
                return IntersectEnumerator(first, second);
        }

        private static IEnumerable<TSource> IntersectEnumerator<TSource>(this HashSet<TSource> first, HashSet<TSource> second)
        {
            foreach (TSource tmp in second)
            {
                if (first.Contains(tmp))
                {
                    yield return tmp;
                }
            }
        }

        internal static TSource PopOne<TSource>(this HashSet<TSource> hashSetToPop)
        {
            if (hashSetToPop.Count == 0)
                return default;

            TSource res = hashSetToPop.First();
            hashSetToPop.Remove(res);

            return res;
        }
    }
}
