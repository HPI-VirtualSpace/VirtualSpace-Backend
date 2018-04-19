using System;
#if BACKEND
using System.Collections.Concurrent;
#endif
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VirtualSpace.Shared
{
    public static class DictionaryExtension
    {
        public static string ToPrintableString<K, V>(this Dictionary<K, V> d)
        {
            StringBuilder builder = new StringBuilder();

            foreach (KeyValuePair<K, V> pair in d)
            {
                builder.Append(pair.Key).Append(":").Append(pair.Value).Append(',');
            }

            string result = builder.ToString();
            result = result.TrimEnd(',');

            return result;
        }

        public static void ForEachKey<K, V>(this Dictionary<K, V> d, Action<K> a)
        {
            foreach (K k in d.Keys)
                a(k);
        }

        public static void ForEachValue<K, V>(this Dictionary<K, V> d, Action<V> a)
        {
            foreach (V v in d.Values)
                a(v);
        }

#if BACKEND
        public static void ForEachValue<K, V>(this ConcurrentDictionary<K, V> d, Action<V> a)
        {
            foreach (V v in d.Values)
                a(v);
        }
#endif

        public static void ModifyEachValue<K, V>(this Dictionary<K, V> d, Func<V, V> a)
        {
            foreach (K k in d.Keys.ToList())
                d[k] = a(d[k]);
        }

        public static void OverwriteWith<K, V>(this Dictionary<K, V> d, Dictionary<K, V> o)
        {
            foreach (KeyValuePair<K, V> pair in o)
                d[pair.Key] = pair.Value;
        }
    }
}
