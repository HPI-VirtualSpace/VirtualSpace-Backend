using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtualSpace.Shared
{
    public static partial class EnumExtension
    {
        public static IEnumerable<T> GetValues<T>()
        {
            return Enum.GetValues(typeof(T)).Cast<T>();
        }

        public static bool IsEmpty<T>(this List<T> l)
        {
            return l.Count == 0;
        }

        public static List<T> RotateRight<T>(this List<T> l, int offset)
        {
            List<T> cL = new List<T>(l);
            cL.Reverse();
            cL = cL.Skip(1).Concat(cL.Take(1)).ToList();
            cL.Reverse();

            return cL;
        }

        public static void ForEach<T>(this List<T> l, Action<T> action)
        {
            foreach (T t in l)
                action(t);
        }

        public static int RemoveRange<T>(this List<T> l, List<T> ld)
        {
            int numRemoved = 0;
            foreach (T d in ld)
                if (l.Remove(d))
                    numRemoved++;
            return numRemoved;
        }

        public static string ToPrintableString<T>(this List<T> l)
        {
            string result;

            result = "List(";
            foreach (T t in l)
            {
                result += t + ",";
            }
            result = result.TrimEnd(',');
            result += ')';

            return result;
        }

        public static bool TrueForOne<T>(this List<T> l, Predicate<T> checkFunction)
        {
            foreach (T t in l)
                if (checkFunction(t))
                    return true;
            return false;
        }

        public static void AddIfNotContained<T>(this List<T> l, T t)
        {
            if (!l.Contains(t)) l.Add(t);
        }

        public static void AddRangeIfNotContained<T>(this List<T> l, IEnumerable<T> o)
        {
            foreach (T t in o) l.AddIfNotContained(t);
        }

        public static bool TryPop<T>(this List<T> list, out T t)
        {
            if (list.Count == 0)
            {
                t = default(T);
                return false;
            }
            t = list[0];
            list.RemoveAt(0);
            return true;
        }
    }
}
