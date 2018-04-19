using System.Collections.Generic;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class ManagedAllocationIntersection
    {
        public Polygon Intersection;
        public List<int> Players;

        public ManagedAllocationIntersection(Polygon intersection, List<int> players)
        {
            Intersection = intersection;
            Players = players;
        }
    }
}
