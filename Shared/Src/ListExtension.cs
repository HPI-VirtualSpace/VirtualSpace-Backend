using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtualSpace.Shared
{
    public static partial class ListExtension
    {
        public static IList<T> Clone<T>(this IList<T> l) where T : ICloneable
        {
            return l.Select(t => (T)t.Clone()).ToList();
        }

        public static LinkedList<T> ToLinkedList<T>(this IEnumerable<T> iEnumerable)
        {
            return new LinkedList<T>(iEnumerable);
        }

        public static IEnumerable<LinkedListNode<T>> IterateNodes<T>(this LinkedList<T> ll)
        {
            for (var lln = ll.First; lln != null; lln = lln.Next)
            {
                yield return lln;
            }
        }
    }
}
